using UnityEngine;
using FishNet.Object;
using UnityEngine.InputSystem;
using Genesis.Core;
using Genesis.Simulation.Combat;
using Genesis.Items;

namespace Genesis.Simulation {

    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotorMultiplayer : NetworkBehaviour
    {
        [Header("References")]
        public Transform cameraTransform;
        public Animator animator;
        [SerializeField] private TargetingSystem targeting;

        [Header("Movement Settings")]
        public float walkSpeed = 6f;
        public float runSpeed = 10f;
        public float rotationSpeed = 15f;
        public float gravity = -20f;

        // Variables internas
        private CharacterController _cc;
        private Vector3 _velocity; // Velocidad vertical (gravedad)
        private Vector3 _lastPosition;
        private float _lastAnimSpeed;
        private StatusEffectSystem _statusEffects;
        private EquipmentManager _equipmentManager;
        private bool _isDashing;

        // ═══════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════

        private void Awake() {
            _cc = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _statusEffects = GetComponent<StatusEffectSystem>();
            _equipmentManager = GetComponent<EquipmentManager>();
        }

        public override void OnStartClient() {
            base.OnStartClient();

            // Setup inicial solo para el dueño
            if (base.IsOwner) {
                // Intentar conectar cámara
                if (LostArkCamera.Instance != null) {
                    LostArkCamera.Instance.SetTarget(transform);
                    cameraTransform = LostArkCamera.Instance.pivot;
                } else if (Camera.main != null) {
                    cameraTransform = Camera.main.transform;
                }

                // Asignar target al sistema de fade de objetos (OccluderFader)
                if (OccluderFader.Instance != null) {
                    OccluderFader.Instance.SetTarget(transform);
                }
            }

            _lastPosition = transform.position;
        }

        // ═══════════════════════════════════════════════════════
        // UPDATE LOOP
        // ═══════════════════════════════════════════════════════

        void Update() {
            // 1. ANIMACIONES (Para TODOS: Owner y Remotos)
            // Calculamos la velocidad visualmente para que el BlendTree funcione en todos los clientes
            // sin necesidad de gastar ancho de banda sincronizando un float "Speed".
            UpdateAnimations();

            // 2. MOVIMIENTO (Solo Owner)
            if (base.IsOwner) {
                if (!_isDashing) {
                    HandleMovement();
                } else {
                    // Durante el dash solo aplicamos gravedad para no quedarnos flotando
                    ApplyGravityOnly();
                }
            }
        }

        private void ApplyGravityOnly() {
            if (_cc.isGrounded) {
                _velocity.y = -5f; // Fuerza de "stick" para mantenerse pegado al suelo
            } else {
                _velocity.y += gravity * Time.deltaTime;
            }
            _cc.Move(Vector3.up * _velocity.y * Time.deltaTime);
        }

        // ═══════════════════════════════════════════════════════
        // DASH / FORCED MOVEMENT
        // ═══════════════════════════════════════════════════════

        public void PerformDash(Vector3 targetPosition, float duration) {
            StartCoroutine(DashCoroutine(targetPosition, duration));
        }

        private System.Collections.IEnumerator DashCoroutine(Vector3 target, float duration) {
            Vector3 startPos = transform.position;
            float elapsed = 0f;

            _isDashing = true;

            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                
                // Calculamos la posición deseada en este frame
                Vector3 desiredPos = Vector3.Lerp(startPos, target, t);
                
                // Calculamos el desplazamiento necesario desde la posición ACTUAL
                // Esto permite que el CC choque con paredes y deje de avanzar
                Vector3 movement = desiredPos - transform.position;
                
                // IMPORTANTE: Permitir el movimiento en Y para que el dash siga la altura
                // calculada por el NavMesh en el DashLogic (ramp support)

                _cc.Move(movement);

                yield return null;
            }

            _isDashing = false;
        }

        // ═══════════════════════════════════════════════════════
        // MOVEMENT LOGIC (Owner)
        // ═══════════════════════════════════════════════════════

        private void HandleMovement() {
            // ═══ STATUS EFFECTS CHECKS ═══
            if (_statusEffects != null) {
                // Stun = no movimiento ni acciones
                if (_statusEffects.HasEffect(Data.EffectType.Stun)) {
                    // Animación idle forzada
                    if (animator != null) animator.SetFloat("Speed", 0f);
                    return;
                }

                // Root = no movimiento pero sí acciones
                if (_statusEffects.HasEffect(Data.EffectType.Root)) {
                    // Animación idle forzada
                    if (animator != null) animator.SetFloat("Speed", 0f);

                    // FIX: Mantener rotación hacia el target aunque esté rooteado
                    if (targeting != null && targeting.CurrentTarget != null) {
                        Vector3 dirToTarget = (targeting.CurrentTarget.transform.position - transform.position);
                        dirToTarget.y = 0;
                        if (dirToTarget.sqrMagnitude > 0.001f) {
                            Quaternion targetRot = Quaternion.LookRotation(dirToTarget);
                            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                        }
                    }
                    return;
                }
            }

            // Input
            Vector2 input = GetInput();

            // Dirección basada en cámara
            Vector3 moveDir = Vector3.zero;
            if (cameraTransform != null) {
                Vector3 camFwd = cameraTransform.forward;
                Vector3 camRight = cameraTransform.right;
                camFwd.y = 0;
                camRight.y = 0;
                camFwd.Normalize();
                camRight.Normalize();

                moveDir = camFwd * input.y + camRight * input.x;
            } else {
                moveDir = new Vector3(input.x, 0, input.y);
            }

            // Velocidad
            bool isRunning = (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed);
            float targetSpeed = isRunning ? runSpeed : walkSpeed;

            if (moveDir.sqrMagnitude < 0.01f) targetSpeed = 0f;

            // ═══ APLICAR STATUS EFFECT MULTIPLIERS ═══
            if (_statusEffects != null && targetSpeed > 0f) {
                float speedMultiplier = _statusEffects.GetMovementSpeedMultiplier();
                targetSpeed *= speedMultiplier;
            }

            // Movimiento Horizontal
            Vector3 velocityXZ = moveDir * targetSpeed;
            
            // Gravedad y Grounding
            if (_cc.isGrounded) {
                // Si estamos en el suelo, aplicamos una fuerza constante hacia abajo (-5f)
                // Esto ayuda a que el CC no pierda el contacto en rampas descendentes.
                _velocity.y = -5f; 
            } else {
                // En el aire, aplicamos gravedad acumulativa
                _velocity.y += gravity * Time.deltaTime;
            }

            // Aplicar Movimiento
            Vector3 finalMove = velocityXZ + Vector3.up * _velocity.y;
            _cc.Move(finalMove * Time.deltaTime);

            // Rotación
            if (moveDir.sqrMagnitude > 0.01f) {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

        private Vector2 GetInput() {
            if (InputManager.Instance != null) {
                var move = InputManager.Instance.GetMoveInput3D();
                return new Vector2(move.x, move.z).normalized;
            }
            // Fallback
            if (Keyboard.current != null) {
                Vector2 v = Vector2.zero;
                if (Keyboard.current.wKey.isPressed) v.y += 1;
                if (Keyboard.current.sKey.isPressed) v.y -= 1;
                if (Keyboard.current.aKey.isPressed) v.x -= 1;
                if (Keyboard.current.dKey.isPressed) v.x += 1;
                return v.normalized;
            }
            return Vector2.zero;
        }

        // ═══════════════════════════════════════════════════════
        // ANIMATION
        // ═══════════════════════════════════════════════════════

        private void UpdateAnimations() {
            if (animator == null) return;

            float currentSpeed = 0f;

            if (base.IsOwner) {
                // Owner usa la velocidad real del controller
                currentSpeed = new Vector3(_cc.velocity.x, 0, _cc.velocity.z).magnitude;
            } else {
                // Remotos calculan velocidad basada en desplazamiento
                Vector3 displacement = transform.position - _lastPosition;
                displacement.y = 0; // Ignorar vertical para animacion de correr
                float rawSpeed = displacement.magnitude / Time.deltaTime;
                
                currentSpeed = Mathf.Lerp(_lastAnimSpeed, rawSpeed, Time.deltaTime * 10f);
                
                _lastAnimSpeed = currentSpeed;
                _lastPosition = transform.position;
            }

            animator.SetFloat("Speed", currentSpeed);

            // ═══ WEAPON STATE ANIMATION ═══
            if (_equipmentManager != null) {
                bool hasWeapon = !_equipmentManager.IsSlotEmpty(EquipmentSlot.Weapon);
                animator.SetBool("HasWeapon", hasWeapon);
            }
        }
    }
}

using UnityEngine;
using FishNet.Object;
using UnityEngine.InputSystem; 
using Genesis.Core;

namespace Genesis.Simulation {

    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotorMultiplayer : NetworkBehaviour 
    {
        [Header("References")]
        public Transform cameraTransform;
        public Animator animator;

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

        // ═══════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════

        private void Awake() {
            _cc = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
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
                HandleMovement();
            }
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

            // Desactivar temporalmente el control manual si es necesario
            // _isDashing = true; 

            while (elapsed < duration) {
                // Mover CC hacia el target
                // Nota: Usamos Move para respetar colisiones con paredes durante el dash
                Vector3 currentPos = Vector3.Lerp(startPos, target, elapsed / duration);
                Vector3 direction = (target - startPos).normalized;
                float distanceFrame = Vector3.Distance(transform.position, target) * (Time.deltaTime / (duration - elapsed));
                
                // Opción A: Teleport suave (ignora paredes)
                // transform.position = currentPos;
                
                // Opción B: Move físico (choca con paredes)
                // _cc.Move(direction * (Vector3.Distance(startPos, target) / duration) * Time.deltaTime);
                
                // Opción C: Lerp directo con desactivación de CC (Smooth Teleport)
                _cc.enabled = false;
                transform.position = currentPos;
                _cc.enabled = true;

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Asegurar posición final
            _cc.enabled = false;
            transform.position = target;
            _cc.enabled = true;
        }

        // ═══════════════════════════════════════════════════════
        // MOVEMENT LOGIC (Owner)
        // ═══════════════════════════════════════════════════════

        private void HandleMovement() {
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

            // Movimiento Horizontal
            Vector3 velocityXZ = moveDir * targetSpeed;
            
            // Gravedad (Vertical)
            if (_cc.isGrounded && _velocity.y < 0) {
                _velocity.y = -2f; // Mantener pegado al suelo
            }
            _velocity.y += gravity * Time.deltaTime;

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
        }
    }
}

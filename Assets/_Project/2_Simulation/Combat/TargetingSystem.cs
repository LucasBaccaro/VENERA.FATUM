using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using Genesis.Core;
using System.Collections.Generic;

namespace Genesis.Simulation {

    public class TargetingSystem : NetworkBehaviour {
        
        [Header("Settings")]
        [SerializeField] private float maxTargetDistance = 40f;
        [SerializeField] private LayerMask targetLayer; // Capa de enemigos (Layer 6: Enemy)
        [SerializeField] private LayerMask groundLayer; // Capa de suelo (Layer 8: Environment)
        
        [Header("Visuals")]
        [SerializeField] private GameObject targetRingPrefab;
        [SerializeField] private float ringScaleMultiplier = 1.2f; // Nuevo: Ajuste manual de tamaño
        [SerializeField] private float ringHeight = 0.05f; // Nuevo: Altura fija (plano)
        [SerializeField] private GameObject cursorCrossPrefab;
        
        // State
        public NetworkObject CurrentTarget { get; private set; }
        
        private GameObject _targetRingInstance;
        private GameObject _cursorCrossInstance;
        
        private bool _isGroundTargeting;
        private Vector3 _groundTargetPoint;

        public override void OnStartClient() {
            base.OnStartClient();
            
            // Instanciar visuales desactivados (Pool local simple)
            if (targetRingPrefab != null) {
                _targetRingInstance = Instantiate(targetRingPrefab);
                _targetRingInstance.SetActive(false);
                // Asegurar que no interfiera con Raycasts
                DestroyCollider(_targetRingInstance);
            }
            
            if (cursorCrossPrefab != null) {
                _cursorCrossInstance = Instantiate(cursorCrossPrefab);
                _cursorCrossInstance.SetActive(false);
                DestroyCollider(_cursorCrossInstance);
            }
        }

        private void DestroyCollider(GameObject obj) {
            foreach(var col in obj.GetComponentsInChildren<Collider>()) {
                Destroy(col);
            }
        }

        void Update() {
            if (!base.IsOwner) return;

            // Solo permitir targeting si NO estamos haciendo ground targeting activo
            if (!_isGroundTargeting) {
                // 1. SELECCIÓN DE TARGET (Click Izquierdo)
                if (Mouse.current.leftButton.wasPressedThisFrame) {
                    TrySelectTarget();
                }

                // 2. CICLAR TARGETS (Tab)
                if (Keyboard.current.tabKey.wasPressedThisFrame) {
                    CycleTargets();
                }

                // 3. DESELECCIONAR (Escape)
                if (Keyboard.current.escapeKey.wasPressedThisFrame) {
                    ClearTarget();
                }
            }

            // 4. ACTUALIZAR VISUALES
            UpdateVisuals();
        }

        private void TrySelectTarget() {
            // Verificar dependencias críticas
            if (Camera.main == null) {
                Debug.LogWarning("[TargetingSystem] No MainCamera found! Cannot raycast.");
                return;
            }

            if (Mouse.current == null) {
                return; // No mouse device connected
            }

            // Verificar si el puntero está sobre UI (requiere EventSystem, simplificado aquí)
            // if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            
            if (Physics.Raycast(ray, out RaycastHit hit, maxTargetDistance, targetLayer)) {
                // Click en enemigo
                if (hit.collider.TryGetComponent(out NetworkObject netObj)) {
                    // Validar que no sea yo mismo (por si acaso)
                    if (netObj != base.NetworkObject) {
                        SetTarget(netObj);
                        return;
                    }
                }
            } 
            
            // Si llegamos aquí, no golpeamos un enemigo válido.
            // Opcional: Click en suelo deselecciona? 
            // Muchos MMOs deseleccionan al clickear en vacío o suelo.
            if (!Physics.Raycast(ray, maxTargetDistance, LayerMask.GetMask("UI"))) {
                 ClearTarget();
            }
        }

        private void CycleTargets() {
            // Buscar enemigos cercanos
            Collider[] hits = Physics.OverlapSphere(transform.position, maxTargetDistance, targetLayer);
            if (hits.Length == 0) return;

            // Filtrar y Ordenar por distancia
            List<NetworkObject> candidates = new List<NetworkObject>();
            foreach(var hit in hits) {
                // Solo agregar si tiene NetworkObject y NO soy yo
                if(hit.TryGetComponent(out NetworkObject no) && no != base.NetworkObject) {
                    candidates.Add(no);
                }
            }
            
            if (candidates.Count == 0) return;

            // Ordenar por distancia ascendente
            candidates.Sort((a, b) => 
                Vector3.Distance(transform.position, a.transform.position)
                .CompareTo(Vector3.Distance(transform.position, b.transform.position))
            );

            // Encontrar índice actual
            int currentIndex = -1;
            if (CurrentTarget != null) {
                currentIndex = candidates.IndexOf(CurrentTarget);
            }

            // Seleccionar siguiente (Wrap around)
            int nextIndex = (currentIndex + 1) % candidates.Count;
            SetTarget(candidates[nextIndex]);
        }

        public void SetTarget(NetworkObject target) {
            if (CurrentTarget == target) return;

            CurrentTarget = target;
            EventBus.Trigger("OnTargetChanged", target); // Notificar al HUD
            Debug.Log($"[Targeting] Selected: {target.name}");
        }

        public void ClearTarget() {
            if (CurrentTarget != null) {
                CurrentTarget = null;
                EventBus.Trigger("OnTargetCleared");
                Debug.Log("[Targeting] Cleared");
            }
        }

        private void UpdateVisuals() {
            // Target Ring logic
            if (_targetRingInstance != null) {
                if (CurrentTarget != null) {
                    _targetRingInstance.SetActive(true);
                    
                    // Posicionar a los pies del target
                    _targetRingInstance.transform.position = CurrentTarget.transform.position + Vector3.up * 0.05f;
                    
                    // Escalar según el tamaño del target (si tiene collider)
                    float finalScale = 1f;
                    if (CurrentTarget.TryGetComponent(out Collider col)) {
                        // Usamos el maximo entre X y Z del bounds para cubrir todo el ancho
                        float targetWidth = Mathf.Max(col.bounds.size.x, col.bounds.size.z);
                        finalScale = targetWidth * ringScaleMultiplier;
                    }

                    // Aplicar escala: X/Z variables, Y fija (plano)
                    _targetRingInstance.transform.localScale = new Vector3(finalScale, ringHeight, finalScale);

                } else {
                    _targetRingInstance.SetActive(false);
                }
            }
        }
        
        // GROUND TARGETING API (Placeholder para Fase de Habilidades)
        public Vector3 GetGroundTargetPoint() => _groundTargetPoint;
    }
}

using UnityEngine;
using Genesis.Data;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Indicador de flecha para habilidades de movimiento/dash (ej: Carga, Desenganche)
    /// Muestra una flecha y línea de trayectoria hacia el punto de destino
    /// </summary>
    public class ArrowIndicator : AbilityIndicator {

        [Header("Arrow Components")]
        [SerializeField] private GameObject arrowModel; // Modelo 3D de flecha
        [SerializeField] private LineRenderer pathLine; // Línea de trayectoria
        [SerializeField] private Renderer arrowRenderer;

        [Header("Dash Settings")]
        [SerializeField] private bool isBackwards = false; // True para Desenganche
        [SerializeField] private float maxDashDistance = 15f;

        private Vector3 _startPoint;
        private Vector3 _targetPoint;
        private Vector3 _direction;
        private LayerMask _groundLayer;

        public override void Initialize(AbilityData abilityData) {
            _abilityData = abilityData;
            maxDashDistance = abilityData.Range;

            // Detectar si es backwards (Desenganche) - se configura en el prefab
            // O podríamos detectarlo por nombre de habilidad
            if (abilityData.Name.Contains("Desenganche") || abilityData.Name.Contains("Disengage")) {
                isBackwards = true;
            }

            _groundLayer = LayerMask.GetMask("Environment");
            _isValid = true;
        }

        public override void UpdatePosition(Vector3 worldPoint, Vector3 direction) {
            _startPoint = transform.position;

            // Calcular dirección (invertir si es backwards)
            if (isBackwards) {
                direction = -direction;
            }

            _direction = direction.normalized;

            // Calcular punto deseado
            Vector3 desiredPoint = _startPoint + _direction * maxDashDistance;

            // 1. Levantar el origen del Raycast para evitar chocar con el suelo (pequeños desniveles)
            Vector3 checkOrigin = _startPoint + Vector3.up * 0.5f;

            // Raycast para detectar obstáculos en el camino
            if (Physics.Raycast(checkOrigin, _direction, out RaycastHit hit, maxDashDistance, obstacleMask)) {
                
                // Hay un obstáculo - ajustar punto antes del obstáculo (Partial Dash)
                // En lugar de invalidar, permitimos cargar HASTA el obstáculo.
                _targetPoint = hit.point - _direction * 0.9f; 
                
                // Bajamos el punto al suelo visualmente si es posible, para que la flecha no quede flotando
                if (Physics.Raycast(_targetPoint + Vector3.up, Vector3.down, out RaycastHit floorHit, 2f, _groundLayer)) {
                    _targetPoint.y = floorHit.point.y;
                } else {
                    _targetPoint.y = _startPoint.y; // Fallback a altura original
                }

                _isValid = true; // AHORA ES VÁLIDO (Carga parcial)

            } else {
                // Camino libre
                
                // Validar que haya suelo en el destino (para no caer al vacío)
                Ray groundRay = new Ray(desiredPoint + Vector3.up * 2f, Vector3.down);

                if (Physics.Raycast(groundRay, out RaycastHit groundHit, 5f, _groundLayer)) {
                    _targetPoint = groundHit.point + Vector3.up * 0.1f; // Ajuste fino
                    _isValid = true;
                } else {
                    // No hay suelo (ej: precipicio).
                    // Opción A: Invalidar. Opción B: Clamp al borde.
                    // Mantenemos invalidar para seguridad, pero rara vez pasa en terreno plano.
                    _targetPoint = desiredPoint;
                    _isValid = false; 
                }
            }

            // Actualizar visual - Flecha
            if (arrowModel != null) {
                arrowModel.transform.position = _targetPoint;
                arrowModel.transform.rotation = Quaternion.LookRotation(_direction);

                // Color según validez
                if (arrowRenderer != null) {
                    UpdateRendererColor(arrowRenderer);
                }
            }

            // Actualizar visual - Línea de trayectoria
            if (pathLine != null) {
                pathLine.positionCount = 2;
                pathLine.SetPosition(0, _startPoint);
                pathLine.SetPosition(1, _targetPoint);
                UpdateRendererColor(pathLine);
            }
        }

        public override Vector3 GetTargetPoint() => _targetPoint;

        public override Vector3 GetDirection() => _direction;

        public override bool IsValid() => _isValid;

        public override void Show() {
            base.Show();
            if (arrowModel != null) arrowModel.SetActive(true);
            if (pathLine != null) pathLine.enabled = true;
        }

        public override void Hide() {
            base.Hide();
            if (arrowModel != null) arrowModel.SetActive(false);
            if (pathLine != null) pathLine.enabled = false;
        }
    }
}

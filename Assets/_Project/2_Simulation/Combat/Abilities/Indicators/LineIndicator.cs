using UnityEngine;
using Genesis.Data;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Indicador de línea para skillshots direccionales (ej: Bola de Fuego, Rayo de Hielo)
    /// Muestra una línea desde el caster hacia la dirección del mouse
    /// </summary>
    public class LineIndicator : AbilityIndicator {

        [Header("Line Components")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private GameObject endMarker; // Esfera/objeto al final de la línea
        [SerializeField] private float defaultWidth = 1f;

        private float _range;
        private float _width;
        private Vector3 _startPoint;
        private Vector3 _endPoint;
        private Vector3 _direction;

        public override void Initialize(AbilityData abilityData) {
            _abilityData = abilityData;
            _range = abilityData.Range;
            _width = abilityData.Radius > 0 ? abilityData.Radius : defaultWidth;

            if (lineRenderer != null) {
                lineRenderer.startWidth = _width;
                lineRenderer.endWidth = _width;
                lineRenderer.positionCount = 2;
            }

            _isValid = true;
        }

        public override void UpdatePosition(Vector3 worldPoint, Vector3 direction) {
            _startPoint = transform.position;
            _direction = direction.normalized;

            // Calcular punto final (con detección de obstáculos)
            _endPoint = _startPoint + _direction * _range;

            // Raycast para detectar obstáculos (paredes, terreno)
            if (Physics.Raycast(_startPoint, _direction, out RaycastHit hit, _range, obstacleMask)) {
                _endPoint = hit.point;
                _isValid = false; // Obstruido por pared
            } else {
                _isValid = true;
            }

            // Actualizar LineRenderer
            if (lineRenderer != null) {
                lineRenderer.SetPosition(0, _startPoint);
                lineRenderer.SetPosition(1, _endPoint);
                UpdateRendererColor(lineRenderer);
            }

            // Actualizar marcador al final
            if (endMarker != null) {
                endMarker.transform.position = _endPoint;

                // Cambiar color del marcador también
                if (endMarker.TryGetComponent<Renderer>(out var markerRenderer)) {
                    UpdateRendererColor(markerRenderer);
                }
            }
        }

        public override Vector3 GetTargetPoint() => _endPoint;

        public override Vector3 GetDirection() => _direction;

        public override bool IsValid() => _isValid;

        public override void Show() {
            base.Show();
            if (lineRenderer != null) lineRenderer.enabled = true;
            if (endMarker != null) endMarker.SetActive(true);
        }

        public override void Hide() {
            base.Hide();
            if (lineRenderer != null) lineRenderer.enabled = false;
            if (endMarker != null) endMarker.SetActive(false);
        }
    }
}

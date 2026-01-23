using UnityEngine;
using Genesis.Data;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Indicador circular para habilidades AOE (ej: Meteorito, Sagrario, Torbellino)
    /// Puede ser ground-targeted (movible con mouse) o self-centered (fijo en el caster)
    /// </summary>
    public class CircleIndicator : AbilityIndicator {

        [Header("Circle Components")]
        [SerializeField] private GameObject circlePrefab; // Cylinder o Decal
        [SerializeField] private Renderer circleRenderer;
        [SerializeField] private float heightOffset = 0.1f; // Altura sobre el suelo

        [Header("Settings")]
        [SerializeField] private bool isSelfCentered = false; // True para Torbellino/Nova
        [SerializeField] private float maxDistance = 30f; // Máxima distancia de placement

        private float _radius;
        private Vector3 _targetPoint;
        private LayerMask _groundLayer;

        public override void Initialize(AbilityData abilityData) {
            _abilityData = abilityData;
            _radius = abilityData.Radius;
            maxDistance = abilityData.Range;

            // Detectar si es self-centered basado en TargetingMode
            isSelfCentered = (abilityData.TargetingMode == TargetType.Self);

            // Configurar escala del círculo
            if (circlePrefab != null) {
                float diameter = _radius * 2f;
                circlePrefab.transform.localScale = new Vector3(diameter, heightOffset, diameter);
            }

            _groundLayer = LayerMask.GetMask("Environment");
            _isValid = true;
        }

        public override void UpdatePosition(Vector3 worldPoint, Vector3 direction) {

            if (isSelfCentered) {
                // Modo self-centered: Siempre en la posición del caster
                _targetPoint = transform.position;
                _isValid = true;
            } else {
                // Modo ground-targeted: Raycast al suelo
                Ray ray = new Ray(worldPoint + Vector3.up * 100f, Vector3.down);

                if (Physics.Raycast(ray, out RaycastHit hit, 200f, _groundLayer)) {
                    _targetPoint = hit.point;

                    // Validar distancia desde el caster
                    float distanceToCaster = Vector3.Distance(transform.position, _targetPoint);
                    _isValid = (distanceToCaster <= maxDistance);
                } else {
                    // No hay suelo debajo
                    _isValid = false;
                }
            }

            // Actualizar visual
            if (circlePrefab != null) {
                circlePrefab.transform.position = _targetPoint + Vector3.up * heightOffset;

                // Cambiar color según validez
                if (circleRenderer != null) {
                    UpdateRendererColor(circleRenderer);
                }
            }
        }

        public override Vector3 GetTargetPoint() => _targetPoint;

        public override Vector3 GetDirection() {
            // Para círculos, la dirección es desde el caster hacia el target point
            return (_targetPoint - transform.position).normalized;
        }

        public override bool IsValid() => _isValid;

        public override void Show() {
            base.Show();
            if (circlePrefab != null) circlePrefab.SetActive(true);
        }

        public override void Hide() {
            base.Hide();
            if (circlePrefab != null) circlePrefab.SetActive(false);
        }

        /// <summary>
        /// Helper: Detecta cuántos enemigos están en el área (opcional, para UI feedback)
        /// </summary>
        public int GetEnemyCountInArea() {
            Collider[] hits = Physics.OverlapSphere(_targetPoint, _radius, LayerMask.GetMask("Enemy"));
            return hits.Length;
        }
    }
}

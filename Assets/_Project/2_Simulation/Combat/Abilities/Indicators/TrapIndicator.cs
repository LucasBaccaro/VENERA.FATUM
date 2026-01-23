using UnityEngine;
using Genesis.Data;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Indicador para trampas (ej: Trampa de Hielo)
    /// Muestra un círculo de placement + preview del modelo de la trampa
    /// </summary>
    public class TrapIndicator : AbilityIndicator {

        [Header("Trap Components")]
        [SerializeField] private GameObject circlePrefab; // Círculo de placement
        [SerializeField] private GameObject trapModelPreview; // Preview del modelo de trampa
        [SerializeField] private Renderer circleRenderer;
        [SerializeField] private float heightOffset = 0.1f;

        [Header("Settings")]
        [SerializeField] private float maxPlacementDistance = 5f;

        private float _triggerRadius;
        private Vector3 _targetPoint;
        private LayerMask _groundLayer;

        public override void Initialize(AbilityData abilityData) {
            _abilityData = abilityData;
            _triggerRadius = abilityData.Radius;
            maxPlacementDistance = abilityData.Range;

            // Configurar escala del círculo (área de trigger)
            if (circlePrefab != null) {
                float diameter = _triggerRadius * 2f;
                circlePrefab.transform.localScale = new Vector3(diameter, heightOffset, diameter);
            }

            _groundLayer = LayerMask.GetMask("Environment");
            _isValid = true;
        }

        public override void UpdatePosition(Vector3 worldPoint, Vector3 direction) {

            // Raycast al suelo
            Ray ray = new Ray(worldPoint + Vector3.up * 100f, Vector3.down);

            if (Physics.Raycast(ray, out RaycastHit hit, 200f, _groundLayer)) {
                _targetPoint = hit.point;

                // Validar distancia desde el caster
                float distanceToCaster = Vector3.Distance(transform.position, _targetPoint);
                _isValid = (distanceToCaster <= maxPlacementDistance);
            } else {
                // No hay suelo
                _isValid = false;
            }

            // Actualizar visual - Círculo
            if (circlePrefab != null) {
                circlePrefab.transform.position = _targetPoint + Vector3.up * heightOffset;

                if (circleRenderer != null) {
                    UpdateRendererColor(circleRenderer);
                }
            }

            // Actualizar visual - Preview del modelo de trampa
            if (trapModelPreview != null) {
                trapModelPreview.transform.position = _targetPoint + Vector3.up * 0.2f; // Ligeramente elevado
                trapModelPreview.transform.rotation = Quaternion.identity; // O random rotation

                // Cambiar transparencia/color según validez
                if (trapModelPreview.TryGetComponent<Renderer>(out var modelRenderer)) {
                    UpdateRendererColor(modelRenderer);
                }
            }
        }

        public override Vector3 GetTargetPoint() => _targetPoint;

        public override Vector3 GetDirection() {
            // Para trampas, la dirección no es crítica (es un objeto estático)
            return (_targetPoint - transform.position).normalized;
        }

        public override bool IsValid() => _isValid;

        public override void Show() {
            base.Show();
            if (circlePrefab != null) circlePrefab.SetActive(true);
            if (trapModelPreview != null) trapModelPreview.SetActive(true);
        }

        public override void Hide() {
            base.Hide();
            if (circlePrefab != null) circlePrefab.SetActive(false);
            if (trapModelPreview != null) trapModelPreview.SetActive(false);
        }
    }
}

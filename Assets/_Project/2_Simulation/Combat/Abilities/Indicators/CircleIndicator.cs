using UnityEngine;
using Genesis.Data;

namespace Genesis.Simulation.Combat
{
    /// <summary>
    /// Indicador circular para habilidades AOE (ej: Meteorito, Sagrario, Torbellino)
    /// Puede ser ground-targeted (movible con mouse) o self-centered (fijo en el caster)
    /// </summary>
    public class CircleIndicator : AbilityIndicator
    {
        [Header("Circle Components")]
        [SerializeField] private GameObject circlePrefab; // Cylinder o Decal
        [SerializeField] private Renderer circleRenderer;

        [Header("Settings")]
        [SerializeField] private bool isSelfCentered = false; // True para Torbellino/Nova
        [SerializeField] private float maxDistance = 30f;     // Máxima distancia de placement

        private float _radius;
        private Vector3 _targetPoint;

        private static readonly Collider[] _enemyBuffer = new Collider[32];
        private int _enemyLayerMask;

        public override void Initialize(AbilityData abilityData)
        {
            _abilityData = abilityData;
            _radius = abilityData.Radius;
            maxDistance = abilityData.Range;

            _enemyLayerMask = LayerMask.GetMask("Enemy");

            // Detectar si es self-centered basado en TargetingMode
            isSelfCentered = (abilityData.TargetingMode == TargetType.Self);

            // Configurar escala del círculo (diametro en X/Z)
            if (circlePrefab != null)
            {
                float diameter = _radius * 2f;
                // Y no importa mucho (es un “disco”), pero no la pongas 0
                circlePrefab.transform.localScale = new Vector3(diameter, 1f, diameter);
            }

            _isValid = true;
        }

        public override void UpdatePosition(Vector3 worldPoint, Vector3 direction)
        {
            // === PROYECCIÓN A SUELO CENTRALIZADA ===
            if (isSelfCentered)
            {
                // Self-centered pero apoyado en el suelo bajo el jugador
                TryProjectToGround(transform.position, out _targetPoint);
            }
            else
            {
                // Apuntado con mouse, apoyado en el suelo bajo el mouse
                TryProjectToGround(worldPoint, out _targetPoint);
            }

            // === VISUAL SIEMPRE ===
            if (circlePrefab != null)
                circlePrefab.transform.position = _targetPoint + Vector3.up * groundProjectionOffset;

            // === VALIDACIÓN (throttleada) ===
            if (ShouldValidate())
            {
                float distance = Vector3.Distance(transform.position, _targetPoint);
                _isValid = distance <= maxDistance;
            }

            // === COLOR (sin instanciar materiales) ===
            if (circleRenderer != null)
                UpdateRendererColor(circleRenderer);
        }

        public override Vector3 GetTargetPoint() => _targetPoint;

        public override Vector3 GetDirection()
        {
            // Para círculos, la dirección es desde el caster hacia el target point
            Vector3 dir = _targetPoint - transform.position;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        }

        public override bool IsValid() => _isValid;

        public override void Show()
        {
            base.Show();
            if (circlePrefab != null)
                circlePrefab.SetActive(true);
        }

        public override void Hide()
        {
            base.Hide();
            if (circlePrefab != null)
                circlePrefab.SetActive(false);
        }

        /// <summary>
        /// Helper: Detecta cuántos enemigos están en el área (opcional, para UI feedback)
        /// </summary>
        private int GetEnemyCountInArea()
        {
            if (_radius <= 0f)
                return 0;

            int count = Physics.OverlapSphereNonAlloc(
                _targetPoint,
                _radius,
                _enemyBuffer,
                _enemyLayerMask,
                QueryTriggerInteraction.Ignore
            );

            return count;
        }
    }
}

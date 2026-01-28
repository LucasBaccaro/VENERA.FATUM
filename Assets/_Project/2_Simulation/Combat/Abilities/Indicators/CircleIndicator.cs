using UnityEngine;
using Genesis.Data;
using UnityEngine.Rendering.Universal;

namespace Genesis.Simulation.Combat
{
    /// <summary>
    /// Indicador circular para habilidades AOE (ej: Meteorito, Sagrario, Torbellino)
    /// Puede ser ground-targeted (movible con mouse) o self-centered (fijo en el caster)
    /// Versión decal: proyecta una textura circular en el suelo adaptándose al terreno.
    /// </summary>
    public class CircleIndicator : AbilityIndicator
    {
        [Header("Decal Components")]
        [SerializeField] private DecalProjector decal;
        [SerializeField] private float projectionDepth = 6f; // Profundidad de proyección del decal

        [Header("Settings")]
        [SerializeField] private bool isSelfCentered = false; // True para Torbellino/Nova
        [SerializeField] private float maxDistance = 30f;     // Máxima distancia de placement
        [Tooltip("Radio con el que fue diseñado el prefab (escala 1). Permite que el código escale el transform proporcionalmente.")]
        [SerializeField] private float baseRadius = 2.5f;
        [Tooltip("Altura del volumen de proyección del decal.")]
        [SerializeField] private float decalHeight = 3f;

        private float _radius;
        private Vector3 _targetPoint;

        private static readonly Collider[] _enemyBuffer = new Collider[32];
        private int _enemyLayerMask;

        public override void Initialize(AbilityData abilityData)
        {
            this.transform.localScale = Vector3.one; // FIX: Reset root scale
            _abilityData = abilityData;
            _radius = abilityData.Radius;
            maxDistance = abilityData.Range;

            _enemyLayerMask = LayerMask.GetMask("Enemy");

            // Detectar si es self-centered basado en TargetingMode
            isSelfCentered = (abilityData.TargetingMode == TargetType.Self);

            // Buscar DecalProjector si no está asignado
            if (decal == null)
                decal = GetComponentInChildren<DecalProjector>(true);

            // Configurar DecalProjector y Transform
            if (decal != null)
            {
                // Material personalizado por habilidad (si existe)
                if (abilityData.IndicatorMaterial != null)
                    decal.material = abilityData.IndicatorMaterial;

                // 1. FORZAR MODO DE ESCALADO (Vital para consistencia entre prefabs)
                decal.scaleMode = DecalScaleMode.InheritFromHierarchy;

                // 2. ESCALAR EL TRANSFORM
                this.transform.localScale = Vector3.one; // Limpiar antes de aplicar
                float scaleMultiplier = _radius / baseRadius;
                this.transform.localScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);

                // 3. CONFIGURAR TAMAÑO BASE
                float baseDiameter = baseRadius * 2f;
                decal.size = new Vector3(baseDiameter, baseDiameter, projectionDepth);
                decal.pivot = Vector3.zero;
                decal.transform.localScale = Vector3.one;
            }

            _isValid = true;
        }

        public override void UpdatePosition(Vector3 worldPoint, Vector3 direction)
        {
            // === PROYECCIÓN A SUELO ===
            if (isSelfCentered)
            {
                // Self-centered: usar posición del caster
                TryProjectToGround(transform.position, out _targetPoint);
            }
            else
            {
                // Ground-targeted: usar posición del mouse
                TryProjectToGround(worldPoint, out _targetPoint);
            }

            // === ACTUALIZAR DECAL PROJECTOR ===
            if (decal != null)
            {
                // Posición: levantar el projector arriba del suelo para que atraviese el terreno
                float yLift = Mathf.Max(0.25f, decalHeight * 0.5f);
                decal.transform.position = _targetPoint + Vector3.up * yLift;

                // Rotación: proyectar hacia abajo (90° en X)
                decal.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                // Color del decal según validez (si el material lo soporta)
                UpdateDecalColor();
            }

            // === VALIDACIÓN (throttleada) ===
            if (ShouldValidate())
            {
                // Self-centered abilities siempre son válidas
                if (isSelfCentered)
                {
                    _isValid = true;
                }
                else
                {
                    // Ground-targeted: validar distancia al target
                    float distance = Vector3.Distance(transform.position, _targetPoint);
                    _isValid = distance <= maxDistance;
                }
            }
        }

        /// <summary>
        /// Actualiza el color del decal según validez
        /// </summary>
        private void UpdateDecalColor()
        {
            if (decal == null || decal.material == null) return;

            // Usar el sistema de colores del parent (validColor/invalidColor)
            Color targetColor = _isValid ? validColor : invalidColor;

            // Intentar setear el color en el material del decal
            // Nota: Esto depende de qué propiedad use tu shader
            if (decal.material.HasProperty("_BaseColor"))
                decal.material.SetColor("_BaseColor", targetColor);
            else if (decal.material.HasProperty("_Color"))
                decal.material.SetColor("_Color", targetColor);
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
            if (decal != null)
                decal.enabled = true;
        }

        public override void Hide()
        {
            base.Hide();
            if (decal != null)
                decal.enabled = false;
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

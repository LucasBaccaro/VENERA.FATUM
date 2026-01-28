using UnityEngine;
using Genesis.Data;
using UnityEngine.Rendering.Universal;

namespace Genesis.Simulation.Combat
{
    /// <summary>
    /// Indicador cónico para habilidades de área frontal (ej: Multidisparo / Burning Hands)
    /// Versión decal: proyecta una textura en el suelo en vez de generar un mesh procedural.
    /// </summary>
    public class ConeIndicator : AbilityIndicator
    {

	

        [Header("Decal Components")]
        [SerializeField] private DecalProjector decal;
        [SerializeField] private float projectionDepth = 6f;

        [Header("Settings")]
        [Tooltip("Rango con el que fue diseñado el prefab (escala 1).")]
        [SerializeField] private float baseRange = 5f;
        [Tooltip("Altura del volumen de proyección del decal.")]
        [SerializeField] private float decalHeight = 3f;

        [Header("Runtime")]
        private float _range;
        private float _angle; // Ángulo total del cono (60°, 90°, etc)
        private Vector3 _direction;
        private Vector3 _originPoint;

        // Enemy check (opcional) sin allocs
        private static readonly Collider[] _enemyBuffer = new Collider[32];
        private int _enemyLayerMask;

        // Cono: SIEMPRE nace en el caster (player)
        private bool _isSelfCentered = true;

        public override void Initialize(AbilityData abilityData)
        {
            this.transform.localScale = Vector3.one; // FIX: Reset root scale
            _abilityData = abilityData;
            _range = abilityData.Range;
            _angle = abilityData.Angle;

            _enemyLayerMask = LayerMask.GetMask("Enemy");

            if (decal == null)
                decal = GetComponentInChildren<DecalProjector>(true);

            if (decal != null)
            {
                // Material por habilidad
                if (abilityData.IndicatorMaterial != null)
                    decal.material = abilityData.IndicatorMaterial;

                // 1. FORZAR MODO DE ESCALADO
                decal.scaleMode = DecalScaleMode.InheritFromHierarchy;

                // 2. ESCALAR EL TRANSFORM
                this.transform.localScale = Vector3.one;
                float scaleMultiplier = _range / baseRange;
                this.transform.localScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);

                // 3. CONFIGURAR TAMAÑO BASE (Basado en baseRange)
                float halfAngleRad = _angle * 0.5f * Mathf.Deg2Rad;
                float baseConeWidth = 2f * Mathf.Tan(halfAngleRad) * baseRange;

                // Invertimos Width/Height según el setup observado anteriormente (90° en X)
                decal.size = new Vector3(baseConeWidth, baseRange, decalHeight);

                // Pivot: queremos que el volumen "arranque" en el caster y vaya hacia adelante
                decal.pivot = new Vector3(0f, baseRange * 0.5f, 0);
                
                Vector3 decalSize = decal.size;
                decalSize.z = projectionDepth;
                decal.size = decalSize;

                // Asegurar que el objeto del decal no tenga escala propia que interfiera
                decal.transform.localScale = Vector3.one;
            }

            _isValid = true;
        }

        public override void UpdatePosition(Vector3 worldPoint, Vector3 direction)
        {
            // Dirección (del mouse): solo orienta el cono
            _direction = direction;
            _direction.y = 0f;

            if (_direction.sqrMagnitude < 0.0001f)
                _direction = transform.forward;
            else
                _direction.Normalize();

            // ORIGEN: siempre caster (este script vive en Indicator_Cone, hijo del player)
            TryProjectToGround(transform.position, out _originPoint);

            if (decal != null)
            {
                // Posición: levantar el projector arriba del piso para que "atraviese" el suelo
                float yLift = Mathf.Max(0.25f, decalHeight * 0.5f);
                decal.transform.position = _originPoint + Vector3.up * yLift;

                // Rotación:
                // 1) Yaw = apuntar hacia adelante según mouse
                // 2) Pitch = 90° para que el projector proyecte hacia abajo (-Y)
                Quaternion yaw = Quaternion.LookRotation(_direction, Vector3.up);
                Quaternion pitchDown = Quaternion.Euler(90f, 0f, 0f); // si tu caso invierte, probá -90

                decal.transform.rotation = yaw * pitchDown;

                // Si sigue "cruzado", tu descubrimiento de swap se aplica acá también:
                // Cuando X=90, Unity puede interpretar Width/Height al revés visualmente.
                // En ese caso, swap real:
                // decal.size = new Vector3(decal.size.x, decal.size.z, decal.size.y);
                // (Pero primero probá solo con la rotación correcta.)
            }
            else
            {
                // Fallback si no hay projector (no debería pasar)
                transform.position = _originPoint + Vector3.up * groundProjectionOffset;
                transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
            }

            if (ShouldValidate())
                _isValid = true;
        }

        public override Vector3 GetTargetPoint()
        {
            // Centro del borde externo del cono
            return _originPoint + _direction * _range;
        }

        public override Vector3 GetDirection() => _direction;
        public override bool IsValid() => _isValid;

        public override void Show()
        {
            base.Show();
            if (decal != null) decal.enabled = true;
        }

        public override void Hide()
        {
            base.Hide();
            if (decal != null) decal.enabled = false;
        }

        public int GetEnemyCountInCone()
        {
            Vector3 casterPos = _originPoint;
            float halfAngle = _angle * 0.5f;

            int hitCount = Physics.OverlapSphereNonAlloc(
                casterPos,
                _range,
                _enemyBuffer,
                _enemyLayerMask,
                QueryTriggerInteraction.Ignore
            );

            int count = 0;
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _enemyBuffer[i];
                if (col == null) continue;

                Vector3 toTarget = col.transform.position - casterPos;
                toTarget.y = 0f;

                if (toTarget.sqrMagnitude < 0.0001f)
                    continue;

                float angleToTarget = Vector3.Angle(_direction, toTarget.normalized);
                if (angleToTarget <= halfAngle)
                    count++;
            }

            return count;
        }
    }
}

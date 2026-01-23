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
	[SerializeField] private float projectionDepth = 6f; // lo que querés (6)

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

                // ANCHO geométrico del cono al final
                float halfAngleRad = _angle * 0.5f * Mathf.Deg2Rad;
                float coneWidth = 2f * Mathf.Tan(halfAngleRad) * _range;

                // IMPORTANTE:
                // Con el projector inclinado 90° en X (hacia abajo), en tu setup
                // se ve correcto cuando "Width" y "Height" están invertidos.
                // Por eso seteamos:
                // - size.x = coneWidth (ancho del cono)
                // - size.y = decalHeight (altura del volumen)
                // - size.z = range (largo)
                //
                // Pero como comprobaste que Unity lo interpreta al revés al rotarlo,
                // hacemos el SWAP que te lo arregla:
                decal.size = new Vector3(coneWidth, _range, decalHeight);


                // Pivot: queremos que el volumen "arranque" en el caster y vaya hacia adelante
                decal.pivot = new Vector3(0f, _range * 0.5f, 0);
		Vector3 size = decal.size;
    		size.z = projectionDepth;
    		decal.size = size;
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

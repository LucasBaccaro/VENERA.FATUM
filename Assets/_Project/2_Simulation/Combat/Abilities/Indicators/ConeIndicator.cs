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

        [Tooltip("Altura del volumen de proyección del decal (eje Y del projector).")]
        [SerializeField] private float decalHeight = 3f;

        [Tooltip("Offset mínimo para evitar z-fighting con el suelo.")]
        [SerializeField] private float groundYOffset = 0.05f;

        [Header("Cone Runtime")]
        private float _range;
        private float _angle;      // Ángulo total del cono (60°, 90°, etc)
        private Vector3 _direction;

        public override void Initialize(AbilityData abilityData)
        {
            _abilityData = abilityData;
            _range = abilityData.Range;
            _angle = abilityData.Angle;

            // Material por habilidad (si lo definiste en AbilityData)
            // Nota: el nombre exacto del campo depende de cómo lo agregaste:
            // - si lo llamaste IndicatorMaterialOverride, dejalo así
            // - si lo llamaste IndicatorMaterial, cambia aquí.
            // Empujar el projector hacia adelante para que el decal arranque en el caster
if (decal != null) {
    decal.pivot = new Vector3(0f, 0f, _range * 0.5f);
}


if (decal != null && abilityData.IndicatorMaterial != null)
    decal.material = abilityData.IndicatorMaterial;

             float halfRad = (_angle * 0.5f) * Mathf.Deg2Rad;
    float width = 2f * Mathf.Tan(halfRad) * _range;

    decal.size = new Vector3(width, decalHeight, _range);

    // Hace que el cono nazca desde el caster (no centrado)
    decal.pivot = new Vector3(0f, 0f, _range * 0.5f);


            _isValid = true;
        }

        public override void UpdatePosition(Vector3 worldPoint, Vector3 direction)
        {
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            _isValid = true;

            // Orientación: forward del cone apunta a la dirección del skill
            transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);

            // Pegarlo a la altura del suelo del punto apuntado (solo en Y)
            Vector3 p = transform.position;
            p.y = worldPoint.y + groundYOffset;
            transform.position = p;
        }

        public override Vector3 GetTargetPoint()
        {
            // Para conos, el “target point” puede ser el centro del borde externo del cono
            return transform.position + _direction * _range;
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

        /// <summary>
        /// Helper: Detecta cuántos enemigos están en el cono (opcional)
        /// </summary>
        public int GetEnemyCountInCone()
        {
            Vector3 casterPos = transform.position;
            float halfAngle = _angle / 2f;

            Collider[] hits = Physics.OverlapSphere(casterPos, _range, LayerMask.GetMask("Enemy"));

            int count = 0;
            foreach (var hit in hits)
            {
                Vector3 dirToTarget = (hit.transform.position - casterPos).normalized;
                float angleToTarget = Vector3.Angle(_direction, dirToTarget);

                if (angleToTarget <= halfAngle)
                    count++;
            }

            return count;
        }
    }
}

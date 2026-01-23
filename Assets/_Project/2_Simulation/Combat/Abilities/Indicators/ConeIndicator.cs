using UnityEngine;
using Genesis.Data;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Indicador cónico para habilidades de área frontal (ej: Multidisparo)
    /// Muestra un cono/abanico desde el caster hacia la dirección del mouse
    /// </summary>
    public class ConeIndicator : AbilityIndicator {

        [Header("Cone Components")]
        [SerializeField] private MeshFilter coneMeshFilter;
        [SerializeField] private MeshRenderer coneRenderer;

        [Header("Cone Settings")]
        [SerializeField] private int segments = 20; // Resolución del cono

        private float _range;
        private float _angle; // Ángulo total del cono (60°, 90°, etc)
        private Vector3 _direction;
        private Mesh _coneMesh;

        public override void Initialize(AbilityData abilityData) {
            _abilityData = abilityData;
            _range = abilityData.Range;
            _angle = abilityData.Angle;

            // Generar mesh del cono
            GenerateConeMesh();

            _isValid = true;
        }

        public override void UpdatePosition(Vector3 worldPoint, Vector3 direction) {
            _direction = direction.normalized;

            // El cono siempre es válido (no se puede obstruir, es área instantánea)
            _isValid = true;

            // Orientar el cono hacia la dirección
            transform.rotation = Quaternion.LookRotation(_direction);

            // Actualizar color
            if (coneRenderer != null) {
                UpdateRendererColor(coneRenderer);
            }
        }

        public override Vector3 GetTargetPoint() {
            // Para conos, el target point es el centro del extremo del cono
            return transform.position + _direction * _range;
        }

        public override Vector3 GetDirection() => _direction;

        public override bool IsValid() => _isValid;

        /// <summary>
        /// Genera el mesh del cono proceduralmente
        /// </summary>
        private void GenerateConeMesh() {
            _coneMesh = new Mesh();
            _coneMesh.name = "ConeMesh";

            float halfAngle = _angle / 2f;

            // Vertices
            Vector3[] vertices = new Vector3[segments + 2];
            vertices[0] = Vector3.zero; // Origen (en el caster)

            for (int i = 0; i <= segments; i++) {
                float currentAngle = -halfAngle + (i * _angle / segments);
                float radians = currentAngle * Mathf.Deg2Rad;

                // Calcular posición en el perímetro del cono
                float x = Mathf.Sin(radians) * _range;
                float z = Mathf.Cos(radians) * _range;

                vertices[i + 1] = new Vector3(x, 0, z);
            }

            // Triángulos
            int[] triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++) {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            // UVs (opcional)
            Vector2[] uvs = new Vector2[vertices.Length];
            for (int i = 0; i < uvs.Length; i++) {
                uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
            }

            _coneMesh.vertices = vertices;
            _coneMesh.triangles = triangles;
            _coneMesh.uv = uvs;
            _coneMesh.RecalculateNormals();

            if (coneMeshFilter != null) {
                coneMeshFilter.mesh = _coneMesh;
            }
        }

        public override void Show() {
            base.Show();
            if (coneRenderer != null) coneRenderer.enabled = true;
        }

        public override void Hide() {
            base.Hide();
            if (coneRenderer != null) coneRenderer.enabled = false;
        }

        /// <summary>
        /// Helper: Detecta cuántos enemigos están en el cono (opcional)
        /// </summary>
        public int GetEnemyCountInCone() {
            Vector3 casterPos = transform.position;
            float halfAngle = _angle / 2f;

            // Detectar todos los enemigos en esfera
            Collider[] hits = Physics.OverlapSphere(casterPos, _range, LayerMask.GetMask("Enemy"));

            int count = 0;
            foreach (var hit in hits) {
                Vector3 dirToTarget = (hit.transform.position - casterPos).normalized;
                float angleToTarget = Vector3.Angle(_direction, dirToTarget);

                if (angleToTarget <= halfAngle) {
                    count++;
                }
            }

            return count;
        }
    }
}

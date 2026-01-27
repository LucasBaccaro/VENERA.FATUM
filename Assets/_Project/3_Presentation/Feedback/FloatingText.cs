using UnityEngine;
using TMPro;
using Genesis.Data;

namespace Genesis.Presentation.Feedback {
    /// <summary>
    /// Componente que gestiona la animación y el billboarding de un número de daño.
    /// </summary>
    public class FloatingText : MonoBehaviour {
        [SerializeField] private TextMeshPro textMesh;
        private FloatingTextConfig _config;
        private float _elapsedTime;
        private Vector3 _startPos;
        private Vector3 _randomOffset;
        private Vector3 _initialVelocity;
        private bool _isInitialized;

        public void Initialize(string text, Color color, FloatingTextConfig config, bool isCritical = false) {
            _config = config;
            textMesh.text = text;
            textMesh.color = color;
            
            // Forzar centrado técnico
            textMesh.alignment = TextAlignmentOptions.Center;

            if (config.fontAsset != null) {
                textMesh.font = config.fontAsset;
            }
            
            if (isCritical) {
                textMesh.fontSize = config.fontSize * config.criticalScaleMultiplier;
                textMesh.fontStyle = FontStyles.Bold;
            } else {
                textMesh.fontSize = config.fontSize;
                textMesh.fontStyle = FontStyles.Normal;
            }

            // Configurar Sorting para que esté por encima de partículas
            var renderer = textMesh.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.sortingLayerName = config.sortingLayerName;
                renderer.sortingOrder = config.sortingOrder;
            }

            _startPos = transform.position;
            _randomOffset = new Vector3(
                Random.Range(-config.randomOffsetRange.x, config.randomOffsetRange.y), // Usamos Y para un poco de spread vertical
                Random.Range(-config.randomOffsetRange.y, config.randomOffsetRange.y),
                0
            );

            // Inicializar velocidad inicial para modo arco
            if (config.animationMode == FCTAnimationMode.Arc) {
                float side = Random.value > 0.5f ? 1f : -1f;
                float hVel = Random.Range(config.horizontalVelocityRange.x, config.horizontalVelocityRange.y) * side;
                _initialVelocity = new Vector3(hVel, config.upwardForce, 0);
            }

            _elapsedTime = 0;
            _isInitialized = true;
            
            // Aplicar el estado inicial inmediatamente
            ApplyState(0);
            
            gameObject.SetActive(true);
        }

        void Update() {
            if (!_isInitialized || _config == null) return;

            _elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsedTime / _config.duration);

            ApplyState(t);

            if (t >= 1.0f) {
                Destroy(gameObject);
            }
        }

        private void ApplyState(float t) {
            float elapsed = t * _config.duration;

            // 1. Billboarding (Mirar a la cámara principal)
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            if (cam != null) {
                transform.rotation = cam.rotation;
            }

            // 2. Animación Determinista (P = P0 + V0*t + 0.5*g*t^2)
            if (_config.animationMode == FCTAnimationMode.Vertical) {
                transform.position = _startPos + _randomOffset + (Vector3.up * _config.floatSpeed * elapsed);
            } else {
                // Ecuación de movimiento parabólico
                Vector3 displacement = (_initialVelocity * elapsed) + (0.5f * Vector3.down * _config.gravity * elapsed * elapsed);
                transform.position = _startPos + _randomOffset + displacement;
            }
            
            // 3. Escala y Alpha mediante curvas
            float scale = _config.scaleCurve.Evaluate(t);
            transform.localScale = Vector3.one * scale;

            Color c = textMesh.color;
            c.a = _config.alphaCurve.Evaluate(t);
            textMesh.color = c;
            
            // Debug visual en la Scene View
            Debug.DrawLine(transform.position, transform.position + Vector3.up * 0.1f, Color.yellow);
        }
    }
}

using UnityEngine;
using FishNet.Object;
using UnityEngine.Rendering.Universal;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Indicador de warning para AOE que se muestra durante el delay antes del impacto.
    /// Es spawneado en red y visible para todos los jugadores.
    /// Se autodestruye después del delay configurado.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class AOEWarningIndicator : NetworkBehaviour {

        [Header("Decal Components")]
        [SerializeField] private DecalProjector decal;
        [SerializeField] private float projectionDepth = 6f;
        [SerializeField] private float decalHeight = 3f;

        [Header("Visual Settings")]
        [SerializeField] private Color warningColor = new Color(.7f, 0f, 0f, 1f); // Rojo semi-transparente

        private float _destroyTime = float.MaxValue; // Inicializar con valor alto para evitar destrucción prematura
        private bool _isConfigured = false;

        /// <summary>
        /// Inicializa el warning indicator con los parámetros del AOE
        /// Solo llamar desde el servidor
        /// </summary>
        [Server]
        public void Initialize(Vector3 position, float radius, float duration) {
            // La posición ya está configurada al spawnearse
            // Sincronizar configuración a todos los clientes
            RpcConfigureIndicator(position, radius, duration);
        }

        /// <summary>
        /// Configura el indicador en todos los clientes
        /// </summary>
        [ObserversRpc]
        private void RpcConfigureIndicator(Vector3 position, float radius, float duration) {
            // Buscar DecalProjector si no está asignado
            if (decal == null)
                decal = GetComponentInChildren<DecalProjector>(true);

            // Configurar posición (la rotación ya viene correcta del spawn)
            transform.position = position + Vector3.up * Mathf.Max(0.25f, decalHeight * 0.5f);
            // NO setear rotación aquí - ya viene correcta del Instantiate para evitar glitch visual

            // Configurar tamaño del decal
            if (decal != null) {
                float diameter = radius * 2f;
                decal.size = new Vector3(diameter, diameter, projectionDepth);
                decal.pivot = Vector3.zero;
                decal.enabled = true;

                // Configurar color de warning
                if (decal.material != null) {
                    if (decal.material.HasProperty("_BaseColor"))
                        decal.material.SetColor("_BaseColor", warningColor);
                    else if (decal.material.HasProperty("_Color"))
                        decal.material.SetColor("_Color", warningColor);
                }

                Debug.Log($"[AOEWarningIndicator] Configured decal at {position} with radius {radius} for {duration}s");
            } else {
                Debug.LogError("[AOEWarningIndicator] DecalProjector is NULL! Cannot display warning.");
            }

            // Programar destrucción
            _destroyTime = Time.time + duration;
            _isConfigured = true;
        }

        public override void OnStartServer() {
            base.OnStartServer();
            // El servidor verificará el timeout
        }

        [Server]
        private void Update() {
            // Solo verificar destrucción si ya está configurado
            if (!_isConfigured) return;

            // Autodestrucción cuando pase el tiempo
            if (Time.time >= _destroyTime) {
                if (base.IsSpawned) {
                    FishNet.InstanceFinder.ServerManager.Despawn(gameObject);
                }
            }
        }
    }
}

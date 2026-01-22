using UnityEngine;
using Genesis.Core;

namespace Genesis.Bootstrap {

    /// <summary>
    /// Punto de entrada principal del juego.
    /// Se ejecuta en la escena Bootstrap y se encarga de inicializar todos los managers.
    /// </summary>
    public class EntryPoint : MonoBehaviour {

        [Header("Initialization Order")]
        [SerializeField] private bool verboseLogging = true;

        void Awake() {
            Log("=== GENESIS - Entry Point ===");

            // Configurar Application settings
            ConfigureApplication();

            // Inicializar ServiceLocator (ya está listo, solo log)
            Log("ServiceLocator initialized");

            // Inicializar EventBus (ya está listo, solo log)
            Log("EventBus initialized");
        }

        void Start() {
            Log("Game initialization complete. Ready to connect.");
        }

        // ═══════════════════════════════════════════════════════
        // APPLICATION CONFIGURATION
        // ═══════════════════════════════════════════════════════

        private void ConfigureApplication() {
            // Target framerate (cliente)
            Application.targetFrameRate = -1; // Sin límite, usa V-Sync

            // Physics
            Physics.defaultContactOffset = 0.01f;
            Physics.queriesHitTriggers = false; // CRÍTICO para performance

            Log("Application configured");
        }

        // ═══════════════════════════════════════════════════════
        // LOGGING
        // ═══════════════════════════════════════════════════════

        private void Log(string message) {
            if (verboseLogging) {
                Debug.Log($"[EntryPoint] {message}");
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Show Registered Services")]
        private void DebugShowServices() {
            ServiceLocator.Instance.LogRegisteredServices();
        }

        [ContextMenu("Show Registered Events")]
        private void DebugShowEvents() {
            EventBus.LogRegisteredEvents();
        }
#endif
    }
}

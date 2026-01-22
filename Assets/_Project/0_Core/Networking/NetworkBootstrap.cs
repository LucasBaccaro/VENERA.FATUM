using UnityEngine;
using FishNet;
using FishNet.Managing;
using Genesis.Core;

namespace Genesis.Core.Networking {

    /// <summary>
    /// Wrapper para inicializar y configurar FishNet NetworkManager.
    /// Maneja la conexión inicial como cliente o servidor.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour {

        [Header("Network Settings")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Auto Start (For Testing)")]
        [SerializeField] private bool autoStartServer = false;
        [SerializeField] private bool autoStartClient = false;

        void Start() {
            if (networkManager == null) {
                networkManager = InstanceFinder.NetworkManager;
            }

            if (networkManager == null) {
                Debug.LogError("[NetworkBootstrap] NetworkManager no encontrado en la escena!");
                return;
            }

            // Registrar en ServiceLocator
            ServiceLocator.Instance.Register(networkManager);

            // Auto-start para testing
#if UNITY_EDITOR
            if (autoStartServer) {
                StartServer();
            }
            
            // Si también queremos cliente (Host mode), lo iniciamos después del servidor
            if (autoStartClient) {
                StartClient();
            }
#endif

            Debug.Log("[NetworkBootstrap] Initialized. Waiting for connection...");
        }

        // ═══════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════

        public void StartServer() {
            if (networkManager.ServerManager.StartConnection()) {
                Debug.Log("[NetworkBootstrap] Server started successfully");
                EventBus.Trigger("OnServerStarted");
            } else {
                Debug.LogError("[NetworkBootstrap] Failed to start server");
            }
        }

        public void StartClient() {
            if (networkManager.ClientManager.StartConnection()) {
                Debug.Log("[NetworkBootstrap] Client connecting...");
                EventBus.Trigger("OnClientConnecting");
            } else {
                Debug.LogError("[NetworkBootstrap] Failed to start client");
            }
        }

        public void StartHost() {
            StartServer();
            StartClient();
        }

        public void StopConnection() {
            if (networkManager.ServerManager.Started) {
                networkManager.ServerManager.StopConnection(true);
                Debug.Log("[NetworkBootstrap] Server stopped");
            }

            if (networkManager.ClientManager.Started) {
                networkManager.ClientManager.StopConnection();
                Debug.Log("[NetworkBootstrap] Client disconnected");
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Start Server")]
        private void DebugStartServer() => StartServer();

        [ContextMenu("Start Client")]
        private void DebugStartClient() => StartClient();

        [ContextMenu("Start Host")]
        private void DebugStartHost() => StartHost();

        [ContextMenu("Stop All")]
        private void DebugStopAll() => StopConnection();
#endif
    }
}

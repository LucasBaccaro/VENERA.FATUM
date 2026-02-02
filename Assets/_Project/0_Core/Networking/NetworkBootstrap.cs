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

            // Leer argumentos de línea de comando
            string[] args = System.Environment.GetCommandLineArgs();
            bool isServerMode = System.Array.Exists(args, arg => arg.ToLower() == "-server");
            bool isClientMode = System.Array.Exists(args, arg => arg.ToLower() == "-client");

            // Buscar override de dirección del servidor: -address=IP
            string serverAddress = null;
            foreach (string arg in args) {
                if (arg.ToLower().StartsWith("-address=")) {
                    serverAddress = arg.Substring(9); // Extraer IP después de "-address="
                    break;
                }
            }

#if UNITY_EDITOR
            // En Editor: Host mode para desarrollo
            // Si se inicia el servidor Y el cliente (modo Host), conectar cliente a localhost
            if (autoStartServer && autoStartClient) {
                // Override client address a localhost para modo Host
                networkManager.TransportManager.Transport.SetClientAddress("127.0.0.1");
                Debug.Log("[NetworkBootstrap] Host mode - Client connecting to localhost");
            }

            if (autoStartServer) {
                StartServer();
            }

            if (autoStartClient) {
                StartClient();
            }
#else
            // En Build: detectar modo y configurar address
            if (isServerMode) {
                Debug.Log("[NetworkBootstrap] Starting as DEDICATED SERVER");
                StartServer();
            } else {
                // Modo cliente: usar address override si existe
                if (!string.IsNullOrEmpty(serverAddress)) {
                    networkManager.TransportManager.Transport.SetClientAddress(serverAddress);
                    Debug.Log($"[NetworkBootstrap] Client address set to: {serverAddress}");
                }

                Debug.Log("[NetworkBootstrap] Starting as CLIENT");
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

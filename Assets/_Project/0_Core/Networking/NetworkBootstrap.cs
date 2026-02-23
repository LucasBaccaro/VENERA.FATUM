using UnityEngine;
using FishNet;
using FishNet.Managing;
using Genesis.Core;

namespace Genesis.Core.Networking {

    /// <summary>
    /// Wrapper para inicializar y configurar FishNet NetworkManager.
    /// Maneja la conexión inicial como cliente o servidor.
    /// When loaded additively from Login scene, waits for LoginSceneController to call Start methods.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour {

        [Header("Network Settings")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Auto Start (For Testing)")]
        [SerializeField] private bool autoStartServer = false;
        [SerializeField] private bool autoStartClient = false;

        [Header("Login")]
        [SerializeField] private bool waitForLogin = false;

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

            // If login is required (Login scene controls connection), just pre-warm server in editor.
            // LoginSceneController will call StartClientLocal()/StartClient() after auth.
            if ((waitForLogin || LoginData.LoginRequired) && !isServerMode) {
#if UNITY_EDITOR
                bool isClone = System.IO.File.Exists(
                    System.IO.Path.Combine(Application.dataPath.Replace("/Assets", ""), ".clone"));
                if (!isClone) {
                    networkManager.TransportManager.Transport.SetClientAddress("127.0.0.1");
                    Debug.Log("[NetworkBootstrap] Pre-warming server before login...");
                    StartServer();
                }
#endif
                Debug.Log("[NetworkBootstrap] Waiting for login (client will connect after auth)...");
                return;
            }

            // Buscar override de dirección del servidor: -address=IP
            string serverAddress = null;
            foreach (string arg in args) {
                if (arg.ToLower().StartsWith("-address=")) {
                    serverAddress = arg.Substring(9); // Extraer IP después de "-address="
                    break;
                }
            }

#if UNITY_EDITOR
            // Detectar si es un clon de ParrelSync (checa el archivo .clone en raíz del proyecto)
            bool isParrelSyncClone = System.IO.File.Exists(
                System.IO.Path.Combine(Application.dataPath.Replace("/Assets", ""), ".clone"));

            if (isParrelSyncClone) {
                // Clon de ParrelSync: solo cliente, conectar al host en localhost
                networkManager.TransportManager.Transport.SetClientAddress("127.0.0.1");
                Debug.Log("[NetworkBootstrap] ParrelSync clone detected - starting as CLIENT only");
                StartClient();
            } else {
                // Editor original: Host mode para desarrollo
                if (autoStartServer && autoStartClient) {
                    networkManager.TransportManager.Transport.SetClientAddress("127.0.0.1");
                    Debug.Log("[NetworkBootstrap] Host mode - Client connecting to localhost");
                }

                if (autoStartServer) {
                    StartServer();
                }

                if (autoStartClient) {
                    StartClient();
                }
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

        public void StartClientLocal() {
            networkManager.TransportManager.Transport.SetClientAddress("127.0.0.1");
            StartClient();
        }

        public void StartHost() {
            networkManager.TransportManager.Transport.SetClientAddress("127.0.0.1");
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

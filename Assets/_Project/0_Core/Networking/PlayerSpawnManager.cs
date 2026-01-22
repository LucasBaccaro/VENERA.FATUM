using UnityEngine;
using FishNet;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Transporting;
using System.Collections;

namespace Genesis.Core.Networking {

    public class PlayerSpawnManager : MonoBehaviour {

        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform[] spawnPoints;

        private NetworkManager _networkManager;

        void Start() {
            StartCoroutine(InitSequence());
        }

        private IEnumerator InitSequence() {
            // Esperar hasta que FishNet se inicialice
            while (InstanceFinder.NetworkManager == null) {
                yield return null;
            }

            _networkManager = InstanceFinder.NetworkManager;
            Debug.Log($"[SpawnManager] NetworkManager found. Server Active: {_networkManager.ServerManager.Started}");

            // Suscribirse
            _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            _networkManager.ServerManager.OnServerConnectionState += OnServerStarted;

            // Si ya arrancó, forzar chequeo tras un delay para asegurar que el cliente local esté listo
            if (_networkManager.ServerManager.Started) {
                yield return new WaitForSeconds(0.5f); // Delay de seguridad para Host
                CheckExistingClients("Start Late Check");
            }
        }

        void OnDestroy() {
            if (_networkManager != null) {
                _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
                _networkManager.ServerManager.OnServerConnectionState -= OnServerStarted;
            }
        }

        private void OnServerStarted(ServerConnectionStateArgs args) {
            if (args.ConnectionState == LocalConnectionState.Started) {
                Debug.Log("[SpawnManager] Server Started Event.");
                // No llamamos CheckExistingClients aquí inmediatamente para el Host,
                // dejamos que OnRemoteConnectionState lo maneje o el Start delay.
            }
        }

        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args) {
            Debug.Log($"[SpawnManager] Conn Event: {conn.ClientId} -> {args.ConnectionState}");
            if (args.ConnectionState == RemoteConnectionState.Started) {
                StartCoroutine(SpawnWithDelay(conn));
            }
        }

        private IEnumerator SpawnWithDelay(NetworkConnection conn) {
            // Esperar un frame para asegurar que la conexión esté totalmente establecida
            yield return null; 
            TrySpawnPlayer(conn);
        }

        private void CheckExistingClients(string source) {
            Debug.Log($"[SpawnManager] Checking clients from: {source}. Count: {_networkManager.ServerManager.Clients.Count}");
            foreach (var conn in _networkManager.ServerManager.Clients.Values) {
                TrySpawnPlayer(conn);
            }
        }

        private void TrySpawnPlayer(NetworkConnection conn) {
            if (conn.FirstObject != null) {
                Debug.Log($"[SpawnManager] Client {conn.ClientId} already has player.");
                return;
            }

            if (playerPrefab == null) {
                Debug.LogError("[SpawnManager] NO PREFAB!");
                return;
            }

            Vector3 pos = (spawnPoints != null && spawnPoints.Length > 0) ? spawnPoints[0].position : Vector3.zero;
            
            GameObject player = Instantiate(playerPrefab, pos, Quaternion.identity);
            
            // FishNet Spawn
            _networkManager.ServerManager.Spawn(player, conn);
            
            Debug.Log($"[SpawnManager] 🟢 SPAWN COMMAND SENT for Client {conn.ClientId}");
        }
    }
}

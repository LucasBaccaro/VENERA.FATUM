using UnityEngine;
using FishNet;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using System.Collections;
using Genesis.Core;
using Genesis.Core.Persistence;

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
            } else if (args.ConnectionState == RemoteConnectionState.Stopped) {
                SavePlayerOnDisconnect(conn);
            }
        }

        /// <summary>
        /// Saves player data and cleans up when a remote client disconnects.
        /// FishNet fires OnRemoteConnectionState BEFORE despawning objects,
        /// so conn.FirstObject and sessions are still valid here.
        /// Save MUST happen before cleanup (UnregisterConnection removes the session).
        /// </summary>
        private void SavePlayerOnDisconnect(NetworkConnection conn) {
            Debug.Log($"[SpawnManager] ═══ DISCONNECT SAVE ═══ clientId={conn.ClientId}");

            // Party cleanup before save
            if (ServiceLocator.Instance.TryGet<IPartyService>(out var partyService)) {
                partyService.OnPlayerDisconnected(conn.ClientId);
            }

            if (!ServiceLocator.Instance.TryGet<IPersistenceService>(out var persistence)) {
                Debug.LogWarning($"[SpawnManager] Disconnect save SKIPPED: No IPersistenceService");
                return;
            }

            if (!(persistence is NakamaManager nakama)) {
                Debug.LogWarning($"[SpawnManager] Disconnect save SKIPPED: Not NakamaManager");
                return;
            }

            // ── SAVE FIRST (sessions and objects still valid) ──
            NetworkObject playerObj = conn.FirstObject;
            if (playerObj != null) {
                string userId = nakama.GetUserId(conn.ClientId);
                if (!string.IsNullOrEmpty(userId) && ServiceLocator.Instance.TryGet<IPersistenceBridge>(out var bridge)) {
                    try {
                        var data = bridge.ExtractPlayerData(playerObj);
                        if (data != null) {
                            // Fire and forget — SaveAsync captures session ref synchronously
                            // before UnregisterConnection removes it below
                            var saveTask = persistence.SaveAsync(userId, data);
                            saveTask.ContinueWith(t => {
                                if (t.IsFaulted)
                                    Debug.LogError($"[SpawnManager] Disconnect save FAILED: {t.Exception?.InnerException?.Message}");
                                else
                                    Debug.Log($"[SpawnManager] ✓ Disconnect save CONFIRMED: {data.playerName} pos=({data.posX:F1},{data.posY:F1},{data.posZ:F1})");
                            });
                            Debug.Log($"[SpawnManager] Disconnect save queued: {data.playerName} pos=({data.posX:F1},{data.posY:F1},{data.posZ:F1})");
                        }
                    } catch (System.Exception e) {
                        Debug.LogError($"[SpawnManager] Disconnect save exception: {e.Message}");
                    }
                }
            } else {
                Debug.LogWarning($"[SpawnManager] Disconnect save: conn.FirstObject is null (unexpected)");
            }

            // ── CLEANUP AFTER save started ──
            nakama.UnregisterPlayer(conn.ClientId);
            nakama.UnregisterConnection(conn.ClientId);
            Debug.Log($"[SpawnManager] ✓ Cleanup done for client {conn.ClientId}");
            Debug.Log($"[SpawnManager] ═══ DISCONNECT SAVE END ═══");
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

            // Get spawn position from provider (if registered), otherwise use spawnPoints
            Vector3 pos = GetSpawnPosition();

            // Snap to ground to prevent spawning mid-air
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 20f))
            {
                pos.y = hit.point.y;
            }

            GameObject player = Instantiate(playerPrefab, pos, Quaternion.identity);

            // Disable CharacterController before FishNet spawn to prevent physics conflicts
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // FishNet Spawn
            _networkManager.ServerManager.Spawn(player, conn);

            // Re-enable CC after spawn with correct position
            if (cc != null)
            {
                player.transform.position = pos;
                cc.enabled = true;
            }

            Debug.Log($"[SpawnManager] SPAWN for Client {conn.ClientId} at {pos}");
        }

        private Vector3 GetSpawnPosition() {
            // Try to get spawn position provider from ServiceLocator (allows Simulation layer to provide positions)
            var positionProvider = ServiceLocator.Instance.Get<ISpawnPositionProvider>();
            if (positionProvider != null) {
                return positionProvider.GetSpawnPosition();
            }

            // Fallback to legacy spawnPoints
            if (spawnPoints != null && spawnPoints.Length > 0) {
                return spawnPoints[Random.Range(0, spawnPoints.Length)].position;
            }

            return Vector3.zero;
        }
    }
}

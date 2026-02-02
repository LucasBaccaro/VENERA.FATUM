using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Genesis.Simulation {
    /// <summary>
    /// Spawns a Chest (or any NetworkObject) at runtime.
    /// Converted to MonoBehaviour to avoid SceneID synchronization issues.
    /// </summary>
    public class ChestSpawner : MonoBehaviour {
        [Header("Settings")]
        [SerializeField] private NetworkObject _chestPrefab;
        [SerializeField] private bool _spawnOnStart = true;

        private bool _hasSpawned = false;

        private void Start() {
            if (InstanceFinder.NetworkManager == null) {
                Debug.LogError("[ChestSpawner] NetworkManager not found!");
                return;
            }

            InstanceFinder.NetworkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;

            // If server is already running when this script starts
            if (InstanceFinder.ServerManager.Started) {
                TrySpawn();
            }
        }

        private void OnDestroy() {
            if (InstanceFinder.NetworkManager != null) {
                InstanceFinder.NetworkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            }
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args) {
            // Spawn when server starts
            if (args.ConnectionState == LocalConnectionState.Started) {
                TrySpawn();
            }
        }

        private void TrySpawn() {
            if (_spawnOnStart && !_hasSpawned) {
                SpawnChest();
                _hasSpawned = true;
            }
        }

        public void SpawnChest() {
            if (_chestPrefab == null) {
                Debug.LogWarning("[ChestSpawner] No Prefab assigned!");
                return;
            }

            // Instantiate at this spawner's position/rotation
            NetworkObject chestInstance = Instantiate(_chestPrefab, transform.position, transform.rotation);
            
            // Spawn over network
            InstanceFinder.ServerManager.Spawn(chestInstance);
            
            Debug.Log($"[ChestSpawner] Spawning Chest at {transform.position}");
        }
        
#if UNITY_EDITOR
        private void OnDrawGizmos() {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one);
            Gizmos.DrawIcon(transform.position + Vector3.up, "Chest Icon", true);
        }
#endif
    }
}

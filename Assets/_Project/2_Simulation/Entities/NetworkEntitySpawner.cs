using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Genesis.Simulation {
    /// <summary>
    /// Generic spawner for any NetworkObject prefab.
    /// Uses dynamic ServerManager.Spawn() to ensure replication to remote clients
    /// in scenes loaded outside of FishNet's scene manager.
    /// </summary>
    public class NetworkEntitySpawner : MonoBehaviour {
        [Header("Settings")]
        [SerializeField] private NetworkObject _prefab;
        [SerializeField] private bool _spawnOnStart = true;

        private bool _hasSpawned = false;

        private void Start() {
            if (InstanceFinder.NetworkManager == null) {
                Debug.LogError("[NetworkEntitySpawner] NetworkManager not found!");
                return;
            }

            InstanceFinder.NetworkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;

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
            if (args.ConnectionState == LocalConnectionState.Started) {
                TrySpawn();
            }
        }

        private void TrySpawn() {
            if (_spawnOnStart && !_hasSpawned) {
                Spawn();
                _hasSpawned = true;
            }
        }

        public void Spawn() {
            if (_prefab == null) {
                Debug.LogWarning($"[NetworkEntitySpawner] No prefab assigned on {gameObject.name}!");
                return;
            }

            NetworkObject instance = Instantiate(_prefab, transform.position, transform.rotation);
            InstanceFinder.ServerManager.Spawn(instance);
            Debug.Log($"[NetworkEntitySpawner] Spawned {_prefab.name} at {transform.position}");
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f,
                _prefab != null ? _prefab.name : "No Prefab",
                new GUIStyle { normal = { textColor = Color.cyan }, fontSize = 12 });
        }
#endif
    }
}

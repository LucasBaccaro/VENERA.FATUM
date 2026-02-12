using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using System.Collections;
using System.Collections.Generic;
using Genesis.Data;

namespace Genesis.Simulation {

    public class EnemySpawner : MonoBehaviour {

        [Header("Spawn Config")]
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private EnemyData _enemyData;
        [SerializeField] private int _maxAlive = 5;
        [SerializeField] private float _respawnDelay = 15f;
        [SerializeField] private float _spawnRadius = 10f;

        [Header("Initial Spawn")]
        [SerializeField] private int _initialCount = 3;

        private List<NetworkObject> _aliveEnemies = new List<NetworkObject>();
        private int _pendingRespawns = 0;
        private bool _initialized = false;

        private void Start() {
            if (InstanceFinder.NetworkManager == null) return;

            InstanceFinder.NetworkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;

            if (InstanceFinder.ServerManager.Started) {
                Initialize();
            }
        }

        private void OnDestroy() {
            if (InstanceFinder.NetworkManager != null) {
                InstanceFinder.NetworkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            }
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args) {
            if (args.ConnectionState == LocalConnectionState.Started) {
                Initialize();
            }
        }

        private void Initialize() {
            if (_initialized) return;
            _initialized = true;
            StartCoroutine(InitialSpawn());
        }

        private IEnumerator InitialSpawn() {
            yield return new WaitForSeconds(1f);

            for (int i = 0; i < _initialCount && _aliveEnemies.Count < _maxAlive; i++) {
                SpawnEnemy();
                yield return new WaitForSeconds(0.2f);
            }
        }

        private void Update() {
            if (!_initialized || !InstanceFinder.IsServerStarted) return;

            // Cleanup dead references
            _aliveEnemies.RemoveAll(e => e == null || !e.gameObject.activeInHierarchy);

            // Check if we need respawns
            int deficit = _maxAlive - _aliveEnemies.Count - _pendingRespawns;
            if (deficit > 0) {
                _pendingRespawns += deficit;
                for (int i = 0; i < deficit; i++) {
                    StartCoroutine(RespawnAfterDelay());
                }
            }
        }

        private IEnumerator RespawnAfterDelay() {
            yield return new WaitForSeconds(_respawnDelay);
            _pendingRespawns--;
            if (_aliveEnemies.Count < _maxAlive) {
                SpawnEnemy();
            }
        }

        private void SpawnEnemy() {
            if (_enemyPrefab == null) {
                Debug.LogError("[EnemySpawner] No enemy prefab assigned!");
                return;
            }

            Vector3 spawnPos = GetRandomSpawnPosition();
            GameObject enemy = Instantiate(_enemyPrefab, spawnPos, Quaternion.identity);

            NetworkObject nob = enemy.GetComponent<NetworkObject>();
            if (nob != null) {
                FishNet.InstanceFinder.ServerManager.Spawn(enemy);
                _aliveEnemies.Add(nob);
            }
        }

        private Vector3 GetRandomSpawnPosition() {
            Vector2 randomCircle = Random.insideUnitCircle * _spawnRadius;
            Vector3 candidate = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            // Raycast down to find ground
            if (Physics.Raycast(candidate + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f)) {
                return hit.point;
            }

            return candidate;
        }

        private void OnDrawGizmosSelected() {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _spawnRadius);
        }
    }
}

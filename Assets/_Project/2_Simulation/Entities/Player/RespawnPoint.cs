using UnityEngine;

namespace Genesis.Simulation {

    /// <summary>
    /// Marks a respawn location in the scene.
    /// Place this on an empty GameObject where players should respawn after death.
    /// </summary>
    public class RespawnPoint : MonoBehaviour {

        private static RespawnPoint _instance;
        public static RespawnPoint Instance => _instance;

        [Header("Settings")]
        [SerializeField] private float _spawnRadius = 2f;

        private void Awake() {
            if (_instance != null && _instance != this) {
                Debug.LogWarning("[RespawnPoint] Multiple RespawnPoints found. Using the latest one.");
            }
            _instance = this;
        }

        private void OnDestroy() {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Returns a random position within the spawn radius to avoid stacking.
        /// </summary>
        public Vector3 GetSpawnPosition() {
            Vector2 offset = Random.insideUnitCircle * _spawnRadius;
            return transform.position + new Vector3(offset.x, 0f, offset.y);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            Gizmos.color = new Color(0f, 1f, 0.3f, 0.4f);
            Gizmos.DrawSphere(transform.position, 0.5f);
            Gizmos.color = new Color(0f, 1f, 0.3f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, _spawnRadius);
        }
#endif
    }
}

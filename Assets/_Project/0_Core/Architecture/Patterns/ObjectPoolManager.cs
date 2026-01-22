using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Core {

    /// <summary>
    /// Manager central para todos los Object Pools del juego.
    /// Se configura en el Inspector con los prefabs a poolear.
    /// </summary>
    public class ObjectPoolManager : Singleton<ObjectPoolManager> {

        [System.Serializable]
        public class PoolConfig {
            public GameObject prefab;
            public int preWarmCount = 20;
            public int maxSize = 100;
        }

        [Header("Pool Configurations")]
        [SerializeField] private PoolConfig[] poolConfigs;

        private Dictionary<string, object> _pools = new Dictionary<string, object>();

        // ═══════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════

        protected override void Awake() {
            base.Awake();
            InitializePools();
        }

        private void InitializePools() {
            foreach (var config in poolConfigs) {
                if (config.prefab == null) {
                    Debug.LogError("[ObjectPoolManager] Pool config tiene prefab null. Saltando.");
                    continue;
                }

                string key = config.prefab.name;

                // Obtener el componente principal del prefab
                var component = config.prefab.GetComponent<MonoBehaviour>();
                if (component == null) {
                    Debug.LogError($"[ObjectPoolManager] Prefab {key} no tiene MonoBehaviour. Saltando.");
                    continue;
                }

                System.Type type = component.GetType();

                // Crear pool genérico usando reflexión
                System.Type poolType = typeof(ObjectPool<>).MakeGenericType(type);
                object pool = System.Activator.CreateInstance(
                    poolType,
                    component,
                    config.preWarmCount,
                    config.maxSize,
                    transform // Usar este transform como contenedor
                );

                _pools[key] = pool;

                Debug.Log($"[ObjectPoolManager] Initialized pool '{key}' with {config.preWarmCount} instances");
            }

            // Registrar en ServiceLocator
            ServiceLocator.Instance.Register(this);
        }

        // ═══════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Obtiene un pool específico por nombre de prefab
        /// </summary>
        public ObjectPool<T> GetPool<T>(string prefabName) where T : MonoBehaviour {
            if (_pools.TryGetValue(prefabName, out object pool)) {
                return pool as ObjectPool<T>;
            }

            Debug.LogError($"[ObjectPoolManager] Pool '{prefabName}' no encontrado. ¿Lo configuraste en el Inspector?");
            return null;
        }

        /// <summary>
        /// Shortcut: Obtiene directamente una instancia de un pool
        /// </summary>
        public T Get<T>(string prefabName) where T : MonoBehaviour {
            var pool = GetPool<T>(prefabName);
            return pool?.Get();
        }

        /// <summary>
        /// Shortcut: Devuelve una instancia a su pool
        /// </summary>
        public void Return<T>(string prefabName, T instance) where T : MonoBehaviour {
            var pool = GetPool<T>(prefabName);
            pool?.Return(instance);
        }

        /// <summary>
        /// Limpia todos los pools
        /// </summary>
        public void ClearAllPools() {
            foreach (var kvp in _pools) {
                // Invoke Clear() via reflection
                var clearMethod = kvp.Value.GetType().GetMethod("Clear");
                clearMethod?.Invoke(kvp.Value, null);
            }

            Debug.Log("[ObjectPoolManager] All pools cleared");
        }

        /// <summary>
        /// Debug: Muestra estadísticas de todos los pools
        /// </summary>
        public void LogAllPoolStats() {
            Debug.Log($"[ObjectPoolManager] Stats for {_pools.Count} pools:");

            foreach (var kvp in _pools) {
                var logMethod = kvp.Value.GetType().GetMethod("LogStats");
                logMethod?.Invoke(kvp.Value, null);
            }
        }

        // ═══════════════════════════════════════════════════════
        // CLEANUP
        // ═══════════════════════════════════════════════════════

        protected override void OnDestroy() {
            base.OnDestroy();
            ClearAllPools();
        }

#if UNITY_EDITOR
        [ContextMenu("Log Pool Stats")]
        private void DebugLogStats() {
            LogAllPoolStats();
        }
#endif
    }
}

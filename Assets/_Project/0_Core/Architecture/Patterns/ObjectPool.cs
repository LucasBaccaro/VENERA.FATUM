using UnityEngine;
using System.Collections.Generic;

namespace Genesis.Core {

    /// <summary>
    /// Generic Object Pool para reducir garbage collection.
    /// OBLIGATORIO para proyectiles, VFX, y cualquier entidad que se spawne frecuentemente.
    /// </summary>
    public class ObjectPool<T> where T : MonoBehaviour {

        private readonly T _prefab;
        private readonly Queue<T> _pool = new Queue<T>();
        private readonly Transform _container;
        private readonly int _maxSize;

        public int ActiveCount { get; private set; }
        public int PooledCount => _pool.Count;

        // ═══════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ═══════════════════════════════════════════════════════

        public ObjectPool(T prefab, int preWarmCount = 10, int maxSize = 100, Transform container = null) {
            _prefab = prefab;
            _maxSize = maxSize;

            // Contenedor para organizar jerarquía
            if (container == null) {
                GameObject containerObj = new GameObject($"Pool_{prefab.name}");
                _container = containerObj.transform;
            } else {
                _container = container;
            }

            // Pre-warm: crear instancias iniciales
            PreWarm(preWarmCount);
        }

        // ═══════════════════════════════════════════════════════
        // PRE-WARMING
        // ═══════════════════════════════════════════════════════

        private void PreWarm(int count) {
            for (int i = 0; i < count; i++) {
                T instance = Object.Instantiate(_prefab, _container);
                instance.gameObject.SetActive(false);
                _pool.Enqueue(instance);
            }

            Debug.Log($"[ObjectPool] Pre-warmed {count} instances of {_prefab.name}");
        }

        // ═══════════════════════════════════════════════════════
        // GET / RETURN
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Obtiene una instancia del pool (o crea una nueva si está vacío)
        /// </summary>
        public T Get() {
            T instance;

            if (_pool.Count > 0) {
                instance = _pool.Dequeue();
                instance.gameObject.SetActive(true);
            } else {
                // Pool exhausted - crear nueva instancia
                instance = Object.Instantiate(_prefab, _container);
                Debug.LogWarning($"[ObjectPool] {_prefab.name} pool exhausted. Creating new instance. Consider increasing pre-warm count.");
            }

            ActiveCount++;
            return instance;
        }

        /// <summary>
        /// Devuelve una instancia al pool
        /// </summary>
        public void Return(T instance) {
            if (instance == null) {
                Debug.LogWarning("[ObjectPool] Intentando devolver una instancia null al pool.");
                return;
            }

            // Verificar límite de tamaño
            if (_pool.Count >= _maxSize) {
                Debug.LogWarning($"[ObjectPool] Pool lleno ({_maxSize}). Destruyendo instancia de {_prefab.name}.");
                Object.Destroy(instance.gameObject);
                ActiveCount--;
                return;
            }

            instance.gameObject.SetActive(false);
            instance.transform.SetParent(_container);
            instance.transform.ResetLocal();
            _pool.Enqueue(instance);

            ActiveCount--;
        }

        // ═══════════════════════════════════════════════════════
        // CLEANUP
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Destruye todas las instancias del pool
        /// </summary>
        public void Clear() {
            while (_pool.Count > 0) {
                T instance = _pool.Dequeue();
                if (instance != null) {
                    Object.Destroy(instance.gameObject);
                }
            }

            ActiveCount = 0;
            Debug.Log($"[ObjectPool] Pool {_prefab.name} cleared");
        }

        /// <summary>
        /// Debug: Imprime estadísticas del pool
        /// </summary>
        public void LogStats() {
            Debug.Log($"[ObjectPool] {_prefab.name} - Pooled: {PooledCount}, Active: {ActiveCount}, Max: {_maxSize}");
        }
    }
}

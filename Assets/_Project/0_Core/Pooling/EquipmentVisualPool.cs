using UnityEngine;
using System.Collections.Generic;

namespace Genesis.Core
{
    /// <summary>
    /// Pool de GameObjects para visuales de equipamiento.
    /// Evita instanciar/destruir constantemente al cambiar items.
    /// </summary>
    public class EquipmentVisualPool : MonoBehaviour
    {
        public static EquipmentVisualPool Instance { get; private set; }
        
        [Header("Pool Settings")]
        [SerializeField] private int initialPoolSize = 10;
        [SerializeField] private Transform poolContainer;
        
        // Pool: Prefab -> Lista de instancias disponibles
        private Dictionary<GameObject, Queue<GameObject>> _pools = new Dictionary<GameObject, Queue<GameObject>>();
        
        // Tracking: Instancia -> Prefab original (para saber a qué pool devolver)
        private Dictionary<GameObject, GameObject> _instanceToPrefab = new Dictionary<GameObject, GameObject>();
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            if (poolContainer == null)
            {
                poolContainer = new GameObject("EquipmentVisualPool_Container").transform;
                poolContainer.SetParent(transform);
            }
        }
        
        /// <summary>
        /// Obtiene una instancia del pool (o crea una nueva si no hay disponibles).
        /// </summary>
        public GameObject GetFromPool(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError("[EquipmentVisualPool] Prefab is null");
                return null;
            }
            
            // Si no existe pool para este prefab, crearlo
            if (!_pools.ContainsKey(prefab))
            {
                _pools[prefab] = new Queue<GameObject>();
            }
            
            GameObject instance;
            
            // Si hay instancias disponibles en el pool, reutilizar
            if (_pools[prefab].Count > 0)
            {
                instance = _pools[prefab].Dequeue();
                instance.SetActive(true);
            }
            else
            {
                // Pool vacío, instanciar nuevo
                instance = Instantiate(prefab, poolContainer);
                _instanceToPrefab[instance] = prefab;
            }
            
            return instance;
        }
        
        /// <summary>
        /// Devuelve una instancia al pool.
        /// </summary>
        public void ReturnToPool(GameObject instance)
        {
            if (instance == null) return;
            
            // Verificar que esta instancia fue creada por este pool
            if (!_instanceToPrefab.TryGetValue(instance, out GameObject prefab))
            {
                Debug.LogWarning($"[EquipmentVisualPool] Trying to return instance that wasn't created by this pool: {instance.name}");
                Destroy(instance);
                return;
            }
            
            // Desactivar y devolver al pool
            instance.SetActive(false);
            instance.transform.SetParent(poolContainer);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            
            _pools[prefab].Enqueue(instance);
        }
        
        /// <summary>
        /// Limpia todos los pools (útil al cambiar de escena).
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                while (pool.Count > 0)
                {
                    GameObject instance = pool.Dequeue();
                    if (instance != null)
                        Destroy(instance);
                }
            }
            
            _pools.Clear();
            _instanceToPrefab.Clear();
        }
    }
}

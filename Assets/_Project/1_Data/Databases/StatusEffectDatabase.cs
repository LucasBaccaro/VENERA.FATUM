using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Genesis.Data {

    /// <summary>
    /// Base de datos central de todos los Status Effects del juego.
    /// Permite buscar StatusEffectData por ID.
    /// Se carga automáticamente al inicio usando Resources.LoadAll o una lista asignada.
    /// </summary>
    [CreateAssetMenu(fileName = "StatusEffectDatabase", menuName = "Genesis/System/Status Effect Database")]
    public class StatusEffectDatabase : ScriptableObject {

        [Header("Registry")]
        [SerializeField] private List<StatusEffectData> effects = new List<StatusEffectData>();

        // Lookup rápido
        private Dictionary<int, StatusEffectData> _lookup;
        private static StatusEffectDatabase _instance;

        public static StatusEffectDatabase Instance {
            get {
                if (_instance == null) {
                    // Carga perezosa desde Resources si no está asignado
                    _instance = Resources.Load<StatusEffectDatabase>("Databases/StatusEffectDatabase");
                    if (_instance != null) _instance.Initialize();
                }
                return _instance;
            }
        }

        public void Initialize() {
            if (_lookup != null) return;

            _lookup = new Dictionary<int, StatusEffectData>();
            foreach (var effect in effects) {
                if (effect != null) {
                    if (_lookup.ContainsKey(effect.ID)) {
                        Debug.LogWarning($"[StatusEffectDatabase] ID duplicado: {effect.ID} en {effect.name}");
                    } else {
                        _lookup.Add(effect.ID, effect);
                    }
                }
            }
            Debug.Log($"[StatusEffectDatabase] Inicializada con {_lookup.Count} status effects.");
        }

        /// <summary>
        /// Obtiene un StatusEffectData por ID.
        /// </summary>
        public StatusEffectData GetEffect(int id) {
            if (_lookup == null) Initialize();

            if (_lookup.TryGetValue(id, out StatusEffectData data)) {
                return data;
            }
            Debug.LogError($"[StatusEffectDatabase] Status Effect ID {id} no encontrado!");
            return null;
        }

        /// <summary>
        /// Método estático para uso conveniente desde StatusEffectSystem.
        /// </summary>
        public static StatusEffectData GetEffectStatic(int id) {
            return Instance?.GetEffect(id);
        }

        /// <summary>
        /// Obtiene todos los efectos registrados.
        /// </summary>
        public List<StatusEffectData> GetAllEffects() {
            return new List<StatusEffectData>(effects);
        }

#if UNITY_EDITOR
        [ContextMenu("Auto-Find All Status Effects")]
        public void FindAllStatusEffects() {
            effects.Clear();
            string[] guids = AssetDatabase.FindAssets("t:StatusEffectData");

            foreach (string guid in guids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                StatusEffectData asset = AssetDatabase.LoadAssetAtPath<StatusEffectData>(path);
                if (asset != null) {
                    effects.Add(asset);
                }
            }

            // Ordenar por ID para facilitar debugging
            effects.Sort((a, b) => a.ID.CompareTo(b.ID));

            Debug.Log($"[StatusEffectDatabase] Encontrados {effects.Count} status effects en el proyecto.");
            EditorUtility.SetDirty(this);
        }

        [ContextMenu("Validate IDs")]
        public void ValidateIDs() {
            HashSet<int> seenIDs = new HashSet<int>();
            List<string> duplicates = new List<string>();

            foreach (var effect in effects) {
                if (effect != null) {
                    if (seenIDs.Contains(effect.ID)) {
                        duplicates.Add($"ID {effect.ID}: {effect.name}");
                    } else {
                        seenIDs.Add(effect.ID);
                    }
                }
            }

            if (duplicates.Count > 0) {
                Debug.LogError($"[StatusEffectDatabase] IDs duplicados encontrados:\n" + string.Join("\n", duplicates));
            } else {
                Debug.Log($"[StatusEffectDatabase] Validación OK: {effects.Count} efectos, sin duplicados.");
            }
        }
#endif
    }
}

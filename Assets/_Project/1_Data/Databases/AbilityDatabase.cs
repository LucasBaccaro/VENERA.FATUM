using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Genesis.Data {

    /// <summary>
    /// Base de datos central de todas las habilidades del juego.
    /// Permite buscar AbilityData por ID.
    /// Se carga automáticamente al inicio usando Resources.LoadAll o una lista asignada.
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityDatabase", menuName = "Genesis/System/Ability Database")]
    public class AbilityDatabase : ScriptableObject {
        
        [Header("Registry")]
        [SerializeField] private List<AbilityData> abilities = new List<AbilityData>();
        
        // Lookup rápido
        private Dictionary<int, AbilityData> _lookup;
        private static AbilityDatabase _instance;

        public static AbilityDatabase Instance {
            get {
                if (_instance == null) {
                    // Carga perezosa desde Resources si no está asignado
                    _instance = Resources.Load<AbilityDatabase>("Databases/AbilityDatabase");
                    if (_instance != null) _instance.Initialize();
                }
                return _instance;
            }
        }

        public void Initialize() {
            if (_lookup != null) return;

            _lookup = new Dictionary<int, AbilityData>();
            foreach (var ability in abilities) {
                if (ability != null) {
                    if (_lookup.ContainsKey(ability.ID)) {
                        Debug.LogWarning($"[AbilityDatabase] ID duplicado: {ability.ID} en {ability.name}");
                    } else {
                        _lookup.Add(ability.ID, ability);
                    }
                }
            }
            Debug.Log($"[AbilityDatabase] Inicializada con {_lookup.Count} habilidades.");
        }

        public AbilityData GetAbility(int id) {
            if (_lookup == null) Initialize();
            
            if (_lookup.TryGetValue(id, out AbilityData data)) {
                return data;
            }
            Debug.LogError($"[AbilityDatabase] Habilidad ID {id} no encontrada!");
            return null;
        }

#if UNITY_EDITOR
        [ContextMenu("Auto-Find All Abilities")]
        public void FindAllAbilities() {
            abilities.Clear();
            string[] guids = AssetDatabase.FindAssets("t:AbilityData");
            
            foreach (string guid in guids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AbilityData asset = AssetDatabase.LoadAssetAtPath<AbilityData>(path);
                if (asset != null) {
                    abilities.Add(asset);
                }
            }
            Debug.Log($"[AbilityDatabase] Encontradas {abilities.Count} habilidades en el proyecto.");
            EditorUtility.SetDirty(this);
        }
#endif
    }
}

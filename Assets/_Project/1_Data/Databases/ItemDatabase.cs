using System.Collections.Generic;
using UnityEngine;
using Genesis.Items;

namespace Genesis.Data {
    /// <summary>
    /// Central registry for all items in the game
    /// Similar pattern to AbilityDatabase
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "VENERA.FATUM/Databases/Item Database", order = 0)]
    public class ItemDatabase : ScriptableObject {
        private static ItemDatabase _instance;

        public static ItemDatabase Instance {
            get {
                if (_instance == null) {
                    _instance = Resources.Load<ItemDatabase>("Databases/ItemDatabase");
                    if (_instance == null) {
                        Debug.LogError("[ItemDatabase] No ItemDatabase found in Resources/Databases folder!");
                    }
                }
                return _instance;
            }
        }

        [Header("Item Registry")]
        [Tooltip("All items in the game")]
        [SerializeField] private List<BaseItemData> _allItems = new List<BaseItemData>();

        private Dictionary<int, BaseItemData> _itemDictionary;

        /// <summary>
        /// Initialize the database (build lookup dictionary)
        /// </summary>
        private void OnEnable() {
            BuildDictionary();
        }

        /// <summary>
        /// Build the lookup dictionary from the item list
        /// </summary>
        private void BuildDictionary() {
            _itemDictionary = new Dictionary<int, BaseItemData>();

            foreach (var item in _allItems) {
                if (item == null) {
                    Debug.LogWarning("[ItemDatabase] Null item found in database!");
                    continue;
                }

                if (item.ItemID == 0) {
                    Debug.LogWarning($"[ItemDatabase] Item '{item.name}' has ItemID = 0, skipping.");
                    continue;
                }

                if (_itemDictionary.ContainsKey(item.ItemID)) {
                    Debug.LogError($"[ItemDatabase] Duplicate ItemID {item.ItemID}! Items: '{_itemDictionary[item.ItemID].name}' and '{item.name}'");
                    continue;
                }

                _itemDictionary[item.ItemID] = item;
            }

            Debug.Log($"[ItemDatabase] Loaded {_itemDictionary.Count} items.");
        }

        /// <summary>
        /// Get item by ID
        /// </summary>
        public BaseItemData GetItem(int itemId) {
            if (_itemDictionary == null || _itemDictionary.Count == 0) {
                BuildDictionary();
            }

            if (_itemDictionary.TryGetValue(itemId, out BaseItemData item)) {
                return item;
            }

            Debug.LogWarning($"[ItemDatabase] Item with ID {itemId} not found!");
            return null;
        }

        /// <summary>
        /// Get equipment item by ID (type-safe)
        /// </summary>
        public EquipmentItemData GetEquipment(int itemId) {
            BaseItemData item = GetItem(itemId);
            return item as EquipmentItemData;
        }

        /// <summary>
        /// Get consumable item by ID (type-safe)
        /// </summary>
        public ConsumableItemData GetConsumable(int itemId) {
            BaseItemData item = GetItem(itemId);
            return item as ConsumableItemData;
        }

        /// <summary>
        /// Check if item exists
        /// </summary>
        public bool ItemExists(int itemId) {
            if (_itemDictionary == null || _itemDictionary.Count == 0) {
                BuildDictionary();
            }

            return _itemDictionary.ContainsKey(itemId);
        }

        /// <summary>
        /// Get all items of a specific type
        /// </summary>
        public List<BaseItemData> GetItemsByType(ItemType type) {
            List<BaseItemData> result = new List<BaseItemData>();

            foreach (var item in _allItems) {
                if (item != null && item.Type == type) {
                    result.Add(item);
                }
            }

            return result;
        }

        /// <summary>
        /// Editor utility: Auto-find all items in project
        /// </summary>
        [ContextMenu("Auto-Find All Items")]
        private void AutoFindAllItems() {
#if UNITY_EDITOR
            _allItems.Clear();

            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BaseItemData");
            foreach (string guid in guids) {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                BaseItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<BaseItemData>(path);
                if (item != null) {
                    _allItems.Add(item);
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ItemDatabase] Auto-found {_allItems.Count} items.");
            BuildDictionary();
            ValidateItemIDs();
#endif
        }

        /// <summary>
        /// Editor utility: Validate that all item IDs are unique
        /// </summary>
        [ContextMenu("Validate Item IDs")]
        private void ValidateItemIDs() {
            HashSet<int> seenIds = new HashSet<int>();
            List<string> duplicates = new List<string>();

            foreach (var item in _allItems) {
                if (item == null) continue;

                if (item.ItemID == 0) {
                    Debug.LogWarning($"[ItemDatabase] Item '{item.name}' has ItemID = 0!", item);
                    continue;
                }

                if (seenIds.Contains(item.ItemID)) {
                    duplicates.Add($"Duplicate ID {item.ItemID}: {item.name}");
                } else {
                    seenIds.Add(item.ItemID);
                }
            }

            if (duplicates.Count > 0) {
                Debug.LogError($"[ItemDatabase] Found {duplicates.Count} duplicate IDs:\n" + string.Join("\n", duplicates));
            } else {
                Debug.Log($"[ItemDatabase] All {_allItems.Count} item IDs are unique!");
            }
        }
    }
}

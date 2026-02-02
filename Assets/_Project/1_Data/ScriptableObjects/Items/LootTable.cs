using UnityEngine;
using System.Collections.Generic;
using Genesis.Data;
using Genesis.Items;

namespace Genesis.Simulation {
    
    [System.Serializable]
    public struct LootEntry {
        [Tooltip("Item to drop")]
        public BaseItemData Item;
        
        [Tooltip("Chance to drop (0.0 to 1.0)")]
        [Range(0f, 1f)]
        public float DropChance;
        
        [Tooltip("Minimum quantity")]
        [Min(1)]
        public int MinQuantity;
        
        [Tooltip("Maximum quantity")]
        [Min(1)]
        public int MaxQuantity;

        [Header("Item Properties")]
        [Tooltip("Tier of the item")]
        public ItemTier Tier;

        [Tooltip("Rarity of the item")]
        public ItemRarity Rarity;
    }

    [CreateAssetMenu(fileName = "NewLootTable", menuName = "VENERA.FATUM/Items/Loot Table", order = 1)]
    public class LootTable : ScriptableObject {
        [SerializeField] private List<LootEntry> _entries = new List<LootEntry>();

        public List<ItemSlot> GetLoot() {
            List<ItemSlot> loot = new List<ItemSlot>();

            foreach (var entry in _entries) {
                if (entry.Item == null) continue;

                if (Random.value <= entry.DropChance) {
                    int qty = Random.Range(entry.MinQuantity, entry.MaxQuantity + 1);
                    
                    // Use constructor to create ItemSlot
                    loot.Add(new ItemSlot(
                        entry.Item.ItemID,
                        qty,
                        entry.Tier,
                        entry.Rarity
                    ));
                }
            }

            return loot;
        }
    }
}

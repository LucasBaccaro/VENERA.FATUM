using UnityEngine;

namespace Genesis.Items {
    /// <summary>
    /// Base class for all item data (ScriptableObject)
    /// </summary>
    public abstract class BaseItemData : ScriptableObject {
        [Header("Basic Info")]
        [Tooltip("Unique item ID (must be unique across all items)")]
        public int ItemID;

        [Tooltip("Display name of the item")]
        public string ItemName;

        [Tooltip("Item description for tooltips")]
        [TextArea(3, 5)]
        public string Description;

        [Tooltip("Icon for UI display")]
        public Sprite Icon;

        [Header("Item Type")]
        [Tooltip("Type of item (Consumable, Equipment, etc.)")]
        public ItemType Type;

        [Header("Stacking")]
        [Tooltip("Can this item stack in inventory?")]
        public bool IsStackable = false;

        [Tooltip("Maximum stack size (only applies if IsStackable = true)")]
        public int MaxStackSize = 1;

        [Header("Loot Protection")]
        [Tooltip("Protected items do NOT drop on death (full loot protection)")]
        public bool IsProtected = false;

        /// <summary>
        /// Get display name with rarity color (for UI)
        /// </summary>
        public virtual string GetDisplayName(ItemRarity rarity) {
            return ItemName;
        }

        /// <summary>
        /// Get tooltip text (override in derived classes for custom tooltips)
        /// </summary>
        public virtual string GetTooltip(ItemRarity rarity) {
            return Description;
        }

        /// <summary>
        /// Validate item data (called in editor)
        /// </summary>
        protected virtual void OnValidate() {
            if (ItemID == 0) {
                Debug.LogWarning($"[BaseItemData] Item '{name}' has ItemID = 0. Please assign a unique ID.", this);
            }

            if (IsStackable && MaxStackSize <= 1) {
                Debug.LogWarning($"[BaseItemData] Item '{name}' is stackable but MaxStackSize <= 1.", this);
            }
        }
    }
}

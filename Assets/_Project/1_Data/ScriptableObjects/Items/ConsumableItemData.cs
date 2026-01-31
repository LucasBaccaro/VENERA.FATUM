using UnityEngine;

namespace Genesis.Items {
    /// <summary>
    /// ScriptableObject for consumable items (potions, food, buffs)
    /// </summary>
    [CreateAssetMenu(fileName = "Consumable_", menuName = "VENERA.FATUM/Items/Consumable", order = 2)]
    public class ConsumableItemData : BaseItemData {
        [Header("Consumable Properties")]
        [Tooltip("Type of consumable effect")]
        public ConsumableType ConsumableType;

        [Header("Effect Values")]
        [Tooltip("Amount to restore (HP for health potions, Mana for mana potions)")]
        public float RestoreAmount = 100f;

        [Tooltip("Cooldown in seconds (uses global potion cooldown)")]
        public float Cooldown = 3f;

        [Header("Usage Restrictions")]
        [Tooltip("Can be used while in combat?")]
        public bool UsableInCombat = true;

        [Tooltip("Can be used while moving?")]
        public bool UsableWhileMoving = true;

        /// <summary>
        /// Get tooltip with effect information
        /// </summary>
        public override string GetTooltip(ItemRarity rarity) {
            string tooltip = $"<b>{ItemName}</b>\n";
            tooltip += $"<color=#FFFFFF>Consumable</color>\n\n";
            tooltip += $"{Description}\n\n";

            tooltip += "<b>Effect:</b>\n";
            switch (ConsumableType) {
                case ConsumableType.HealthPotion:
                    tooltip += $"  Restores {RestoreAmount} HP\n";
                    break;
                case ConsumableType.ManaPotion:
                    tooltip += $"  Restores {RestoreAmount} Mana\n";
                    break;
            }

            tooltip += $"\n<i>Cooldown: {Cooldown}s</i>";

            return tooltip;
        }

        /// <summary>
        /// Validate consumable data
        /// </summary>
        protected override void OnValidate() {
            base.OnValidate();

            // Consumables should stack
            IsStackable = true;
            if (MaxStackSize <= 1) {
                MaxStackSize = 99; // Default stack size for consumables
            }

            // Ensure Type is set to Consumable
            Type = ItemType.Consumable;
        }
    }
}

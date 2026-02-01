using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Items {
    /// <summary>
    /// ScriptableObject for equipment items (armor, accessories)
    /// </summary>
    [CreateAssetMenu(fileName = "Equipment_", menuName = "VENERA.FATUM/Items/Equipment", order = 1)]
    public class EquipmentItemData : BaseItemData {
        [Header("Equipment Properties")]
        [Tooltip("Which slot this equipment occupies")]
        public EquipmentSlot Slot;

        [Tooltip("Required Class Name (empty for any)")]
        public string RequiredClass;

        [Header("Stat Modifiers (Per Rarity)")]
        [Tooltip("Stats for rarity Common (white)")]
        public List<StatModifier> CommonStats = new List<StatModifier>();

        [Tooltip("Stats for rarity Uncommon (green)")]
        public List<StatModifier> UncommonStats = new List<StatModifier>();

        [Tooltip("Stats for rarity Rare (blue)")]
        public List<StatModifier> RareStats = new List<StatModifier>();

        [Tooltip("Stats for rarity Epic (purple)")]
        public List<StatModifier> EpicStats = new List<StatModifier>();

        [Header("Visual Configuration")]
        [Tooltip("Visual data for this equipment piece")]
        public EquipmentVisualData VisualData;

        /// <summary>
        /// Get stat modifiers for a specific rarity
        /// </summary>
        public List<StatModifier> GetStatsForRarity(ItemRarity rarity) {
            switch (rarity) {
                case ItemRarity.Common: return CommonStats;
                case ItemRarity.Uncommon: return UncommonStats;
                case ItemRarity.Rare: return RareStats;
                case ItemRarity.Epic: return EpicStats;
                default:
                    Debug.LogWarning($"[EquipmentItemData] Unknown rarity '{rarity}', returning Common stats.");
                    return CommonStats;
            }
        }

        /// <summary>
        /// Get tooltip with stat information
        /// </summary>
        public override string GetTooltip(ItemRarity rarity) {
            string tooltip = $"<b>{ItemName}</b>\n";
            tooltip += $"<color={GetRarityColor(rarity)}>{rarity}</color>\n\n";
            tooltip += $"{Description}\n\n";

            List<StatModifier> stats = GetStatsForRarity(rarity);
            if (stats != null && stats.Count > 0) {
                tooltip += "<b>Stats:</b>\n";
                foreach (var stat in stats) {
                    tooltip += $"  {stat}\n";
                }
            }

            return tooltip;
        }

        /// <summary>
        /// Get hex color for rarity
        /// </summary>
        private string GetRarityColor(ItemRarity rarity) {
            switch (rarity) {
                case ItemRarity.Common: return "#FFFFFF";    // White
                case ItemRarity.Uncommon: return "#1EFF00";  // Green
                case ItemRarity.Rare: return "#0070DD";      // Blue
                case ItemRarity.Epic: return "#A335EE";      // Purple
                default: return "#FFFFFF";
            }
        }

        /// <summary>
        /// Validate equipment data
        /// </summary>
        protected override void OnValidate() {
            base.OnValidate();

            // Equipment should never stack
            IsStackable = false;
            MaxStackSize = 1;

            // Ensure Type is set to Equipment
            Type = ItemType.Equipment;
        }
    }
}

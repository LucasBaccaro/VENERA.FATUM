using System;

namespace Genesis.Items {
    /// <summary>
    /// Item type categories
    /// </summary>
    public enum ItemType {
        Consumable,
        Equipment,
        Material,  // Future: crafting materials
        Quest      // Future: quest items
    }

    /// <summary>
    /// Equipment slot positions
    /// </summary>
    public enum EquipmentSlot {
        Head,
        Shoulders,
        Chest,
        Pants,
        Feet,
        Hands,
        Belt,
        Weapon,
        OffHand,
        Ring1,
        Ring2
    }

    /// <summary>
    /// Item tier for progression (T0, T1, T2...)
    /// </summary>
    public enum ItemTier {
        T0,  // Tier 0 (starter/basic)
        T1,  // Tier 1 (intermediate)
        T2   // Tier 2 (advanced)
        // Future: T3, T4...
    }

    /// <summary>
    /// Item rarity/quality (all tiers use same rarities)
    /// </summary>
    public enum ItemRarity {
        Common,    // White
        Uncommon,  // Green
        Rare,      // Blue
        Epic       // Purple
        // Future: Legendary (Orange), Mythic (Red)
    }

    /// <summary>
    /// Types of consumable effects
    /// </summary>
    public enum ConsumableType {
        HealthPotion,
        ManaPotion,
        Buff,      // Future: temporary buffs
        Food       // Future: food items
    }

    /// <summary>
    /// Player stat types that can be modified by equipment
    /// </summary>
    public enum StatType {
        MaxHealth,     // Increases maximum HP (flat)
        MaxMana,       // Increases maximum Mana (flat)
        // Primary Attributes
        Strength,
        Agility,
        Intelligence,
        Wisdom,
        Constitution,
        // Combat Sub-stats
        Haste,
        LifeSteal,
        Penetration,
        Block,
        // World Sub-stats
        LootLuck,
        Lockpicking,
        Perception,
        MoveSpeed,
        Armor,
        MagicResistance
    }

    /// <summary>
    /// Stat modifier struct for equipment bonuses
    /// </summary>
    [Serializable]
    public struct StatModifier {
        public StatType Type;
        public float Value;

        public StatModifier(StatType type, float value) {
            Type = type;
            Value = value;
        }

        public override string ToString() {
            string prefix = Value >= 0 ? "+" : "";

            switch (Type) {
                case StatType.MaxHealth:
                    return $"{prefix}{Value} Max HP";
                case StatType.MaxMana:
                    return $"{prefix}{Value} Max Mana";
                case StatType.Strength:
                    return $"{prefix}{Value} Strength";
                case StatType.Agility:
                    return $"{prefix}{Value} Agility";
                case StatType.Intelligence:
                    return $"{prefix}{Value} Intelligence";
                case StatType.Wisdom:
                    return $"{prefix}{Value} Wisdom";
                case StatType.Constitution:
                    return $"{prefix}{Value} Constitution";
                case StatType.Haste:
                    return $"{prefix}{Value * 100f:F0}% Haste";
                case StatType.LifeSteal:
                    return $"{prefix}{Value * 100f:F0}% Life Steal";
                case StatType.Penetration:
                    return $"{prefix}{Value * 100f:F0}% Penetration";
                case StatType.Block:
                    return $"{prefix}{Value} Block";
                case StatType.LootLuck:
                    return $"{prefix}{Value * 100f:F0}% Loot Luck";
                case StatType.Lockpicking:
                    return $"{prefix}{Value} Lockpicking";
                case StatType.Perception:
                    return $"{prefix}{Value} Perception";
                case StatType.MoveSpeed:
                    return $"{prefix}{Value * 100f:F0}% Move Speed";
                case StatType.Armor:
                    return $"{prefix}{Value} Armor";
                case StatType.MagicResistance:
                    return $"{prefix}{Value} Magic Resistance";
                default:
                    return $"{prefix}{Value} {Type}";
            }
        }
    }

    /// <summary>
    /// Network-friendly item slot structure
    /// </summary>
    [Serializable]
    public struct ItemSlot {
        public int ItemID;         // ID from ItemDatabase (0 = empty)
        public int Quantity;       // Stack size
        public ItemTier Tier;      // Item tier
        public ItemRarity Rarity;  // Item rarity

        public bool IsEmpty => ItemID == 0;

        public static ItemSlot Empty => new ItemSlot { ItemID = 0, Quantity = 0, Tier = ItemTier.T0, Rarity = ItemRarity.Common };

        public ItemSlot(int itemId, int quantity, ItemTier tier, ItemRarity rarity) {
            ItemID = itemId;
            Quantity = quantity;
            Tier = tier;
            Rarity = rarity;
        }

        public override string ToString() {
            if (IsEmpty) return "[Empty]";
            return $"[ID:{ItemID} x{Quantity} {Tier} {Rarity}]";
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using Genesis.Items;

namespace Genesis.Data {
    /// <summary>
    /// Configuration for automatic stat generation on equipment items.
    /// Defines class attribute mappings, sub-stat pools, and rarity tier ranges.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemGenerationConfig", menuName = "Genesis/Core/Item Generation Config")]
    public class ItemGenerationConfig : ScriptableObject {
        [Header("Class Attribute Mappings")]
        [Tooltip("Maps class names to their primary/secondary attributes")]
        public List<ClassAttributeMapping> ClassMappings = new List<ClassAttributeMapping>();

        [Header("Sub-Stat Pools")]
        [Tooltip("Full pool of possible sub-stats (all slots except weapons)")]
        public List<StatType> SubStatPool = new List<StatType> {
            StatType.Haste,
            StatType.LifeSteal,
            StatType.Penetration,
            StatType.Block,
            StatType.LootLuck,
            StatType.Lockpicking,
            StatType.Perception,
            StatType.MoveSpeed
        };

        [Tooltip("Combat-only sub-stats (used for weapons)")]
        public List<StatType> CombatSubStatPool = new List<StatType> {
            StatType.Haste,
            StatType.LifeSteal,
            StatType.Penetration,
            StatType.Block
        };

        [Header("Rarity Tiers")]
        [Tooltip("Stat ranges for each rarity tier (Uncommon, Rare, Epic)")]
        public List<RarityTierConfig> RarityTiers = new List<RarityTierConfig>();

        /// <summary>
        /// Returns true for stats that use percentage values (0.01-0.06 range).
        /// Returns false for stats that use flat integer values.
        /// </summary>
        public static bool IsPercentageStat(StatType stat) {
            switch (stat) {
                case StatType.Haste:
                case StatType.LifeSteal:
                case StatType.Penetration:
                case StatType.LootLuck:
                case StatType.MoveSpeed:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Find the class mapping for a given class name.
        /// </summary>
        public ClassAttributeMapping? GetClassMapping(string className) {
            for (int i = 0; i < ClassMappings.Count; i++) {
                if (string.Equals(ClassMappings[i].ClassName, className, StringComparison.OrdinalIgnoreCase))
                    return ClassMappings[i];
            }
            return null;
        }

        /// <summary>
        /// Get the rarity tier config for a given rarity.
        /// </summary>
        public RarityTierConfig GetTierConfig(ItemRarity rarity) {
            switch (rarity) {
                case ItemRarity.Uncommon:
                    return RarityTiers.Count > 0 ? RarityTiers[0] : default;
                case ItemRarity.Rare:
                    return RarityTiers.Count > 1 ? RarityTiers[1] : default;
                case ItemRarity.Epic:
                    return RarityTiers.Count > 2 ? RarityTiers[2] : default;
                default:
                    return default;
            }
        }
    }

    [Serializable]
    public struct ClassAttributeMapping {
        public string ClassName;
        public StatType PrimaryAttribute;
        public StatType SecondaryAttribute;
        [Tooltip("Sub-stats that this class can NEVER roll")]
        public List<StatType> ExcludedSubStats;
    }

    [Serializable]
    public struct FloatRange {
        public float Min;
        public float Max;

        public FloatRange(float min, float max) {
            Min = min;
            Max = max;
        }

        public float RandomValue() {
            return UnityEngine.Random.Range(Min, Max);
        }

        public int RandomInt() {
            return UnityEngine.Random.Range((int)Min, (int)Max + 1);
        }
    }

    [Serializable]
    public struct RarityTierConfig {
        [Header("Armor (Head, Shoulders, Chest, Pants)")]
        [Tooltip("Primary attribute flat range")]
        public FloatRange ArmorPrimary;
        [Tooltip("Secondary attribute flat range")]
        public FloatRange ArmorSecondary;

        [Header("Complement (Hands, Feet)")]
        [Tooltip("Primary attribute flat range")]
        public FloatRange ComplementPrimary;
        [Tooltip("Secondary attribute flat range")]
        public FloatRange ComplementSecondary;

        [Header("Weapon (Weapon, OffHand)")]
        [Tooltip("Primary attribute flat range (highest)")]
        public FloatRange WeaponPrimary;
        [Tooltip("Secondary attribute flat range")]
        public FloatRange WeaponSecondary;

        [Header("Sub-Stats (added on top of primary/secondary)")]
        [Tooltip("Number of random sub-stats: Uncommon=1, Rare=2, Epic=3")]
        public int NumSubStats;
        [Tooltip("Value range for percentage-based sub-stats")]
        public FloatRange SubStatPercent;
        [Tooltip("Value range for flat sub-stats")]
        public FloatRange SubStatFlat;
    }
}

using UnityEngine;

namespace Genesis.Data {

    /// <summary>
    /// Configuration ScriptableObject containing all attribute scaling formulas.
    /// Single source of truth for balancing attribute-derived stats.
    /// </summary>
    [CreateAssetMenu(fileName = "AttributeConfig", menuName = "Genesis/Core/Attribute Config")]
    public class AttributeConfig : ScriptableObject {

        [Header("Strength Scaling")]
        [Tooltip("% physical damage bonus per STR point (0.005 = 0.5%)")]
        public float PhysicalDamagePerPoint = 0.005f;
        [Tooltip("Flat block value bonus per STR point")]
        public float BlockValuePerPoint = 0.5f;

        [Header("Agility Scaling")]
        [Tooltip("% crit chance per AGI point (0.0015 = 0.15%)")]
        public float CritChancePerPoint = 0.0015f;
        [Tooltip("% evasion chance per AGI point (0.001 = 0.1%)")]
        public float EvasionPerPoint = 0.001f;

        [Header("Intelligence Scaling")]
        [Tooltip("% magic damage bonus per INT point (0.005 = 0.5%)")]
        public float MagicDamagePerPoint = 0.005f;
        [Tooltip("% spell crit chance per INT point (0.0015 = 0.15%)")]
        public float SpellCritPerPoint = 0.0015f;

        [Header("Wisdom Scaling")]
        [Tooltip("% healing power bonus per WIS point (0.005 = 0.5%)")]
        public float HealingPowerPerPoint = 0.005f;
        [Tooltip("Flat mana regen per second per WIS point")]
        public float ManaRegenPerPoint = 0.1f;

        [Header("Constitution Scaling")]
        [Tooltip("Flat HP bonus per CON point")]
        public float HealthPerPoint = 5f;
        [Tooltip("HP regen per second (OOC) per CON point")]
        public float HPRegenPerPoint = 0.05f;

        [Header("Overpower (Warrior)")]
        [Tooltip("Base overpower chance (0.0 = 0%)")]
        public float OverpowerBaseChance = 0f;
        [Tooltip("Overpower chance per STR point (0.002 = 0.2%)")]
        public float OverpowerChancePerStr = 0.002f;
        [Tooltip("% of armor ignored on overpower")]
        public float OverpowerArmorIgnore = 0.5f;
        [Tooltip("Damage multiplier on overpower hit")]
        public float OverpowerDamageMultiplier = 1.5f;

        [Header("Critical Hits")]
        [Tooltip("Damage multiplier on critical hit")]
        public float CritDamageMultiplier = 2.0f;
        [Tooltip("Healing multiplier on critical heal")]
        public float CritHealMultiplier = 2.0f;

        [Header("Leveling")]
        public int MaxLevel = 50;
        [Tooltip("Attribute points granted per level")]
        public int PointsPerLevel = 5;
        [Tooltip("Base XP required for level 2")]
        public float BaseXP = 100f;
        [Tooltip("XP curve exponent (XP = BaseXP * Growth^(level-1))")]
        public float XPGrowthRate = 1.15f;

        /// <summary>
        /// Calculate XP required to reach the next level from current level.
        /// Formula: BaseXP * GrowthRate^(level - 1)
        /// </summary>
        public float GetXPForLevel(int level) {
            if (level <= 1) return BaseXP;
            return BaseXP * Mathf.Pow(XPGrowthRate, level - 1);
        }
    }
}

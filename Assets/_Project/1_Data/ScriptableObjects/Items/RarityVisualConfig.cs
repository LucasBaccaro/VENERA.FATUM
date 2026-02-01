using UnityEngine;

namespace Genesis.Items
{
    [System.Serializable]
    public class RarityVisualSettings
    {
        [Header("Flake Color Settings")]
        [Tooltip("Color applied to _FlakeColor shader property")]
        public Color FlakeColor = Color.white;
        
        [Header("Read-Only: Fixed Values")]
        [Tooltip("_FlakeIntensity is always 5.0 (does not vary by rarity)")]
        public const float FLAKE_INTENSITY = 5.0f;
    }
    
    [CreateAssetMenu(fileName = "RarityVisualConfig", menuName = "Genesis/Items/Rarity Visual Config")]
    public class RarityVisualConfig : ScriptableObject
    {
        [Header("Common (Tier 0)")]
        public RarityVisualSettings CommonSettings;
        
        [Header("Uncommon (Tier 1)")]
        public RarityVisualSettings UncommonSettings;
        
        [Header("Rare (Tier 2)")]
        public RarityVisualSettings RareSettings;
        
        [Header("Epic (Tier 3)")]
        public RarityVisualSettings EpicSettings;
        
        public RarityVisualSettings GetSettingsForRarity(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => CommonSettings,
                ItemRarity.Uncommon => UncommonSettings,
                ItemRarity.Rare => RareSettings,
                ItemRarity.Epic => EpicSettings,
                _ => CommonSettings
            };
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Genesis.Data;
using Genesis.Items;

namespace Genesis.Editor {
    /// <summary>
    /// Editor window that auto-generates Uncommon/Rare/Epic stats for EquipmentItemData assets.
    /// Pattern: 2 primary stats (class primary + secondary) + N random sub-stats per rarity.
    /// Weapons use combat-only sub-stats. Items without class get only sub-stats.
    /// </summary>
    public class ItemStatGenerator : EditorWindow {
        private ItemGenerationConfig config;
        private int selectedClassIndex;
        private string[] classOptions;

        [MenuItem("Genesis/Items/Generate Stats")]
        public static void ShowWindow() {
            var window = GetWindow<ItemStatGenerator>("Item Stat Generator");
            window.minSize = new Vector2(350, 200);
        }

        private void OnEnable() {
            LoadConfig();
            RebuildClassOptions();
        }

        private void LoadConfig() {
            string[] guids = AssetDatabase.FindAssets("t:ItemGenerationConfig");
            if (guids.Length > 0) {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                config = AssetDatabase.LoadAssetAtPath<ItemGenerationConfig>(path);
            }
        }

        private void RebuildClassOptions() {
            var options = new List<string> { "All" };
            if (config != null) {
                foreach (var mapping in config.ClassMappings) {
                    if (!string.IsNullOrEmpty(mapping.ClassName))
                        options.Add(mapping.ClassName);
                }
            }
            classOptions = options.ToArray();
            selectedClassIndex = 0;
        }

        private void OnGUI() {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Item Stat Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();
            config = (ItemGenerationConfig)EditorGUILayout.ObjectField("Config", config, typeof(ItemGenerationConfig), false);
            if (EditorGUI.EndChangeCheck()) {
                RebuildClassOptions();
            }

            if (config == null) {
                EditorGUILayout.HelpBox(
                    "No ItemGenerationConfig found. Create one via:\nAssets > Create > Genesis > Core > Item Generation Config",
                    MessageType.Warning);
                return;
            }

            if (config.RarityTiers.Count < 3) {
                EditorGUILayout.HelpBox(
                    "Config needs exactly 3 Rarity Tiers (Uncommon, Rare, Epic).",
                    MessageType.Error);
                return;
            }

            EditorGUILayout.Space(5);
            selectedClassIndex = EditorGUILayout.Popup("Filter by Class", selectedClassIndex, classOptions);

            EditorGUILayout.Space(15);

            if (GUILayout.Button("Generate Stats", GUILayout.Height(35))) {
                GenerateStats();
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Generates: Armor + 2 primary stats (class attrs) + N sub-stats per rarity.\n" +
                "Uncommon: +1 sub-stat | Rare: +2 sub-stats | Epic: +3 sub-stats\n" +
                "Weapons use combat-only sub-stats. Rings use ring pool (no Block/MoveSpeed, has MagicRes).\n" +
                "CommonStats get only Armor.",
                MessageType.Info);
        }

        private void GenerateStats() {
            string classFilter = selectedClassIndex == 0 ? null : classOptions[selectedClassIndex];

            string[] guids = AssetDatabase.FindAssets("t:EquipmentItemData");
            var items = new List<EquipmentItemData>();

            foreach (string guid in guids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<EquipmentItemData>(path);
                if (item != null)
                    items.Add(item);
            }

            int processed = 0;
            int skipped = 0;

            foreach (var item in items) {
                // Filter by class if selected
                if (classFilter != null) {
                    if (string.IsNullOrEmpty(item.RequiredClass) ||
                        !string.Equals(item.RequiredClass, classFilter, System.StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                }

                SlotCategory category = GetSlotCategory(item.Slot);
                bool hasClass = !string.IsNullOrEmpty(item.RequiredClass);
                ClassAttributeMapping? mapping = null;

                if (hasClass) {
                    mapping = config.GetClassMapping(item.RequiredClass);
                    if (mapping == null) {
                        Debug.LogWarning($"[ItemStatGenerator] Skipped '{item.ItemName}': class '{item.RequiredClass}' not found in ClassMappings.");
                        skipped++;
                        continue;
                    }
                }

                // Generate CommonStats with only Armor
                item.CommonStats = new List<StatModifier> {
                    new StatModifier(StatType.Armor, config.CommonArmorRange.RandomInt())
                };

                item.UncommonStats = GenerateStatsForRarity(ItemRarity.Uncommon, category, item.Slot, mapping);
                item.RareStats = GenerateStatsForRarity(ItemRarity.Rare, category, item.Slot, mapping);
                item.EpicStats = GenerateStatsForRarity(ItemRarity.Epic, category, item.Slot, mapping);

                EditorUtility.SetDirty(item);
                processed++;

                Debug.Log($"[ItemStatGenerator] '{item.ItemName}' ({category}{(hasClass ? ", " + item.RequiredClass : "")}) - U:{item.UncommonStats.Count} R:{item.RareStats.Count} E:{item.EpicStats.Count} stats");
            }

            AssetDatabase.SaveAssets();

            string filterMsg = classFilter != null ? $" (class: {classFilter})" : " (all classes)";
            Debug.Log($"<color=green>[ItemStatGenerator] Generated stats for {processed} items{filterMsg}. Skipped: {skipped}.</color>");
        }

        private List<StatModifier> GenerateStatsForRarity(ItemRarity rarity, SlotCategory category, EquipmentSlot slot, ClassAttributeMapping? mapping) {
            RarityTierConfig tier = config.GetTierConfig(rarity);
            var stats = new List<StatModifier>();

            // 1. Add primary + secondary attributes (only if item has a class)
            if (mapping.HasValue) {
                FloatRange primaryRange;
                FloatRange secondaryRange;

                switch (category) {
                    case SlotCategory.Complement:
                        primaryRange = tier.ComplementPrimary;
                        secondaryRange = tier.ComplementSecondary;
                        break;
                    case SlotCategory.Weapon:
                        primaryRange = tier.WeaponPrimary;
                        secondaryRange = tier.WeaponSecondary;
                        break;
                    default: // Armor and Accessory with class (Belt)
                        primaryRange = tier.ArmorPrimary;
                        secondaryRange = tier.ArmorSecondary;
                        break;
                }

                stats.Add(new StatModifier(mapping.Value.PrimaryAttribute, primaryRange.RandomInt()));
                stats.Add(new StatModifier(mapping.Value.SecondaryAttribute, secondaryRange.RandomInt()));
            }

            // 2. Add mandatory Armor stat
            stats.Add(new StatModifier(StatType.Armor, tier.ArmorRange.RandomInt()));

            // 3. Add random sub-stats (filtered by class exclusions)
            bool isWeapon = category == SlotCategory.Weapon;
            bool isRing = slot == EquipmentSlot.Ring1 || slot == EquipmentSlot.Ring2;
            List<StatType> pool = isWeapon ? config.CombatSubStatPool : (isRing ? config.RingSubStatPool : config.SubStatPool);

            if (mapping.HasValue && mapping.Value.ExcludedSubStats != null && mapping.Value.ExcludedSubStats.Count > 0) {
                pool = new List<StatType>(pool);
                foreach (var excluded in mapping.Value.ExcludedSubStats)
                    pool.Remove(excluded);
            }

            AddRandomSubStats(stats, pool, tier);

            return stats;
        }

        private void AddRandomSubStats(List<StatModifier> stats, List<StatType> pool, RarityTierConfig tier) {
            if (pool.Count == 0) return;

            int numStats = Mathf.Min(tier.NumSubStats, pool.Count);
            var available = new List<StatType>(pool);

            for (int i = 0; i < numStats; i++) {
                int idx = Random.Range(0, available.Count);
                StatType stat = available[idx];
                available.RemoveAt(idx);

                float value = ItemGenerationConfig.IsPercentageStat(stat)
                    ? tier.SubStatPercent.RandomValue()
                    : tier.SubStatFlat.RandomInt();

                stats.Add(new StatModifier(stat, value));
            }
        }

        private static SlotCategory GetSlotCategory(EquipmentSlot slot) {
            switch (slot) {
                case EquipmentSlot.Head:
                case EquipmentSlot.Shoulders:
                case EquipmentSlot.Chest:
                case EquipmentSlot.Pants:
                    return SlotCategory.Armor;

                case EquipmentSlot.Hands:
                case EquipmentSlot.Feet:
                    return SlotCategory.Complement;

                case EquipmentSlot.Belt:
                case EquipmentSlot.Ring1:
                case EquipmentSlot.Ring2:
                    return SlotCategory.Accessory;

                case EquipmentSlot.Weapon:
                case EquipmentSlot.OffHand:
                    return SlotCategory.Weapon;

                default:
                    return SlotCategory.Accessory;
            }
        }

        private enum SlotCategory {
            Armor,
            Complement,
            Accessory,
            Weapon
        }
    }
}

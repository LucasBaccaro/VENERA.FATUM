using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Genesis.Data;

namespace Genesis.EditorTools {

    public class AbilityImporter : EditorWindow {

        private const string JSON_PATH = "Assets/_Project/1_Data/Json/Abilities.json";
        private const string ASSET_PATH = "Assets/_Project/1_Data/Abilities/";

        [MenuItem("Genesis/Data/Import Abilities from JSON")]
        public static void ImportAbilities() {
            if (!File.Exists(JSON_PATH)) {
                Debug.LogError($"[AbilityImporter] Archivo JSON no encontrado en: {JSON_PATH}");
                return;
            }

            string json = File.ReadAllText(JSON_PATH);
            
            // Unity JsonUtility no soporta arrays top-level, así que envolvemos
            string wrappedJson = "{\"items\":" + json + "}";
            AbilityListWrapper wrapper = JsonUtility.FromJson<AbilityListWrapper>(wrappedJson);

            if (wrapper == null || wrapper.items == null) {
                Debug.LogError("[AbilityImporter] Error parseando JSON.");
                return;
            }

            if (!Directory.Exists(ASSET_PATH)) {
                Directory.CreateDirectory(ASSET_PATH);
            }

            int created = 0;
            int updated = 0;

            foreach (var dto in wrapper.items) {
                ImportAbility(dto, ref created, ref updated);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Actualizar Database automáticamente
            UpdateDatabase();

            Debug.Log($"[AbilityImporter] Importación completa. Creados: {created}, Actualizados: {updated}");
        }

        private static void ImportAbility(AbilityDTO dto, ref int created, ref int updated) {
            string fileName = $"Ability_{dto.Name}.asset";
            string fullPath = ASSET_PATH + fileName;

            AbilityData asset = AssetDatabase.LoadAssetAtPath<AbilityData>(fullPath);
            bool isNew = asset == null;

            if (isNew) {
                asset = ScriptableObject.CreateInstance<AbilityData>();
                created++;
            } else {
                updated++;
            }

            // Mapeo de datos
            asset.ID = dto.ID;
            asset.Name = dto.Name;
            asset.Description = dto.Description;
            asset.ManaCost = dto.ManaCost;
            asset.Cooldown = dto.Cooldown;
            asset.GCD = dto.GCD;
            asset.CastTime = dto.CastTime;
            asset.CanMoveWhileCasting = dto.CanMoveWhileCasting;
            asset.Range = dto.Range;
            asset.Radius = dto.Radius;
            asset.BaseDamage = dto.BaseDamage;
            asset.BaseHeal = dto.BaseHeal;
            asset.ProjectileSpeed = dto.ProjectileSpeed;

            // Enums (Parse string to enum)
            if (System.Enum.TryParse(dto.CastType, out CastingType ct)) asset.CastType = ct;
            if (System.Enum.TryParse(dto.TargetingMode, out TargetType tt)) asset.TargetingMode = tt;
            if (System.Enum.TryParse(dto.Category, out AbilityCategory ac)) asset.Category = ac;

            // Guardar Asset
            if (isNew) {
                AssetDatabase.CreateAsset(asset, fullPath);
            } else {
                EditorUtility.SetDirty(asset);
            }
        }

        private static void UpdateDatabase() {
            // Buscar la base de datos
            string[] guids = AssetDatabase.FindAssets("t:AbilityDatabase");
            if (guids.Length == 0) {
                Debug.LogWarning("[AbilityImporter] No se encontró AbilityDatabase para actualizar.");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            AbilityDatabase db = AssetDatabase.LoadAssetAtPath<AbilityDatabase>(path);
            
            if (db != null) {
                db.FindAllAbilities();
                Debug.Log("[AbilityImporter] AbilityDatabase actualizada.");
            }
        }

        // ═══════════════════════════════════════════════════════
        // DTOs (Data Transfer Objects)
        // ═══════════════════════════════════════════════════════

        [System.Serializable]
        private class AbilityListWrapper {
            public List<AbilityDTO> items;
        }

        [System.Serializable]
        private class AbilityDTO {
            public int ID;
            public string Name;
            public string Description;
            public float ManaCost;
            public float Cooldown;
            public float GCD;
            public string CastType;
            public float CastTime;
            public bool CanMoveWhileCasting;
            public string TargetingMode;
            public float Range;
            public float Radius;
            public string Category;
            public int BaseDamage;
            public int BaseHeal;
            public float ProjectileSpeed;
            public string LogicType;
        }
    }
}

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Genesis.Data;
using Genesis.Simulation.Combat;

namespace Genesis.EditorTools {

    public class AbilityImporter : EditorWindow {

        private const string JSON_PATH = "Assets/_Project/1_Data/Json/Abilities.json";
        private const string ASSET_PATH = "Assets/_Project/1_Data/Abilities/";
        private const string LOGIC_PATH = "Assets/_Project/1_Data/Abilities/Logic/";

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

            if (!Directory.Exists(LOGIC_PATH)) {
                Directory.CreateDirectory(LOGIC_PATH);
            }

            int created = 0;
            int updated = 0;
            int logicsCreated = 0;

            foreach (var dto in wrapper.items) {
                ImportAbility(dto, ref created, ref updated, ref logicsCreated);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Actualizar Database automáticamente
            UpdateDatabase();

            Debug.Log($"[AbilityImporter] Importación completa.\nAbilities - Creados: {created}, Actualizados: {updated}\nLogic Assets Creados: {logicsCreated}");
        }

        private static void ImportAbility(AbilityDTO dto, ref int created, ref int updated, ref int logicsCreated) {
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

            // NEW: Asignar IndicatorType automáticamente
            asset.IndicatorType = DetermineIndicatorType(dto);

            // NEW: Asignar o crear AbilityLogic
            asset.Logic = GetOrCreateLogic(dto.LogicType, ref logicsCreated);

            // Guardar Asset
            if (isNew) {
                AssetDatabase.CreateAsset(asset, fullPath);
            } else {
                EditorUtility.SetDirty(asset);
            }

            Debug.Log($"[AbilityImporter] {(isNew ? "Created" : "Updated")}: {dto.Name} (IndicatorType: {asset.IndicatorType}, Logic: {dto.LogicType})");
        }

        /// <summary>
        /// Determina el IndicatorType apropiado según el LogicType y TargetingMode
        /// </summary>
        private static IndicatorType DetermineIndicatorType(AbilityDTO dto) {
            // Si el LogicType es explícito, usarlo
            switch (dto.LogicType) {
                case "Skillshot":
                    return IndicatorType.Line;

                case "AoE":
                case "AOE":
                    // Distinguir entre Ground y Self
                    if (dto.TargetingMode == "Ground") {
                        return IndicatorType.Circle;
                    } else if (dto.TargetingMode == "Self") {
                        return IndicatorType.Circle;
                    }
                    return IndicatorType.Circle;

                case "SelfAOE":
                    return IndicatorType.Circle;

                case "Dash":
                    return IndicatorType.Arrow;

                case "Cone":
                    return IndicatorType.Cone;

                case "Trap":
                    return IndicatorType.Trap;

                case "Projectile":
                    // Projectile legacy: si es Enemy target, no necesita indicador (tab-target)
                    if (dto.TargetingMode == "Enemy") {
                        return IndicatorType.None;
                    }
                    // Si es ground-targeted, usar Line
                    return IndicatorType.Line;

                case "Direct":
                case "Melee":
                case "Targeted":
                default:
                    // Habilidades targeted (legacy) no necesitan indicador
                    return IndicatorType.None;
            }
        }

        /// <summary>
        /// Obtiene o crea un AbilityLogic asset del tipo especificado
        /// </summary>
        private static AbilityLogic GetOrCreateLogic(string logicType, ref int logicsCreated) {
            if (string.IsNullOrEmpty(logicType)) {
                logicType = "Targeted"; // Default
            }

            string logicAssetName = $"Logic_{logicType}";
            string logicPath = LOGIC_PATH + logicAssetName + ".asset";

            // Buscar asset existente
            AbilityLogic logic = AssetDatabase.LoadAssetAtPath<AbilityLogic>(logicPath);
            if (logic != null) {
                return logic;
            }

            // Crear nuevo Logic asset según tipo
            switch (logicType) {
                case "Projectile":
                    logic = ScriptableObject.CreateInstance<ProjectileLogic>();
                    break;

                case "Skillshot":
                    logic = ScriptableObject.CreateInstance<SkillshotLogic>();
                    break;

                case "AoE":
                case "AOE":
                    logic = ScriptableObject.CreateInstance<AOELogic>();
                    break;

                case "SelfAOE":
                    logic = ScriptableObject.CreateInstance<SelfAOELogic>();
                    break;

                case "Dash":
                    logic = ScriptableObject.CreateInstance<DashLogic>();
                    break;

                case "Cone":
                    logic = ScriptableObject.CreateInstance<ConeLogic>();
                    break;

                case "Trap":
                    logic = ScriptableObject.CreateInstance<TrapLogic>();
                    break;

                case "Direct":
                case "Melee":
                case "Targeted":
                default:
                    logic = ScriptableObject.CreateInstance<TargetedLogic>();
                    logicAssetName = "Logic_Targeted"; // Normalizar nombre
                    logicPath = LOGIC_PATH + logicAssetName + ".asset";
                    break;
            }

            if (logic != null) {
                AssetDatabase.CreateAsset(logic, logicPath);
                logicsCreated++;
                Debug.Log($"[AbilityImporter] Created Logic Asset: {logicAssetName}");
            }

            return logic;
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

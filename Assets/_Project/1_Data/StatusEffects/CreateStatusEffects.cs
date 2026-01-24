#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Genesis.Data;

namespace Genesis.Editor {

    /// <summary>
    /// Utility para crear los Status Effects iniciales del proyecto.
    /// Ejecutar: Tools > Genesis > Create Initial Status Effects
    /// </summary>
    public static class CreateStatusEffects {

        [MenuItem("Tools/Genesis/Create Initial Status Effects")]
        public static void CreateInitialEffects() {
            string basePath = "Assets/_Project/1_Data/StatusEffects";

            // Asegurar que el directorio existe
            if (!AssetDatabase.IsValidFolder(basePath)) {
                Debug.LogError($"Folder {basePath} does not exist!");
                return;
            }

            CreateStun(basePath);
            CreateRoot(basePath);
            CreateSlow(basePath);
            CreateSilence(basePath);
            CreateShield(basePath);
            CreateReflect(basePath);
            CreateInvulnerable(basePath);
            CreatePoison(basePath);
            CreateRegen(basePath);
            CreateSpeed(basePath);
            CreateHaste(basePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CreateStatusEffects] Successfully created 11 status effects!");
        }

        // ═══════════════════════════════════════════════════════
        // CROWD CONTROL (Debuffs)
        // ═══════════════════════════════════════════════════════

        static void CreateStun(string basePath) {
            var effect = ScriptableObject.CreateInstance<StatusEffectData>();
            effect.ID = 1;
            effect.Name = "Stun";
            effect.Description = "Cannot move or cast abilities.";
            effect.Type = EffectType.Stun;
            effect.IsBuff = false;
            effect.Duration = 2f;
            effect.IsStackable = false;
            effect.MaxStacks = 1;

            AssetDatabase.CreateAsset(effect, $"{basePath}/Effect_Stun.asset");
            Debug.Log("Created: Effect_Stun");
        }

        static void CreateRoot(string basePath) {
            var effect = ScriptableObject.CreateInstance<StatusEffectData>();
            effect.ID = 2;
            effect.Name = "Root";
            effect.Description = "Cannot move but can cast abilities.";
            effect.Type = EffectType.Root;
            effect.IsBuff = false;
            effect.Duration = 3f;
            effect.IsStackable = false;
            effect.MaxStacks = 1;

            AssetDatabase.CreateAsset(effect, $"{basePath}/Effect_Root.asset");
            Debug.Log("Created: Effect_Root");
        }

        static void CreateSlow(string basePath) {
            var effect = ScriptableObject.CreateInstance<StatusEffectData>();
            effect.ID = 3;
            effect.Name = "Slow";
            effect.Description = "Movement speed reduced by 50%.";
            effect.Type = EffectType.Slow;
            effect.IsBuff = false;
            effect.Duration = 5f;
            effect.IsStackable = false;
            effect.MaxStacks = 1;
            effect.PercentageValue = 0.5f; // 50% slower

            AssetDatabase.CreateAsset(effect, $"{basePath}/Effect_Slow.asset");
            Debug.Log("Created: Effect_Slow");
        }

        static void CreateSilence(string basePath) {
            var effect = ScriptableObject.CreateInstance<StatusEffectData>();
            effect.ID = 4;
            effect.Name = "Silence";
            effect.Description = "Cannot cast magical abilities.";
            effect.Type = EffectType.Silence;
            effect.IsBuff = false;
            effect.Duration = 4f;
            effect.IsStackable = false;
            effect.MaxStacks = 1;

            AssetDatabase.CreateAsset(effect, $"{basePath}/Effect_Silence.asset");
            Debug.Log("Created: Effect_Silence");
        }

        // ═══════════════════════════════════════════════════════
        // DEFENSIVE BUFFS
        // ═══════════════════════════════════════════════════════

        static void CreateShield(string basePath) {
            var effect = ScriptableObject.CreateInstance<StatusEffectData>();
            effect.ID = 10;
            effect.Name = "Arcane Shield";
            effect.Description = "Absorbs up to 50 damage.";
            effect.Type = EffectType.Shield;
            effect.IsBuff = true;
            effect.Duration = 10f;
            effect.IsStackable = false;
            effect.MaxStacks = 1;
            effect.FlatValue = 50f; // 50 HP shield

            AssetDatabase.CreateAsset(effect, $"{basePath}/Effect_Shield.asset");
            Debug.Log("Created: Effect_Shield");
        }

        static void CreateReflect(string basePath) {
            var effect = ScriptableObject.CreateInstance<StatusEffectData>();
            effect.ID = 11;
            effect.Name = "Projectile Reflection";
            effect.Description = "Reflects the next projectile back to the attacker.";
            effect.Type = EffectType.Reflect;
            effect.IsBuff = true;
            effect.Duration = 5f;
            effect.IsStackable = false;
            effect.MaxStacks = 1;

            AssetDatabase.CreateAsset(effect, $"{basePath}/Effect_Reflect.asset");
            Debug.Log("Created: Effect_Reflect");
        }

        static void CreateInvulnerable(string basePath) {
            var effect = ScriptableObject.CreateInstance<StatusEffectData>();
            effect.ID = 12;
            effect.Name = "Divine Invulnerability";
            effect.Description = "Immune to all damage and debuffs.";
            effect.Type = EffectType.Invulnerable;
            effect.IsBuff = true;
            effect.Duration = 3f;
            effect.IsStackable = false;
            effect.MaxStacks = 1;

            AssetDatabase.CreateAsset(effect, $"{basePath}/Effect_Invulnerable.asset");
            Debug.Log("Created: Effect_Invulnerable");
        }

        // ═══════════════════════════════════════════════════════
        // DAMAGE OVER TIME (DoT) & HEAL OVER TIME (HoT)
        // ═══════════════════════════════════════════════════════

        static void CreatePoison(string basePath) {
            var effect = ScriptableObject.CreateInstance<StatusEffectData>();
            effect.ID = 20;
            effect.Name = "Poison";
            effect.Description = "Takes 5 damage every second.";
            effect.Type = EffectType.Poison;
            effect.IsBuff = false;
            effect.Duration = 6f;
            effect.IsStackable = true;
            effect.MaxStacks = 3;
            effect.FlatValue = 5f; // 5 damage per tick
            effect.TickInterval = 1f; // Every 1 second

            AssetDatabase.CreateAsset(effect, $"{basePath}/Effect_Poison.asset");
            Debug.Log("Created: Effect_Poison");
        }

        static void CreateRegen(string basePath) {
            var effect = ScriptableObject.CreateInstance<StatusEffectData>();
            effect.ID = 21;
            effect.Name = "Regeneration";
            effect.Description = "Restores 10 HP every 2 seconds.";
            effect.Type = EffectType.Regen;
            effect.IsBuff = true;
            effect.Duration = 10f;
            effect.IsStackable = false;
            effect.MaxStacks = 1;
            effect.FlatValue = 10f; // 10 HP per tick
            effect.TickInterval = 2f; // Every 2 seconds

            AssetDatabase.CreateAsset(effect, $"{basePath}/Effect_Regen.asset");
            Debug.Log("Created: Effect_Regen");
        }

        // ═══════════════════════════════════════════════════════
        // MOVEMENT & CAST SPEED BUFFS
        // ═══════════════════════════════════════════════════════

        static void CreateSpeed(string basePath) {
            var effect = ScriptableObject.CreateInstance<StatusEffectData>();
            effect.ID = 30;
            effect.Name = "Speed Boost";
            effect.Description = "Movement speed increased by 30%.";
            effect.Type = EffectType.Speed;
            effect.IsBuff = true;
            effect.Duration = 8f;
            effect.IsStackable = false;
            effect.MaxStacks = 1;
            effect.PercentageValue = 0.3f; // +30% speed

            AssetDatabase.CreateAsset(effect, $"{basePath}/Effect_Speed.asset");
            Debug.Log("Created: Effect_Speed");
        }

        static void CreateHaste(string basePath) {
            var effect = ScriptableObject.CreateInstance<StatusEffectData>();
            effect.ID = 31;
            effect.Name = "Haste";
            effect.Description = "Cast speed and attack speed increased by 25%.";
            effect.Type = EffectType.Haste;
            effect.IsBuff = true;
            effect.Duration = 12f;
            effect.IsStackable = false;
            effect.MaxStacks = 1;
            effect.PercentageValue = 0.25f; // +25% cast/attack speed

            AssetDatabase.CreateAsset(effect, $"{basePath}/Effect_Haste.asset");
            Debug.Log("Created: Effect_Haste");
        }
    }
}
#endif

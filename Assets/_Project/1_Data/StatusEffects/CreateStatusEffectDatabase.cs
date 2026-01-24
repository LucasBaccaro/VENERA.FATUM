#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Genesis.Data;

namespace Genesis.Editor {

    /// <summary>
    /// Utility para crear la base de datos de Status Effects.
    /// Ejecutar: Tools > Genesis > Create Status Effect Database
    /// </summary>
    public static class CreateStatusEffectDatabaseUtility {

        [MenuItem("Tools/Genesis/Create Status Effect Database")]
        public static void CreateDatabase() {
            string path = "Assets/_Project/1_Data/Resources/Databases/StatusEffectDatabase.asset";

            // Verificar si ya existe
            StatusEffectDatabase existing = AssetDatabase.LoadAssetAtPath<StatusEffectDatabase>(path);
            if (existing != null) {
                Debug.LogWarning($"[StatusEffectDatabase] Database already exists at {path}. Updating instead.");
                existing.FindAllStatusEffects();
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return;
            }

            // Crear directorio si no existe
            string directory = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(directory)) {
                System.IO.Directory.CreateDirectory(directory);
            }

            // Crear nuevo database
            StatusEffectDatabase database = ScriptableObject.CreateInstance<StatusEffectDatabase>();
            AssetDatabase.CreateAsset(database, path);

            // Auto-find effects
            database.FindAllStatusEffects();

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StatusEffectDatabase] Created successfully at {path} with {database.GetAllEffects().Count} effects.");
        }
    }
}
#endif

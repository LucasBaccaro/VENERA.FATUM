using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace Genesis.Editor.Dungeon
{
    /// <summary>
    /// Persists a registry of FBX → OutputPath mappings so that whenever a registered
    /// FBX is reimported, PropExtractorCore automatically re-extracts and updates all
    /// prop prefabs derived from it.
    /// </summary>
    [System.Serializable]
    public class PropFBXEntry
    {
        public string FbxPath;
        public string OutputPath;
        public string Layer;
        public bool   AddBoxCollider;
        public string MaterialPath; // Asset path of the override material (empty = use FBX materials)
    }

    [System.Serializable]
    public class PropFBXRegistry
    {
        public List<PropFBXEntry> Entries = new List<PropFBXEntry>();
    }

    /// <summary>
    /// AssetPostprocessor that watches reimports and triggers prop extraction
    /// for all registered FBXs.
    /// </summary>
    public class PropFBXPostprocessor : AssetPostprocessor
    {
        // ── Registry path (project-relative, inside ProjectSettings so it is not an asset) ──
        private static readonly string RegistryPath =
            Path.Combine(Application.dataPath, "..", "ProjectSettings", "PropFBXRegistry.json");

        // ── In-memory cache (invalidated when registry file changes) ──
        private static PropFBXRegistry _cache;
        private static long _cacheWriteTime = -1;

        // ─────────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────────

        public static void RegisterFBX(string fbxPath, string outputPath, string layer, bool addBoxCollider, Material overrideMaterial = null)
        {
            var reg = LoadRegistry();
            var existing = reg.Entries.Find(e => e.FbxPath == fbxPath);
            if (existing != null)
            {
                existing.OutputPath     = outputPath;
                existing.Layer          = layer;
                existing.AddBoxCollider  = addBoxCollider;
                existing.MaterialPath   = overrideMaterial != null ? AssetDatabase.GetAssetPath(overrideMaterial) : string.Empty;
                Debug.Log($"[PropExtractor] Updated registration for: {fbxPath}");
            }
            else
            {
                reg.Entries.Add(new PropFBXEntry
                {
                    FbxPath        = fbxPath,
                    OutputPath     = outputPath,
                    Layer          = layer,
                    AddBoxCollider  = addBoxCollider,
                    MaterialPath   = overrideMaterial != null ? AssetDatabase.GetAssetPath(overrideMaterial) : string.Empty
                });
                Debug.Log($"[PropExtractor] Registered FBX for auto-update: {fbxPath}");
            }
            SaveRegistry(reg);
        }

        public static void UnregisterFBX(string fbxPath)
        {
            var reg = LoadRegistry();
            int removed = reg.Entries.RemoveAll(e => e.FbxPath == fbxPath);
            if (removed > 0)
            {
                SaveRegistry(reg);
                Debug.Log($"[PropExtractor] Unregistered: {fbxPath}");
            }
        }

        public static List<PropFBXEntry> GetRegisteredEntries()
        {
            return LoadRegistry().Entries;
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  AssetPostprocessor callback
        // ─────────────────────────────────────────────────────────────────────────

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var reg = LoadRegistry();
            if (reg.Entries.Count == 0) return;

            foreach (string path in importedAssets)
            {
                // Only react to FBX / model file reimports
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".fbx" && ext != ".obj" && ext != ".dae" && ext != ".blend") continue;

                var entry = reg.Entries.Find(e => e.FbxPath == path);
                if (entry == null) continue;

                Debug.Log($"[PropExtractor] FBX reimported — auto-updating props for: {path}");
                Material mat = string.IsNullOrEmpty(entry.MaterialPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<Material>(entry.MaterialPath);
                PropExtractorCore.Extract(entry.FbxPath, entry.OutputPath, entry.Layer, entry.AddBoxCollider, mat);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Registry I/O
        // ─────────────────────────────────────────────────────────────────────────

        private static PropFBXRegistry LoadRegistry()
        {
            string absPath = Path.GetFullPath(RegistryPath);

            // Return cache if still valid
            if (_cache != null && File.Exists(absPath))
            {
                long writeTime = File.GetLastWriteTime(absPath).Ticks;
                if (writeTime == _cacheWriteTime) return _cache;
            }

            if (!File.Exists(absPath))
            {
                _cache = new PropFBXRegistry();
                _cacheWriteTime = -1;
                return _cache;
            }

            try
            {
                string json = File.ReadAllText(absPath);
                _cache = JsonUtility.FromJson<PropFBXRegistry>(json) ?? new PropFBXRegistry();
                _cacheWriteTime = File.GetLastWriteTime(absPath).Ticks;
            }
            catch
            {
                _cache = new PropFBXRegistry();
                _cacheWriteTime = -1;
            }

            return _cache;
        }

        private static void SaveRegistry(PropFBXRegistry reg)
        {
            string absPath = Path.GetFullPath(RegistryPath);
            string dir     = Path.GetDirectoryName(absPath);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonUtility.ToJson(reg, true);
            File.WriteAllText(absPath, json);

            _cache = reg;
            _cacheWriteTime = File.GetLastWriteTime(absPath).Ticks;
        }
    }
}

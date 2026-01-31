using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

namespace Genesis.Editor
{
    /// <summary>
    /// Utility to automatically add all chunk scenes to Build Settings.
    /// Menu: Tools > World Streaming > Add Chunk Scenes to Build
    /// </summary>
    public static class ChunkSceneBuilder
    {
        [MenuItem("Tools/World Streaming/Add Chunk Scenes to Build Settings")]
        public static void AddChunkScenesToBuild()
        {
            // Find all scenes in Chunks folder
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/5_Content/Scenes/Chunks" });

            if (guids.Length == 0)
            {
                Debug.LogWarning("[ChunkSceneBuilder] No chunk scenes found in Assets/_Project/5_Content/Scenes/Chunks");
                return;
            }

            // Get current build scenes
            var buildScenes = EditorBuildSettings.scenes.ToList();
            int addedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Check if already in build settings
                if (buildScenes.Any(s => s.path == path))
                {
                    continue; // Already added
                }

                // Add to build settings
                buildScenes.Add(new EditorBuildSettingsScene(path, true));
                addedCount++;
                Debug.Log($"[ChunkSceneBuilder] Added to build: {path}");
            }

            // Update build settings
            EditorBuildSettings.scenes = buildScenes.ToArray();

            Debug.Log($"[ChunkSceneBuilder] ✅ Added {addedCount} chunk scenes to Build Settings (Total: {guids.Length} chunks found)");

            if (addedCount == 0)
            {
                Debug.Log("[ChunkSceneBuilder] All chunk scenes already in Build Settings.");
            }
        }

        [MenuItem("Tools/World Streaming/Remove All Chunk Scenes from Build Settings")]
        public static void RemoveChunkScenesFromBuild()
        {
            // Get current build scenes
            var buildScenes = EditorBuildSettings.scenes.ToList();
            int removedCount = 0;

            // Remove any scene with "Chunk_" in the path
            buildScenes.RemoveAll(scene =>
            {
                if (scene.path.Contains("/Chunks/Chunk_"))
                {
                    Debug.Log($"[ChunkSceneBuilder] Removed from build: {scene.path}");
                    removedCount++;
                    return true;
                }
                return false;
            });

            // Update build settings
            EditorBuildSettings.scenes = buildScenes.ToArray();

            Debug.Log($"[ChunkSceneBuilder] ✅ Removed {removedCount} chunk scenes from Build Settings");
        }

        [MenuItem("Tools/World Streaming/List All Chunk Scenes in Build Settings")]
        public static void ListChunkScenesInBuild()
        {
            var chunkScenes = EditorBuildSettings.scenes
                .Where(s => s.path.Contains("/Chunks/Chunk_"))
                .ToList();

            Debug.Log($"[ChunkSceneBuilder] === Chunk Scenes in Build Settings ({chunkScenes.Count}) ===");

            foreach (var scene in chunkScenes)
            {
                string status = scene.enabled ? "✅" : "❌";
                Debug.Log($"{status} {scene.path}");
            }

            if (chunkScenes.Count == 0)
            {
                Debug.LogWarning("[ChunkSceneBuilder] No chunk scenes found in Build Settings. Use 'Add Chunk Scenes to Build Settings' first.");
            }
        }
    }
}

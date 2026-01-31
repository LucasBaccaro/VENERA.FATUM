using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Genesis.Data;
using System.Linq;

namespace Genesis.Editor
{
    public static class WorldStreamingDebugger
    {
        [MenuItem("Tools/World Streaming/Debug: Verify World Setup")]
        public static void VerifyWorldSetup()
        {
            Debug.Log("═══════════════════════════════════════════════════");
            Debug.Log("     WORLD STREAMING SYSTEM - VERIFICATION");
            Debug.Log("═══════════════════════════════════════════════════");

            // 1. Check WorldDatabase
            Debug.Log("\n[1] Checking WorldDatabase...");
            WorldDatabase worldDB = Resources.Load<WorldDatabase>("Databases/WorldDatabase");
            if (worldDB == null)
            {
                Debug.LogError("❌ WorldDatabase NOT FOUND at Resources/Databases/WorldDatabase!");
                return;
            }
            Debug.Log($"✅ WorldDatabase found: {worldDB.name}");

            // Initialize to access chunks
            worldDB.Initialize();

            // 2. Check ChunkData assets
            Debug.Log("\n[2] Checking ChunkData assets...");
            var chunkDataGuids = AssetDatabase.FindAssets("t:ChunkData");
            Debug.Log($"Found {chunkDataGuids.Length} ChunkData assets in project");

            foreach (var guid in chunkDataGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ChunkData chunkData = AssetDatabase.LoadAssetAtPath<ChunkData>(path);

                string status = "";
                if (string.IsNullOrEmpty(chunkData.SceneName))
                {
                    status = "❌ EMPTY SceneName";
                }
                else
                {
                    // Check if scene exists in Build Settings
                    bool inBuild = EditorBuildSettings.scenes.Any(s => s.path.Contains(chunkData.SceneName));
                    status = inBuild ? "✅" : "⚠️ NOT IN BUILD SETTINGS";
                }

                Debug.Log($"  {status} | {chunkData.name} | Coord: ({chunkData.Coordinate.x}, {chunkData.Coordinate.y}) | Scene: '{chunkData.SceneName}' | Starting: {chunkData.IsStartingChunk}");
            }

            // 3. Check Build Settings scenes
            Debug.Log("\n[3] Checking Build Settings (Chunk scenes)...");
            var chunkScenes = EditorBuildSettings.scenes.Where(s => s.path.Contains("/Chunks/Chunk_")).ToList();
            Debug.Log($"Found {chunkScenes.Count} chunk scenes in Build Settings:");

            foreach (var scene in chunkScenes)
            {
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scene.path);
                string status = scene.enabled ? "✅" : "❌ DISABLED";
                Debug.Log($"  {status} | {sceneName} | Path: {scene.path}");
            }

            // 4. Check Layer 9
            Debug.Log("\n[4] Checking Layer 9 (SafeZone)...");
            string layerName = LayerMask.LayerToName(9);
            if (layerName == "SafeZone")
            {
                Debug.Log("✅ Layer 9 is named 'SafeZone'");
            }
            else if (string.IsNullOrEmpty(layerName))
            {
                Debug.LogError("❌ Layer 9 is NOT CONFIGURED! Go to Project Settings > Tags and Layers");
            }
            else
            {
                Debug.LogWarning($"⚠️ Layer 9 is named '{layerName}' (expected 'SafeZone')");
            }

            // 5. Check for SafeZone triggers in scenes
            Debug.Log("\n[5] Checking for ZoneTrigger components in chunk scenes...");
            var zoneTriggerGuids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/_Project/5_Content/Scenes/Chunks" });
            Debug.Log($"Searching in {zoneTriggerGuids.Length} chunk scene files...");
            Debug.Log("(Open chunk scenes manually to see ZoneTriggers - they're runtime objects)");

            Debug.Log("\n═══════════════════════════════════════════════════");
            Debug.Log("     VERIFICATION COMPLETE");
            Debug.Log("═══════════════════════════════════════════════════\n");
        }

        [MenuItem("Tools/World Streaming/Debug: List All ChunkData in WorldDatabase")]
        public static void ListWorldDatabaseChunks()
        {
            WorldDatabase worldDB = Resources.Load<WorldDatabase>("Databases/WorldDatabase");
            if (worldDB == null)
            {
                Debug.LogError("❌ WorldDatabase NOT FOUND!");
                return;
            }

            worldDB.Initialize();

            Debug.Log("═══════════════════════════════════════════════════");
            Debug.Log("     CHUNKS IN WORLDDATABASE");
            Debug.Log("═══════════════════════════════════════════════════");

            // Try to get all chunks by iterating coordinates
            for (int x = -5; x <= 5; x++)
            {
                for (int y = -5; y <= 5; y++)
                {
                    ChunkData chunk = worldDB.GetChunk(new Vector2Int(x, y));
                    if (chunk != null)
                    {
                        string startingIcon = chunk.IsStartingChunk ? "🏠" : "  ";
                        Debug.Log($"{startingIcon} Chunk({x}, {y}) | Name: {chunk.ChunkName} | Scene: {chunk.SceneName} | Spawns: {chunk.SpawnPositions.Length}");
                    }
                }
            }

            Debug.Log("═══════════════════════════════════════════════════\n");
        }

        [MenuItem("Tools/World Streaming/Debug: Test Chunk Coordinate Math")]
        public static void TestChunkCoordinates()
        {
            Debug.Log("═══════════════════════════════════════════════════");
            Debug.Log("     CHUNK COORDINATE TESTS");
            Debug.Log("═══════════════════════════════════════════════════");

            // Test positions
            Vector3[] testPositions = new Vector3[]
            {
                new Vector3(0, 0, 0),       // Should be Chunk(0, 0)
                new Vector3(128, 0, 128),   // Should be Chunk(0, 0) - center
                new Vector3(255, 0, 255),   // Should be Chunk(0, 0) - edge
                new Vector3(256, 0, 0),     // Should be Chunk(1, 0)
                new Vector3(0, 0, 256),     // Should be Chunk(0, 1)
                new Vector3(256, 0, 256),   // Should be Chunk(1, 1)
                new Vector3(-1, 0, 0),      // Should be Chunk(-1, 0)
                new Vector3(0, 0, -1),      // Should be Chunk(0, -1)
            };

            foreach (var pos in testPositions)
            {
                var chunk = Genesis.Core.ChunkCoordinate.FromWorldPosition(pos);
                Debug.Log($"Position {pos} → Chunk({chunk.X}, {chunk.Y})");
            }

            Debug.Log("\n--- Testing 9-slice grid from Chunk(0, 1) ---");
            var centerChunk = new Genesis.Core.ChunkCoordinate(0, 1);
            var neighbors = centerChunk.GetNeighbors();
            Debug.Log($"Center: {centerChunk}");
            Debug.Log($"Neighbors (8): {string.Join(", ", neighbors)}");
            Debug.Log($"Total required (9-slice): {centerChunk} + {neighbors.Count} neighbors = 9 chunks");

            Debug.Log("═══════════════════════════════════════════════════\n");
        }

        [MenuItem("Tools/World Streaming/Debug: Check Player Prefab Components")]
        public static void CheckPlayerPrefab()
        {
            Debug.Log("═══════════════════════════════════════════════════");
            Debug.Log("     PLAYER PREFAB COMPONENT CHECK");
            Debug.Log("═══════════════════════════════════════════════════");

            // Find player prefab
            var guids = AssetDatabase.FindAssets("t:Prefab Player");
            if (guids.Length == 0)
            {
                Debug.LogError("❌ No Player prefab found!");
                return;
            }

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                Debug.Log($"\nChecking: {prefab.name} ({path})");

                bool hasNetworkObject = prefab.GetComponent<FishNet.Object.NetworkObject>() != null;
                bool hasPlayerChunkTracker = prefab.GetComponent("PlayerChunkTracker") != null;
                bool hasPlayerState = prefab.GetComponent("PlayerState") != null;
                bool hasPlayerSpawnHandler = prefab.GetComponent("PlayerSpawnHandler") != null;

                Debug.Log($"  {(hasNetworkObject ? "✅" : "❌")} NetworkObject");
                Debug.Log($"  {(hasPlayerChunkTracker ? "✅" : "❌")} PlayerChunkTracker");
                Debug.Log($"  {(hasPlayerState ? "✅" : "❌")} PlayerState");
                Debug.Log($"  {(hasPlayerSpawnHandler ? "✅" : "❌")} PlayerSpawnHandler");

                if (!hasPlayerChunkTracker || !hasPlayerState || !hasPlayerSpawnHandler)
                {
                    Debug.LogError($"⚠️ Player prefab is missing required components for World Streaming!");
                }
            }

            Debug.Log("═══════════════════════════════════════════════════\n");
        }
    }
}

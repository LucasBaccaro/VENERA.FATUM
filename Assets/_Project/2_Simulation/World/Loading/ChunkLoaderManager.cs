using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Genesis.Core;
using Genesis.Data;


namespace Genesis.Simulation.World
{
    public class ChunkLoaderManager : MonoBehaviour
    {
        private HashSet<ChunkCoordinate> _loadedChunks = new HashSet<ChunkCoordinate>();
        private Dictionary<ChunkCoordinate, AsyncOperation> _loadOperations = new Dictionary<ChunkCoordinate, AsyncOperation>();

        private WorldDatabase _worldDB;
        private bool _isServer;

        // Tracks the active LOOKS environment prefab instance (city, dungeon, etc.)
        private GameObject _activeLooksInstance;
        private ChunkCoordinate _currentPlayerChunk;

        void Start()
        {
            _worldDB = ServiceLocator.Instance.Get<WorldDatabase>();
            _isServer = FishNet.InstanceFinder.IsServer;

            EventBus.Subscribe<ChunkCoordinate>(WorldStreamingEvents.PLAYER_CHUNK_CHANGED, OnPlayerChunkChanged);
        }

        private void OnPlayerChunkChanged(ChunkCoordinate newChunk)
        {
            Debug.Log($"<color=cyan>[ChunkLoader] ═══ PLAYER MOVED TO CHUNK {newChunk} ═══</color>");

            _currentPlayerChunk = newChunk;

            HashSet<ChunkCoordinate> requiredChunks = new HashSet<ChunkCoordinate>();
            requiredChunks.Add(newChunk);
            requiredChunks.UnionWith(newChunk.GetNeighbors());

            Debug.Log($"[ChunkLoader] Required chunks (9-slice): {string.Join(", ", requiredChunks)}");
            Debug.Log($"[ChunkLoader] Currently loaded: {string.Join(", ", _loadedChunks)}");

            var chunksToUnload = _loadedChunks.Except(requiredChunks);
            Debug.Log($"[ChunkLoader] Chunks to UNLOAD: {string.Join(", ", chunksToUnload)}");
            foreach (var chunk in chunksToUnload.ToList())
            {
                UnloadChunk(chunk);
            }

            var chunksToLoad = requiredChunks.Except(_loadedChunks);
            Debug.Log($"[ChunkLoader] Chunks to LOAD: {string.Join(", ", chunksToLoad)}");
            foreach (var chunk in chunksToLoad.ToList())
            {
                LoadChunk(chunk);
            }

            // Swap the LOOKS prefab for the chunk the player is directly in
            SwapLooksPrefab(newChunk);

            Debug.Log($"<color=cyan>[ChunkLoader] ═══ END CHUNK UPDATE ═══</color>");
        }

        private void SwapLooksPrefab(ChunkCoordinate coord)
        {
            ChunkData data = _worldDB.GetChunk(new Vector2Int(coord.X, coord.Y));
            if (data == null) return;

            // Destroy previous LOOKS instance
            if (_activeLooksInstance != null)
            {
                Destroy(_activeLooksInstance);
                _activeLooksInstance = null;
                Debug.Log("[ChunkLoader] Destroyed previous LOOKS instance.");
            }

            // Instantiate new LOOKS prefab (if defined for this chunk)
            if (data.LooksPrefab != null)
            {
                _activeLooksInstance = Instantiate(data.LooksPrefab);
                Debug.Log($"[ChunkLoader] Instantiated LOOKS prefab '{data.LooksPrefab.name}' for chunk {coord}.");

                // Apply render settings if the prefab has a LooksSettings component
                if (_activeLooksInstance.TryGetComponent<LooksSettings>(out var looksSettings))
                {
                    looksSettings.Apply();
                }
                else
                {
                    Debug.LogWarning($"[ChunkLoader] LOOKS prefab '{data.LooksPrefab.name}' has no LooksSettings component. RenderSettings won't change.");
                }
            }
            else
            {
                Debug.LogWarning($"[ChunkLoader] No LooksPrefab assigned for chunk {coord}. Environment will be empty.");
            }
        }

        private void LoadChunk(ChunkCoordinate coord)
        {
            Debug.Log($"[ChunkLoader] 📥 Attempting to load chunk {coord}...");

            ChunkData data = _worldDB.GetChunk(new Vector2Int(coord.X, coord.Y));
            if (data == null)
            {
                Debug.LogWarning($"[ChunkLoader] ⚠️ No ChunkData for {coord} (chunk doesn't exist, skipping)");
                return;
            }

            Debug.Log($"[ChunkLoader] Found ChunkData: {data.name} | SceneName: '{data.SceneName}' | Coordinate: {data.Coordinate}");

            if (string.IsNullOrEmpty(data.SceneName))
            {
                Debug.LogError($"<color=red>[ChunkLoader] ❌ ChunkData for {coord} has empty SceneName!</color>");
                return;
            }

            // Check if scene is already loaded
            Scene existingScene = SceneManager.GetSceneByName(data.SceneName);
            if (existingScene.isLoaded)
            {
                Debug.LogWarning($"[ChunkLoader] ⚠️ Scene {data.SceneName} already loaded, skipping.");
                _loadedChunks.Add(coord);
                return;
            }

            Debug.Log($"[ChunkLoader] Starting async load for scene: {data.SceneName}");

            // Start async scene load
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(data.SceneName, LoadSceneMode.Additive);
            if (asyncLoad == null)
            {
                Debug.LogError($"<color=red>[ChunkLoader] ❌ LoadSceneAsync returned null for {data.SceneName}! Scene not in Build Settings?</color>");
                return;
            }

            _loadOperations[coord] = asyncLoad;
            StartCoroutine(WaitForSceneLoad(coord, asyncLoad, data.SceneName));
        }

        private IEnumerator WaitForSceneLoad(ChunkCoordinate coord, AsyncOperation asyncLoad, string sceneName)
        {
            yield return asyncLoad;

            if (asyncLoad.isDone)
            {
                _loadedChunks.Add(coord);
                
                // Set the loaded scene as active to ensure lighting settings (baked data, ambient, etc.) are applied
                Scene loadedScene = SceneManager.GetSceneByName(sceneName);
                if (loadedScene.IsValid() && loadedScene.isLoaded)
                {
                    SceneManager.SetActiveScene(loadedScene);
                    Debug.Log($"[ChunkLoader] Set active scene to: {sceneName}");
                }

                EventBus.Trigger(WorldStreamingEvents.CHUNK_LOAD_COMPLETED, coord);
                Debug.Log($"[ChunkLoader] Loaded chunk {coord} - Scene: {sceneName} ({(_isServer ? "Server" : "Client")})");
            }
            else
            {
                Debug.LogError($"[ChunkLoader] Failed to load chunk {coord} - Scene: {sceneName}");
            }
        }

        private void UnloadChunk(ChunkCoordinate coord)
        {
            if (!_loadedChunks.Contains(coord)) return;

            ChunkData data = _worldDB.GetChunk(new Vector2Int(coord.X, coord.Y));
            if (data == null || string.IsNullOrEmpty(data.SceneName)) return;

            Scene scene = SceneManager.GetSceneByName(data.SceneName);
            if (scene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(scene);
                Debug.Log($"[ChunkLoader] Unloaded chunk {coord} - Scene: {data.SceneName}");
            }

            _loadOperations.Remove(coord);
            _loadedChunks.Remove(coord);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<ChunkCoordinate>(WorldStreamingEvents.PLAYER_CHUNK_CHANGED, OnPlayerChunkChanged);
        }
    }
}

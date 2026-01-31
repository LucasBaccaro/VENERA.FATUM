using UnityEngine;
using Genesis.Core;
using Genesis.Core.Networking;
using Genesis.Data;

namespace Genesis.Simulation.World
{
    /// <summary>
    /// Provides spawn positions from WorldDatabase chunks.
    /// Registered with ServiceLocator to provide chunk-aware spawning to PlayerSpawnManager.
    /// </summary>
    public class WorldSpawnProvider : ISpawnPositionProvider
    {
        private WorldDatabase _worldDB;

        public WorldSpawnProvider(WorldDatabase worldDB)
        {
            _worldDB = worldDB;
        }

        public Vector3 GetSpawnPosition()
        {
            if (_worldDB == null)
            {
                Debug.LogError("[WorldSpawnProvider] WorldDatabase is null!");
                return Vector3.zero;
            }

            ChunkData startingChunk = _worldDB.GetRandomStartingChunk();
            if (startingChunk == null || startingChunk.SpawnPositions.Length == 0)
            {
                Debug.LogError("[WorldSpawnProvider] No valid starting chunk with spawn positions!");
                return Vector3.zero;
            }

            // Pick random spawn position from chunk
            Vector3 spawnPos = startingChunk.SpawnPositions[Random.Range(0, startingChunk.SpawnPositions.Length)];

            Debug.Log($"[WorldSpawnProvider] Providing spawn position from chunk {startingChunk.ChunkName}: {spawnPos}");

            return spawnPos;
        }
    }
}

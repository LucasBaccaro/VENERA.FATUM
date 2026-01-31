using UnityEngine;
using FishNet.Object;
using Genesis.Core;
using Genesis.Core.Networking;

namespace Genesis.Simulation.World
{
    /// <summary>
    /// Handles scene migration for spawned players.
    /// Add this component to your Player Prefab alongside PlayerChunkTracker and PlayerState.
    /// Moves player to correct chunk scene on server when they spawn.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PlayerChunkTracker))]
    [RequireComponent(typeof(PlayerState))]
    public class PlayerSpawnHandler : NetworkBehaviour
    {
        private ServerSceneHandler _sceneHandler;

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Find scene handler
            _sceneHandler = FindObjectOfType<ServerSceneHandler>();

            if (_sceneHandler == null)
            {
                Debug.LogWarning("[PlayerSpawnHandler] ServerSceneHandler not found!");
                return;
            }

            // Move player to correct chunk scene on spawn
            ChunkCoordinate spawnChunk = ChunkCoordinate.FromWorldPosition(transform.position);
            _sceneHandler.MovePlayerToChunkScene(base.NetworkObject, spawnChunk);

            Debug.Log($"[PlayerSpawnHandler] Player spawned in chunk {spawnChunk}");
        }
    }
}

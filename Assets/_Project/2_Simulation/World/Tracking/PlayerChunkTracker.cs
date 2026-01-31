using FishNet.Object;
using UnityEngine;
using Genesis.Core;
using Genesis.Core.Networking;

namespace Genesis.Simulation.World
{
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerChunkTracker : NetworkBehaviour
    {
        private ChunkCoordinate _currentChunk;
        private ChunkCoordinate _previousChunk;
        private float _checkInterval = 0.5f;
        private float _nextCheckTime;

        private ServerSceneHandler _sceneHandler;

        public override void OnStartClient()
        {
            base.OnStartClient();

            _currentChunk = ChunkCoordinate.FromWorldPosition(transform.position);
            _previousChunk = _currentChunk;

            if (base.IsOwner)
            {
                EventBus.Trigger(WorldStreamingEvents.PLAYER_CHUNK_CHANGED, _currentChunk);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _sceneHandler = FindObjectOfType<ServerSceneHandler>();
        }

        void Update()
        {
            if (!base.IsOwner) return;

            if (Time.time < _nextCheckTime) return;
            _nextCheckTime = Time.time + _checkInterval;

            ChunkCoordinate newChunk = ChunkCoordinate.FromWorldPosition(transform.position);

            if (!newChunk.Equals(_currentChunk))
            {
                _previousChunk = _currentChunk;
                _currentChunk = newChunk;

                EventBus.Trigger(WorldStreamingEvents.PLAYER_CHUNK_CHANGED, _currentChunk);
                CmdNotifyChunkChange(newChunk);
            }
        }

        [ServerRpc]
        private void CmdNotifyChunkChange(ChunkCoordinate newChunk)
        {
            if (_sceneHandler != null)
            {
                _sceneHandler.MovePlayerToChunkScene(base.NetworkObject, newChunk);
            }
        }

        public ChunkCoordinate GetCurrentChunk() => _currentChunk;
    }
}

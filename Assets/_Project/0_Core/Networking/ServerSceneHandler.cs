using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;
using Genesis.Core;

namespace Genesis.Core.Networking
{
    public class ServerSceneHandler : NetworkBehaviour
    {
        [Server]
        public void MovePlayerToChunkScene(NetworkObject player, ChunkCoordinate chunk)
        {
            string sceneName = $"Chunk_{chunk.X}_{chunk.Y}";
            Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);

            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                Debug.LogError($"[ServerSceneHandler] Cannot move player to {sceneName} - scene not loaded!");
                return;
            }

            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(player.gameObject, targetScene);
            Debug.Log($"[ServerSceneHandler] Moved {player.name} to scene {sceneName}");
        }
    }
}

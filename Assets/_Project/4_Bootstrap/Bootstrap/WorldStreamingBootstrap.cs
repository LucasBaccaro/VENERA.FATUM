using System.Collections;
using UnityEngine;
using Genesis.Core;
using Genesis.Core.Networking;
using Genesis.Data;
using Genesis.Simulation.World;

namespace Genesis.Bootstrap
{
    public class WorldStreamingBootstrap : MonoBehaviour
    {
        [SerializeField] private WorldDatabase worldDatabase;
        [SerializeField] private GameObject chunkLoaderPrefab;
        [SerializeField] private GameObject serverSceneHandlerPrefab;

        void Awake()
        {
            if (worldDatabase == null)
            {
                worldDatabase = Resources.Load<WorldDatabase>("Databases/WorldDatabase");
            }

            if (worldDatabase != null)
            {
                worldDatabase.Initialize();
                ServiceLocator.Instance.Register(worldDatabase);
                Debug.Log("[WorldStreamingBootstrap] WorldDatabase initialized");

                // Register spawn position provider for PlayerSpawnManager
                WorldSpawnProvider spawnProvider = new WorldSpawnProvider(worldDatabase);
                ServiceLocator.Instance.Register<ISpawnPositionProvider>(spawnProvider);
                Debug.Log("[WorldStreamingBootstrap] WorldSpawnProvider registered");
            }
            else
            {
                Debug.LogError("[WorldStreamingBootstrap] WorldDatabase not found!");
            }
        }

        void Start()
        {
            // Wait for NetworkManager
            StartCoroutine(InitNetworkedComponents());
        }

        private IEnumerator InitNetworkedComponents()
        {
            while (FishNet.InstanceFinder.NetworkManager == null)
            {
                yield return null;
            }

            // Spawn ServerSceneHandler (server-only)
            if (FishNet.InstanceFinder.IsServer && serverSceneHandlerPrefab != null)
            {
                GameObject handler = Instantiate(serverSceneHandlerPrefab);
                DontDestroyOnLoad(handler); // Exception: This handler needs persistence
                Debug.Log("[WorldStreamingBootstrap] ServerSceneHandler spawned");
            }

            // Spawn ChunkLoaderManager (client and server)
            if (chunkLoaderPrefab != null)
            {
                GameObject loader = Instantiate(chunkLoaderPrefab);
                DontDestroyOnLoad(loader); // Exception: This manager needs persistence
                Debug.Log("[WorldStreamingBootstrap] ChunkLoaderManager spawned");
            }

            // NOTE: PlayerSpawnHandler is now a component on the Player Prefab itself, not a separate manager
            Debug.Log("[WorldStreamingBootstrap] World streaming system initialized");
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Genesis.Data
{
    [CreateAssetMenu(fileName = "WorldDatabase", menuName = "Genesis/World/World Database")]
    public class WorldDatabase : ScriptableObject
    {
        [SerializeField] private List<ChunkData> chunks = new List<ChunkData>();

        private Dictionary<Vector2Int, ChunkData> _lookup;
        private static WorldDatabase _instance;

        public static WorldDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<WorldDatabase>("Databases/WorldDatabase");
                }
                return _instance;
            }
        }

        public void Initialize()
        {
            _lookup = new Dictionary<Vector2Int, ChunkData>();
            foreach (var chunk in chunks)
            {
                _lookup[chunk.Coordinate] = chunk;
            }
            Debug.Log($"[WorldDatabase] Initialized with {chunks.Count} chunks");
        }

        public ChunkData GetChunk(Vector2Int coord)
        {
            _lookup.TryGetValue(coord, out ChunkData data);
            return data;
        }

        public ChunkData GetRandomStartingChunk()
        {
            var startingChunks = chunks.Where(c => c.IsStartingChunk).ToList();
            if (startingChunks.Count == 0)
            {
                Debug.LogError("[WorldDatabase] No starting chunks defined!");
                return chunks.FirstOrDefault();
            }
            return startingChunks[Random.Range(0, startingChunks.Count)];
        }
    }
}

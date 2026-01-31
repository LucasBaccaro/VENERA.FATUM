using UnityEngine;

namespace Genesis.Data
{
    [CreateAssetMenu(fileName = "Chunk_X_Y", menuName = "Genesis/World/Chunk Data")]
    public class ChunkData : ScriptableObject
    {
        [Header("Identity")]
        public Vector2Int Coordinate;
        public string ChunkName;

        [Header("Scene Reference")]
        [Tooltip("Scene name (must match scene file name exactly, e.g., 'Chunk_0_0')")]
        public string SceneName;

        [Header("Spawn Points")]
        [Tooltip("If IsStartingChunk=true, players can spawn at these positions")]
        public bool IsStartingChunk;
        public Vector3[] SpawnPositions;

        [Header("Metadata")]
        public string BiomeType; // Future: weather, ambience
    }
}

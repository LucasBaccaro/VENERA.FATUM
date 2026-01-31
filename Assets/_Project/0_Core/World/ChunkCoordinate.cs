using System;
using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Core
{
    [System.Serializable]
    public struct ChunkCoordinate : IEquatable<ChunkCoordinate>
    {
        public int X;
        public int Y;

        public const int CHUNK_SIZE = 256;

        public ChunkCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static ChunkCoordinate FromWorldPosition(Vector3 worldPos)
        {
            int x = Mathf.FloorToInt(worldPos.x / CHUNK_SIZE);
            int z = Mathf.FloorToInt(worldPos.z / CHUNK_SIZE);
            return new ChunkCoordinate(x, z);
        }

        public Vector3 GetChunkCenterWorld()
        {
            float centerOffset = CHUNK_SIZE / 2f;
            return new Vector3(X * CHUNK_SIZE + centerOffset, 0, Y * CHUNK_SIZE + centerOffset);
        }

        public List<ChunkCoordinate> GetNeighbors()
        {
            // Returns 8 surrounding chunks for 9-slice grid
            return new List<ChunkCoordinate>
            {
                new ChunkCoordinate(X - 1, Y + 1), // NW
                new ChunkCoordinate(X, Y + 1),     // N
                new ChunkCoordinate(X + 1, Y + 1), // NE
                new ChunkCoordinate(X - 1, Y),     // W
                new ChunkCoordinate(X + 1, Y),     // E
                new ChunkCoordinate(X - 1, Y - 1), // SW
                new ChunkCoordinate(X, Y - 1),     // S
                new ChunkCoordinate(X + 1, Y - 1)  // SE
            };
        }

        public bool Equals(ChunkCoordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public override string ToString()
        {
            return $"Chunk({X}, {Y})";
        }

        public static bool operator ==(ChunkCoordinate left, ChunkCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChunkCoordinate left, ChunkCoordinate right)
        {
            return !left.Equals(right);
        }
    }
}

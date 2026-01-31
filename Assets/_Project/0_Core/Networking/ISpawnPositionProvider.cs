using UnityEngine;

namespace Genesis.Core.Networking
{
    /// <summary>
    /// Interface for providing spawn positions to PlayerSpawnManager.
    /// Allows higher-level assemblies (Simulation) to control spawn logic without Core depending on them.
    /// </summary>
    public interface ISpawnPositionProvider
    {
        Vector3 GetSpawnPosition();
    }
}

using FishNet.Object;

namespace Genesis.Core.Persistence
{
    /// <summary>
    /// Bridge interface implemented in Simulation layer.
    /// Allows Core (NakamaManager) to extract/hydrate player data
    /// without directly referencing Simulation types.
    /// </summary>
    public interface IPersistenceBridge
    {
        /// <summary>
        /// Extracts all player data into a serializable CharacterData (server-side).
        /// </summary>
        CharacterData ExtractPlayerData(NetworkObject playerObj);

        /// <summary>
        /// Hydrates the player with loaded data (server-side).
        /// </summary>
        void HydratePlayer(NetworkObject playerObj, CharacterData data);

        /// <summary>
        /// Returns CharacterData with default values for a new player.
        /// </summary>
        CharacterData CreateNewPlayerData(string playerName, int classIndex, UnityEngine.Vector3 spawnPosition);
    }
}

using UnityEngine;

namespace Genesis.Core.Networking
{
    /// <summary>
    /// Define visibility rules for NetworkObjects based on distance.
    /// Used with FishNet's NetworkObserver system.
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkVisibilityProfile", menuName = "Genesis/Network/Visibility Profile")]
    public class NetworkVisibilityProfile : ScriptableObject
    {
        [Header("Distance Settings")]
        [Tooltip("Maximum distance for this object to be replicated to clients (in meters)")]
        public float maxDistance = 100f;

        [Tooltip("How often to update visibility checks (in seconds). Lower = more accurate but more CPU")]
        public float updateInterval = 1f;

        [Header("Object Type")]
        [Tooltip("Descriptive name for this profile")]
        public string profileName = "Default";

        [Header("Advanced")]
        [Tooltip("Use distance squared for performance (avoids sqrt calculation)")]
        public bool useDistanceSquared = true;

        [Tooltip("Always visible to owner regardless of distance")]
        public bool alwaysVisibleToOwner = true;
    }
}

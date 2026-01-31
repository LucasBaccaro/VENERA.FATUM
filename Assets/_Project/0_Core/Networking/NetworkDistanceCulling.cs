using FishNet.Object;
using FishNet.Observing;
using FishNet.Component.Observing;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace Genesis.Core.Networking
{
    /// <summary>
    /// Configures NetworkObserver with distance-based visibility using FishNet's DistanceCondition.
    /// Only replicates this NetworkObject to clients within configured distance.
    ///
    /// HOW IT WORKS:
    /// - Adds NetworkObserver component if not present
    /// - Creates and assigns a DistanceCondition ScriptableObject
    /// - FishNet automatically manages which clients can see this object
    ///
    /// PERFORMANCE:
    /// - Reduces network bandwidth (only send to nearby clients)
    /// - Reduces CPU on clients (fewer objects to process)
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [DisallowMultipleComponent]
    public class NetworkDistanceCulling : NetworkBehaviour
    {
        [Header("Visibility Configuration")]
        [SerializeField] private NetworkVisibilityProfile profile;

        [Header("Runtime Override (Optional)")]
        [SerializeField] private bool useCustomDistance = false;
        [SerializeField] private float customDistance = 100f;

        [Header("Runtime Status (Read-Only)")]
        [SerializeField] private bool _isConfigured = false;

        private NetworkObject _networkObject;
        private NetworkObserver _networkObserver;
        private DistanceCondition _distanceCondition;

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();

            // Configure observer BEFORE NetworkObject spawns
            // This ensures FishNet processes our condition during initialization
            ConfigureObserverEarly();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Force rebuild observers to apply the distance condition
            if (_networkObject != null && _isConfigured)
            {
                var serverObjects = base.NetworkManager?.ServerManager?.Objects;
                if (serverObjects != null)
                {
                    serverObjects.RebuildObservers(_networkObject);
                    Debug.Log($"[NetworkDistanceCulling] Forced observer rebuild for {gameObject.name}");
                }
            }
        }

        private void ConfigureObserverEarly()
        {
            if (_networkObject == null)
            {
                Debug.LogError($"[NetworkDistanceCulling] NetworkObject not found on {gameObject.name}");
                return;
            }

            // Get or add NetworkObserver
            _networkObserver = GetComponent<NetworkObserver>();
            if (_networkObserver == null)
            {
                _networkObserver = gameObject.AddComponent<NetworkObserver>();

                // Enable host visibility updates (host will also respect distance)
                _networkObserver.SetUpdateHostVisibility(true);

                Debug.Log($"[NetworkDistanceCulling] Added NetworkObserver to {gameObject.name}");
            }

            // Determine distance to use
            float distance = useCustomDistance ? customDistance : (profile != null ? profile.maxDistance : 100f);
            string profileName = profile != null ? profile.profileName : "Custom";

            // Create DistanceCondition ScriptableObject
            _distanceCondition = ScriptableObject.CreateInstance<DistanceCondition>();
            _distanceCondition.SetMaximumDistance(distance);

            // Pre-initialize with NetworkObject (FishNet will re-initialize during spawn)
            // This prevents NullRef if condition is checked before FishNet initialization
            _distanceCondition.Initialize(_networkObject);

            // Use Reflection to access internal _observerConditions field
            FieldInfo conditionsField = typeof(NetworkObserver).GetField("_observerConditions", BindingFlags.NonPublic | BindingFlags.Instance);
            if (conditionsField == null)
            {
                Debug.LogError($"[NetworkDistanceCulling] Could not access _observerConditions field via Reflection!");
                return;
            }

            List<ObserverCondition> conditions = conditionsField.GetValue(_networkObserver) as List<ObserverCondition>;
            if (conditions == null)
            {
                conditions = new List<ObserverCondition>();
                conditionsField.SetValue(_networkObserver, conditions);
            }

            // Check if already has a DistanceCondition
            bool hasDistanceCondition = false;
            foreach (var condition in conditions)
            {
                if (condition is DistanceCondition)
                {
                    hasDistanceCondition = true;
                    Debug.LogWarning($"[NetworkDistanceCulling] {gameObject.name} already has DistanceCondition, skipping");
                    break;
                }
            }

            if (!hasDistanceCondition)
            {
                conditions.Add(_distanceCondition);
                _isConfigured = true;
                Debug.Log($"<color=cyan>[NetworkDistanceCulling] ✅ Configured '{gameObject.name}' with {profileName} profile | Distance: {distance}m</color>");
            }
        }

        /// <summary>
        /// Runtime method to change visibility distance
        /// </summary>
        [Server]
        public void SetVisibilityDistance(float newDistance)
        {
            if (_distanceCondition == null)
            {
                Debug.LogWarning($"[NetworkDistanceCulling] DistanceCondition not initialized for {gameObject.name}");
                return;
            }

            _distanceCondition.SetMaximumDistance(newDistance);
            Debug.Log($"[NetworkDistanceCulling] Updated distance for {gameObject.name}: {newDistance}m");
        }

        /// <summary>
        /// Get current visibility distance
        /// </summary>
        public float GetVisibilityDistance()
        {
            if (_distanceCondition != null)
                return _distanceCondition.GetMaximumDistance();

            return useCustomDistance ? customDistance : (profile != null ? profile.maxDistance : 100f);
        }

        private void OnDestroy()
        {
            // Cleanup ScriptableObject instance
            if (_distanceCondition != null)
            {
                Destroy(_distanceCondition);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (profile == null && !useCustomDistance)
            {
                Debug.LogWarning($"[NetworkDistanceCulling] No profile assigned to {gameObject.name}. Using default 100m distance.");
            }
        }
#endif
    }
}

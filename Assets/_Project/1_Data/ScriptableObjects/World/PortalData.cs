using UnityEngine;

namespace Genesis.Data
{
    [CreateAssetMenu(fileName = "PortalData_New", menuName = "Genesis/World/Portal Data")]
    public class PortalData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _portalID;
        [SerializeField] private string _displayName = "Portal";

        [Header("Destination")]
        [SerializeField] private Vector3 _destinationPosition;
        [SerializeField] private Vector3 _destinationRotation;

        [Header("Linked Portal")]
        [Tooltip("Reference to the return portal. Null = one-way.")]
        [SerializeField] private PortalData _linkedPortal;

        [Header("Requirements")]
        [SerializeField] private int _minLevel = 1;
        [SerializeField] private bool _requireOutOfCombat = true;

        [Header("Timing")]
        [SerializeField] private float _transitionDuration = 1.5f;
        [SerializeField] private float _cooldown = 3f;

        // Public accessors
        public string PortalID => _portalID;
        public string DisplayName => _displayName;
        public Vector3 DestinationPosition => _destinationPosition;
        public Vector3 DestinationRotation => _destinationRotation;
        public PortalData LinkedPortal => _linkedPortal;
        public int MinLevel => _minLevel;
        public bool RequireOutOfCombat => _requireOutOfCombat;
        public float TransitionDuration => _transitionDuration;
        public float Cooldown => _cooldown;
    }
}
using FishNet.Object;
using UnityEngine;

namespace Genesis.Simulation.World
{
    [RequireComponent(typeof(BoxCollider))]
    public class ZoneTrigger : MonoBehaviour
    {
        [Header("Zone Config")]
        [SerializeField] private ZoneType zoneType = ZoneType.SafeZone;
        [SerializeField] private string zoneName = "Safe Zone";

        void Awake()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;
            gameObject.layer = LayerMask.NameToLayer("SafeZone"); // Layer 9

            Debug.Log($"[ZoneTrigger] Initialized: {zoneName} | Layer: {gameObject.layer} | IsTrigger: {col.isTrigger}");
        }

        void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[ZoneTrigger] OnTriggerEnter detected: {other.gameObject.name} | Layer: {other.gameObject.layer} | IsServer: {FishNet.InstanceFinder.IsServer}");

            if (!FishNet.InstanceFinder.IsServer)
            {
                Debug.Log($"[ZoneTrigger] Ignoring trigger (not server)");
                return;
            }

            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogWarning($"[ZoneTrigger] No NetworkObject on {other.gameObject.name}");
                return;
            }

            PlayerState playerState = netObj.GetComponent<PlayerState>();
            if (playerState == null)
            {
                Debug.LogWarning($"[ZoneTrigger] No PlayerState on {netObj.name}");
                return;
            }

            if (zoneType == ZoneType.SafeZone)
            {
                playerState.SetSafeZone(true);
                Debug.Log($"<color=green>[ZoneTrigger] ✅ Player {netObj.ObjectId} ENTERED {zoneName}</color>");
            }
        }

        void OnTriggerExit(Collider other)
        {
            Debug.Log($"[ZoneTrigger] OnTriggerExit detected: {other.gameObject.name} | IsServer: {FishNet.InstanceFinder.IsServer}");

            if (!FishNet.InstanceFinder.IsServer)
            {
                Debug.Log($"[ZoneTrigger] Ignoring trigger (not server)");
                return;
            }

            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogWarning($"[ZoneTrigger] No NetworkObject on {other.gameObject.name}");
                return;
            }

            PlayerState playerState = netObj.GetComponent<PlayerState>();
            if (playerState == null)
            {
                Debug.LogWarning($"[ZoneTrigger] No PlayerState on {netObj.name}");
                return;
            }

            if (zoneType == ZoneType.SafeZone)
            {
                playerState.SetSafeZone(false);
                Debug.Log($"<color=red>[ZoneTrigger] ❌ Player {netObj.ObjectId} EXITED {zoneName}</color>");
            }
        }
    }

    public enum ZoneType
    {
        SafeZone,
        PvPZone,     // Future
        ResourceZone // Future
    }
}

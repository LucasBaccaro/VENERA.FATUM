using FishNet.Object;
using Genesis.Core;
using UnityEngine;

namespace Genesis.Simulation.World
{
    [RequireComponent(typeof(BoxCollider))]
    public class ZoneTrigger : MonoBehaviour
    {
        [Header("Zone Config")]
        [SerializeField] private ZoneType zoneType = ZoneType.SafeZone;
        [SerializeField] private string zoneName = "Safe Zone";

        [Header("Audio")]
        [SerializeField] private AudioClip zoneMusic;
        [SerializeField] private AudioClip zoneAmbient;

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

            // Zone audio: client-side only, for the local player
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner && (zoneMusic != null || zoneAmbient != null))
            {
                EventBus.Trigger("OnZoneMusicChanged", zoneMusic, zoneAmbient);
            }

            if (!FishNet.InstanceFinder.IsServer)
            {
                return;
            }

            if (netObj == null)
            {
                return;
            }

            PlayerState playerState = netObj.GetComponent<PlayerState>();
            if (playerState == null)
            {
                return;
            }

            if (zoneType == ZoneType.SafeZone)
            {
                playerState.SetSafeZone(true);
                Debug.Log($"[ZoneTrigger] Player {netObj.ObjectId} ENTERED {zoneName}");
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

using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Genesis.Core;

namespace Genesis.Simulation.World
{
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerState : NetworkBehaviour
    {
        private readonly SyncVar<bool> _isInSafeZone = new SyncVar<bool>(false);

        public bool IsInSafeZone => _isInSafeZone.Value;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _isInSafeZone.OnChange += OnSafeZoneChanged;
        }

        [Server]
        public void SetSafeZone(bool inSafeZone)
        {
            if (_isInSafeZone.Value == inSafeZone)
            {
                Debug.Log($"[PlayerState] Safe zone value unchanged: {inSafeZone}");
                return;
            }

            _isInSafeZone.Value = inSafeZone;
            Debug.Log($"<color=yellow>[PlayerState] Safe zone changed: {!inSafeZone} -> {inSafeZone}</color>");
        }

        private void OnSafeZoneChanged(bool oldValue, bool newValue, bool asServer)
        {
            Debug.Log($"[PlayerState] OnSafeZoneChanged: {oldValue} -> {newValue} | IsOwner: {base.IsOwner} | AsServer: {asServer}");

            if (base.IsOwner)
            {
                EventBus.Trigger(WorldStreamingEvents.PLAYER_ZONE_CHANGED, newValue);
                Debug.Log($"<color=cyan>[PlayerState] ✅ EventBus triggered for UI: {newValue}</color>");
            }
        }
    }
}

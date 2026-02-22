using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using Genesis.Core;

namespace Genesis.Simulation {

    /// <summary>
    /// NetworkBehaviour on the player root GO.
    /// Handles party state sync and client↔server party RPCs.
    /// Add this component to the player prefab root.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PartyMember : NetworkBehaviour {

        private readonly SyncVar<int> _partyId = new SyncVar<int>(-1);
        private readonly SyncVar<bool> _isLeader = new SyncVar<bool>(false);
        private readonly SyncVar<int> _partySize = new SyncVar<int>(0);

        public bool IsInParty => _partyId.Value != -1;
        public int PartyId => _partyId.Value;
        public bool IsLeader => _isLeader.Value;
        public int PartySize => _partySize.Value;
        public bool IsPartyFull => IsInParty && _partySize.Value >= 4;

        public override void OnStartNetwork() {
            base.OnStartNetwork();
            _partyId.OnChange += OnPartyIdChanged;
        }

        // ═══════════════════════════════════════════════════════
        // CLIENT → SERVER
        // ═══════════════════════════════════════════════════════

        [ServerRpc]
        public void CmdInvitePlayer(int targetClientId) {
            if (!ServiceLocator.Instance.TryGet<PartyManager>(out var pm)) return;

            int inviterClientId = base.Owner.ClientId;
            if (!pm.TryCreateInvite(inviterClientId, targetClientId)) return;

            if (!InstanceFinder.NetworkManager.ServerManager.Clients.TryGetValue(targetClientId, out var targetConn)) return;
            var targetPm = targetConn.FirstObject?.GetComponent<PartyMember>();
            if (targetPm == null) return;

            string inviterName = base.NetworkObject.GetComponent<PlayerClassManager>()?.PlayerName ?? "Unknown";
            targetPm.TargetReceiveInvite(targetConn, inviterName);
        }

        [ServerRpc]
        public void CmdAcceptInvite() {
            if (!ServiceLocator.Instance.TryGet<PartyManager>(out var pm)) return;
            pm.TryAcceptInvite(base.Owner.ClientId);
        }

        [ServerRpc]
        public void CmdDeclineInvite() {
            if (!ServiceLocator.Instance.TryGet<PartyManager>(out var pm)) return;
            pm.DeclineInvite(base.Owner.ClientId);
        }

        [ServerRpc]
        public void CmdLeaveParty() {
            if (!ServiceLocator.Instance.TryGet<PartyManager>(out var pm)) return;
            pm.LeaveParty(base.Owner.ClientId);
        }

        [ServerRpc]
        public void CmdKickMember(int targetClientId) {
            if (!ServiceLocator.Instance.TryGet<PartyManager>(out var pm)) return;
            pm.KickMember(base.Owner.ClientId, targetClientId);
        }

        // ═══════════════════════════════════════════════════════
        // SERVER → CLIENT
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Called by PartyManager to sync party state to all clients observing this player.
        /// </summary>
        [Server]
        public void ApplyPartyState(int partyId, bool isLeader, int memberCount) {
            _partyId.Value = partyId;
            _isLeader.Value = isLeader;
            _partySize.Value = memberCount;
            RpcPartyStateChanged();
        }

        /// <summary>
        /// Delivers a party invite notification to a specific client.
        /// </summary>
        [TargetRpc]
        public void TargetReceiveInvite(NetworkConnection conn, string inviterName) {
            EventBus.Trigger("OnPartyInviteReceived", inviterName);
        }

        [ObserversRpc]
        private void RpcPartyStateChanged() {
            if (base.IsOwner) {
                EventBus.Trigger("OnPartyStateChanged");
            }
        }

        // ═══════════════════════════════════════════════════════
        // SYNCVAR CALLBACK
        // ═══════════════════════════════════════════════════════

        private void OnPartyIdChanged(int oldValue, int newValue, bool asServer) {
            if (!asServer && base.IsOwner) {
                EventBus.Trigger("OnPartyStateChanged");
            }
        }
    }
}

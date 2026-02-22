using System.Collections.Generic;
using UnityEngine;
using FishNet;
using FishNet.Object;
using Genesis.Core;

namespace Genesis.Simulation {

    /// <summary>
    /// Server-only manager for party state. Registered in ServiceLocator as both
    /// PartyManager (for Simulation code) and IPartyService (for Core code).
    /// </summary>
    public class PartyManager : MonoBehaviour, IPartyService {

        // partyId → PartyGroup
        private readonly Dictionary<int, PartyGroup> _parties = new Dictionary<int, PartyGroup>();
        // clientId → partyId
        private readonly Dictionary<int, int> _memberToParty = new Dictionary<int, int>();
        // inviteeClientId → inviterClientId
        private readonly Dictionary<int, int> _pendingInvites = new Dictionary<int, int>();

        private void Awake() {
            ServiceLocator.Instance.Register<PartyManager>(this);
            ServiceLocator.Instance.Register<IPartyService>(this);
        }

        private void OnDestroy() {
            ServiceLocator.Instance.Unregister<PartyManager>();
            ServiceLocator.Instance.Unregister<IPartyService>();
        }

        // ═══════════════════════════════════════════════════════
        // PUBLIC QUERY API
        // ═══════════════════════════════════════════════════════

        public bool IsInSameParty(int clientIdA, int clientIdB) {
            if (!_memberToParty.TryGetValue(clientIdA, out int partyA)) return false;
            if (!_memberToParty.TryGetValue(clientIdB, out int partyB)) return false;
            return partyA == partyB;
        }

        public PartyGroup GetParty(int clientId) {
            if (!_memberToParty.TryGetValue(clientId, out int partyId)) return null;
            _parties.TryGetValue(partyId, out var group);
            return group;
        }

        // ═══════════════════════════════════════════════════════
        // INVITE FLOW
        // ═══════════════════════════════════════════════════════

        public bool TryCreateInvite(int inviterClientId, int inviteeClientId) {
            var inviterParty = GetParty(inviterClientId);
            if (inviterParty != null && inviterParty.IsFull) {
                Debug.Log("[PartyManager] Invite rejected: party is full.");
                return false;
            }
            if (IsInSameParty(inviterClientId, inviteeClientId)) {
                Debug.Log("[PartyManager] Invite rejected: already party members.");
                return false;
            }
            _pendingInvites[inviteeClientId] = inviterClientId;
            Debug.Log($"[PartyManager] Invite queued: {inviterClientId} → {inviteeClientId}");
            return true;
        }

        public bool TryAcceptInvite(int inviteeClientId) {
            if (!_pendingInvites.TryGetValue(inviteeClientId, out int inviterClientId)) {
                Debug.LogWarning($"[PartyManager] No pending invite for client {inviteeClientId}");
                return false;
            }
            _pendingInvites.Remove(inviteeClientId);

            PartyGroup party;
            if (_memberToParty.TryGetValue(inviterClientId, out int existingPartyId)) {
                party = _parties[existingPartyId];
            } else {
                party = new PartyGroup {
                    PartyId = inviterClientId,
                    LeaderClientId = inviterClientId,
                    LootMode = LootMode.Free
                };
                party.MemberClientIds.Add(inviterClientId);
                _parties[party.PartyId] = party;
                _memberToParty[inviterClientId] = party.PartyId;
            }

            if (party.IsFull) {
                Debug.LogWarning("[PartyManager] Cannot accept: party is now full.");
                return false;
            }

            party.MemberClientIds.Add(inviteeClientId);
            _memberToParty[inviteeClientId] = party.PartyId;

            Debug.Log($"[PartyManager] {inviteeClientId} joined party {party.PartyId} (size={party.MemberClientIds.Count})");
            NotifyAllMembers(party);
            return true;
        }

        public void DeclineInvite(int inviteeClientId) {
            _pendingInvites.Remove(inviteeClientId);
            Debug.Log($"[PartyManager] Client {inviteeClientId} declined invite.");
        }

        // ═══════════════════════════════════════════════════════
        // PARTY MANAGEMENT
        // ═══════════════════════════════════════════════════════

        public void LeaveParty(int clientId) {
            if (!_memberToParty.TryGetValue(clientId, out int partyId)) return;

            _memberToParty.Remove(clientId);
            var party = _parties[partyId];
            party.MemberClientIds.Remove(clientId);

            // Notify the leaving player they are no longer in party
            GetPartyMember(clientId)?.ApplyPartyState(-1, false, 0);

            if (party.MemberClientIds.Count <= 1) {
                DisbandParty(partyId);
            } else {
                if (party.LeaderClientId == clientId) {
                    party.LeaderClientId = party.MemberClientIds[0];
                    Debug.Log($"[PartyManager] New leader: {party.LeaderClientId}");
                }
                NotifyAllMembers(party);
            }
        }

        public void KickMember(int leaderClientId, int targetClientId) {
            var party = GetParty(leaderClientId);
            if (party == null || party.LeaderClientId != leaderClientId) return;
            if (!party.HasMember(targetClientId)) return;

            _memberToParty.Remove(targetClientId);
            party.MemberClientIds.Remove(targetClientId);

            GetPartyMember(targetClientId)?.ApplyPartyState(-1, false, 0);

            if (party.MemberClientIds.Count <= 1) {
                DisbandParty(party.PartyId);
            } else {
                NotifyAllMembers(party);
            }
        }

        public void DisbandParty(int partyId) {
            if (!_parties.TryGetValue(partyId, out var party)) return;

            var membersSnapshot = new List<int>(party.MemberClientIds);
            foreach (int memberId in membersSnapshot) {
                _memberToParty.Remove(memberId);
            }
            party.MemberClientIds.Clear();
            _parties.Remove(partyId);

            foreach (int memberId in membersSnapshot) {
                GetPartyMember(memberId)?.ApplyPartyState(-1, false, 0);
            }

            Debug.Log($"[PartyManager] Party {partyId} disbanded.");
        }

        // ═══════════════════════════════════════════════════════
        // DISCONNECT CLEANUP (IPartyService)
        // ═══════════════════════════════════════════════════════

        public void OnPlayerDisconnected(int clientId) {
            // Cancel pending invites involving this player
            _pendingInvites.Remove(clientId);
            var inviterKeys = new List<int>();
            foreach (var kvp in _pendingInvites) {
                if (kvp.Value == clientId) inviterKeys.Add(kvp.Key);
            }
            foreach (int key in inviterKeys) _pendingInvites.Remove(key);

            if (!_memberToParty.ContainsKey(clientId)) return;

            int partyId = _memberToParty[clientId];
            bool isLeader = _parties.TryGetValue(partyId, out var party) && party.LeaderClientId == clientId;

            if (isLeader) {
                DisbandParty(partyId);
            } else {
                LeaveParty(clientId);
            }

            Debug.Log($"[PartyManager] Disconnected client {clientId} removed from party.");
        }

        // ═══════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════

        private void NotifyAllMembers(PartyGroup party) {
            foreach (int memberId in party.MemberClientIds) {
                GetPartyMember(memberId)?.ApplyPartyState(
                    party.PartyId,
                    party.LeaderClientId == memberId,
                    party.MemberClientIds.Count);
            }
        }

        private PartyMember GetPartyMember(int clientId) {
            if (InstanceFinder.NetworkManager == null) return null;
            if (!InstanceFinder.NetworkManager.ServerManager.Clients.TryGetValue(clientId, out var conn)) return null;
            return conn.FirstObject?.GetComponent<PartyMember>();
        }
    }
}

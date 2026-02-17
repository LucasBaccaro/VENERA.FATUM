using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using Genesis.Core;
using Genesis.Data;

namespace Genesis.Simulation {

    [RequireComponent(typeof(NetworkObject))]
    public class NpcController : NetworkBehaviour, IInteractable {

        [Header("NPC Data")]
        [SerializeField] private NpcData _npcData;

        public string NpcID => _npcData != null ? _npcData.NpcID : "";
        public string DisplayName => _npcData != null ? _npcData.DisplayName : "NPC";
        public NpcData NpcData => _npcData;

        // ═══════════════════════════════════════════════════════
        // IInteractable
        // ═══════════════════════════════════════════════════════

        public void Interact(NetworkObject player) {
            if (player == null || _npcData == null) return;

            // Vendor NPCs open shop instead of dialogue
            if (_npcData.IsVendor && _npcData.VendorInventory != null) {
                if (player.Owner.IsValid) {
                    TargetOpenVendorUI(player.Owner, _npcData.NpcID, _npcData.DisplayName);
                }
                return;
            }

            // Handle quest talk objectives (server-side)
            if (base.IsServerInitialized) {
                var questMgr = player.GetComponent<PlayerQuestManager>();
                if (questMgr != null) {
                    questMgr.NotifyTalkedToNpc(_npcData.NpcID);
                }
            }

            // Notify client to open dialogue
            if (player.Owner.IsValid) {
                TargetOpenDialogue(player.Owner, _npcData.NpcID, _npcData.DisplayName);
            }
        }

        public bool CanInteract(NetworkObject player) {
            return _npcData != null;
        }

        public string GetInteractionPrompt() {
            if (_npcData == null) return "Talk";
            if (_npcData.IsVendor) return $"Trade with {_npcData.DisplayName}";
            return $"Talk to {_npcData.DisplayName}";
        }

        // ═══════════════════════════════════════════════════════
        // RPCs
        // ═══════════════════════════════════════════════════════

        [TargetRpc]
        private void TargetOpenDialogue(NetworkConnection conn, string npcId, string displayName) {
            EventBus.Trigger("OnNpcDialogueOpen", npcId, displayName);
        }

        [TargetRpc]
        private void TargetOpenVendorUI(NetworkConnection conn, string npcId, string displayName) {
            EventBus.Trigger("OnVendorUIOpen", npcId, displayName);
        }
    }
}

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
            return _npcData != null ? $"Talk to {_npcData.DisplayName}" : "Talk";
        }

        // ═══════════════════════════════════════════════════════
        // RPCs
        // ═══════════════════════════════════════════════════════

        [TargetRpc]
        private void TargetOpenDialogue(NetworkConnection conn, string npcId, string displayName) {
            EventBus.Trigger("OnNpcDialogueOpen", npcId, displayName);
        }
    }
}

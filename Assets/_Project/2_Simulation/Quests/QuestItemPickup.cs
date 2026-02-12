using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Genesis.Core;
using Genesis.Items;

namespace Genesis.Simulation {

    [RequireComponent(typeof(NetworkObject))]
    public class QuestItemPickup : NetworkBehaviour, IInteractable {

        [Header("Quest Item")]
        [SerializeField] private string _questItemTag = "relic";
        [SerializeField] private string _promptText = "Pick up Relic";

        [Header("Respawn")]
        [SerializeField] private float _respawnTime = 60f;

        [Header("Inventory Item (optional)")]
        [SerializeField] private int _itemId = 0;
        [SerializeField] private int _quantity = 1;
        [SerializeField] private ItemTier _tier = ItemTier.T0;
        [SerializeField] private ItemRarity _rarity = ItemRarity.Common;

        private readonly SyncVar<bool> _isAvailable = new SyncVar<bool>(true);

        public override void OnStartNetwork() {
            base.OnStartNetwork();
            _isAvailable.OnChange += OnAvailabilityChanged;
        }

        // ═══════════════════════════════════════════════════════
        // IInteractable
        // ═══════════════════════════════════════════════════════

        public void Interact(NetworkObject player) {
            if (!base.IsServerInitialized || !_isAvailable.Value) return;

            _isAvailable.Value = false;

            // Add item to inventory if configured
            if (_itemId > 0) {
                var inventory = player.GetComponent<PlayerInventory>();
                if (inventory != null) {
                    inventory.AddItem(_itemId, _quantity, _tier, _rarity);
                }
            }

            // Notify quest system
            var questMgr = player.GetComponent<PlayerQuestManager>();
            if (questMgr != null) {
                questMgr.NotifyItemCollected(_questItemTag);
            }

            Debug.Log($"[QuestItemPickup] {player.name} picked up {_questItemTag}");

            // Schedule respawn
            StartCoroutine(Respawn());
        }

        public bool CanInteract(NetworkObject player) {
            return _isAvailable.Value;
        }

        public string GetInteractionPrompt() {
            return _isAvailable.Value ? _promptText : "";
        }

        // ═══════════════════════════════════════════════════════
        // Respawn
        // ═══════════════════════════════════════════════════════

        private System.Collections.IEnumerator Respawn() {
            yield return new WaitForSeconds(_respawnTime);
            if (base.IsServerInitialized) {
                _isAvailable.Value = true;
            }
        }

        private void OnAvailabilityChanged(bool oldVal, bool newVal, bool asServer) {
            // Toggle visual
            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null) renderer.enabled = newVal;

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = newVal;
        }
    }
}

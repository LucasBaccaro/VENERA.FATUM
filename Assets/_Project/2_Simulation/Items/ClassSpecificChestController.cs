using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Genesis.Data;
using Genesis.Items;
using Genesis.Core;
using System.Collections.Generic;

namespace Genesis.Simulation {
    
    /// <summary>
    /// Special chest that generates class-specific T0 equipment for "El Fuerte" quest.
    /// Detects player class and populates with appropriate starter gear.
    /// </summary>
    public class ClassSpecificChestController : ChestController {
        
        [Header("Class-Specific Settings")]
        [SerializeField] private bool _isOneTimeUse = true;
        [SerializeField] private string _questID = "tutorial_02"; // El Fuerte quest ID
        
        // T0 Equipment IDs by class
        private readonly Dictionary<string, int[]> _classT0Items = new Dictionary<string, int[]> {
            { "Mage", new[] { 2001, 2002, 2003, 2004, 2005, 2006, 2007, 2008 } },
            { "Warrior", new[] { 3001, 3002, 3003, 3004, 3005, 3006, 3007, 3008, 3009 } }
        };
        
        private readonly SyncVar<bool> _hasBeenLooted = new SyncVar<bool>(false);
        
        [Server]
        protected override void GenerateLoot() {
            _lootItems.Clear();
            
            // If one-time use and already looted, don't generate anything
            if (_isOneTimeUse && _hasBeenLooted.Value) {
                Debug.Log("[ClassSpecificChest] Chest already looted, not generating new loot.");
                return;
            }
            
            // Find the player who is opening this chest
            NetworkObject player = GetNearestPlayer();
            if (player == null) {
                Debug.LogWarning("[ClassSpecificChest] No player found nearby!");
                return;
            }
            
            // Get player's class
            var classManager = player.GetComponent<PlayerClassManager>();
            if (classManager == null) {
                Debug.LogWarning("[ClassSpecificChest] Player has no PlayerClassManager!");
                return;
            }
            
            string playerClass = classManager.GetCurrentClassName();
            Debug.Log($"[ClassSpecificChest] Generating loot for class: {playerClass}");
            
            // Get appropriate item set
            if (!_classT0Items.ContainsKey(playerClass)) {
                Debug.LogWarning($"[ClassSpecificChest] Unknown class: {playerClass}");
                return;
            }
            
            int[] itemIDs = _classT0Items[playerClass];
            
            // Add all T0 items to chest
            foreach (int itemID in itemIDs) {
                var itemData = ItemDatabase.Instance?.GetItem(itemID);
                if (itemData != null) {
                    _lootItems.Add(new ItemSlot {
                        ItemID = itemID,
                        Quantity = 1,
                        Tier = ItemTier.T0,
                        Rarity = ItemRarity.Common
                    });
                } else {
                    Debug.LogWarning($"[ClassSpecificChest] Item {itemID} not found in database!");
                }
            }
            
            Debug.Log($"[ClassSpecificChest] Generated {_lootItems.Count} items for {playerClass}");
        }
        
        [Server]
        protected override void TakeItem(int lootIndex, NetworkObject player) {
            base.TakeItem(lootIndex, player);
            
            // Notify quest system about item collection
            var questManager = player.GetComponent<PlayerQuestManager>();
            if (questManager != null) {
                // Use "starter_equipment" tag to track collection
                questManager.NotifyItemCollected("starter_equipment");
            }
            
            // Mark as looted if all items taken
            if (_lootItems.Count == 0 && _isOneTimeUse) {
                _hasBeenLooted.Value = true;
            }
        }
        
        [Server]
        protected override void TakeAll(NetworkObject player) {
            int itemCount = _lootItems.Count;
            base.TakeAll(player);
            
            // Notify quest system about all items collected
            var questManager = player.GetComponent<PlayerQuestManager>();
            if (questManager != null) {
                // Trigger quest progress for each item taken
                int itemsTaken = itemCount - _lootItems.Count;
                for (int i = 0; i < itemsTaken; i++) {
                    questManager.NotifyItemCollected("starter_equipment");
                }
            }
            
            // Mark as looted if all items taken
            if (_lootItems.Count == 0 && _isOneTimeUse) {
                _hasBeenLooted.Value = true;
            }
        }
        
        public override bool CanInteract(NetworkObject player) {
            // Can't interact if already looted
            if (_isOneTimeUse && _hasBeenLooted.Value) {
                return false;
            }
            
            return base.CanInteract(player);
        }
        
        [Server]
        public override void Interact(NetworkObject player) {
            // Check if player has the required quest active
            var questManager = player.GetComponent<PlayerQuestManager>();
            if (questManager == null || !questManager.IsQuestActive(_questID)) {
                // Send alert to the player
                TargetShowLockedAlert(player.Owner);
                return;
            }
            
            base.Interact(player);
        }

        [TargetRpc]
        private void TargetShowLockedAlert(FishNet.Connection.NetworkConnection conn) {
            EventBus.Trigger("OnCombatError", "Este cofre está cerrado (Requiere misión 'El Fuerte')");
        }
        
        /// <summary>
        /// Find the nearest player to this chest (for class detection)
        /// </summary>
        private NetworkObject GetNearestPlayer() {
            var players = FindObjectsByType<PlayerClassManager>(FindObjectsSortMode.None);
            NetworkObject nearest = null;
            float nearestDist = float.MaxValue;
            
            foreach (var playerClass in players) {
                var netObj = playerClass.GetComponent<NetworkObject>();
                if (netObj != null) {
                    float dist = Vector3.Distance(transform.position, netObj.transform.position);
                    if (dist < nearestDist) {
                        nearestDist = dist;
                        nearest = netObj;
                    }
                }
            }
            
            return nearest;
        }
    }
}

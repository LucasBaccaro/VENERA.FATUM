using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using UnityEngine;
using Genesis.Items;
using Genesis.Data;
using Genesis.Core;

namespace Genesis.Simulation {
    /// <summary>
    /// Loot bag spawned when player dies
    /// Contains all inventory + equipment items
    /// Public loot (anyone can take items)
    /// Auto-despawns after 5 minutes or when empty
    /// </summary>
    public class LootBag : NetworkBehaviour, IInteractable, ILootSource {
        [Header("Configuration")]
        [Tooltip("Time in seconds before auto-despawn")]
        [SerializeField] private float _despawnTime = 300f; // 5 minutes

        [Tooltip("Interact radius")]
        [SerializeField] private float _interactRadius = 3f;

        [Header("Network State")]
        private readonly SyncList<ItemSlot> _lootItems = new SyncList<ItemSlot>();
        private readonly SyncVar<string> _ownerName = new SyncVar<string>("");

        private float _spawnTime;
        private bool _isInitialized = false;

        // ILootSource Implementation
        public IReadOnlyList<ItemSlot> LootItems => _lootItems;
        public string LootName => $"{_ownerName.Value}'s Bag";

        private void Awake() {
            // Subscribe to loot changes
            _lootItems.OnChange += OnLootChanged;
        }

        public override void OnStartServer() {
            base.OnStartServer();
            _spawnTime = Time.time;

            Debug.Log($"[LootBag] Spawned at {transform.position}");
        }

        private void Update() {
            if (!base.IsServer) return;

            // Check for auto-despawn
            float elapsed = Time.time - _spawnTime;
            if (elapsed >= _despawnTime) {
                Debug.Log($"[LootBag] Despawning after {elapsed:F1}s (timeout)");
                DespawnBag();
            }
        }

        /// <summary>
        /// Initialize loot bag with items (server-only)
        /// </summary>
        [Server]
        public void Initialize(List<ItemSlot> items, string ownerName) {
            _ownerName.Value = ownerName;

            _lootItems.Clear();
            foreach (var item in items) {
                if (!item.IsEmpty) {
                    _lootItems.Add(item);
                }
            }

            _isInitialized = true;

            Debug.Log($"[LootBag] Initialized with {_lootItems.Count} items from '{ownerName}'");

            // If empty, despawn immediately
            if (_lootItems.Count == 0) {
                Debug.Log("[LootBag] No items to loot, despawning immediately.");
                DespawnBag();
            }
        }

        /// <summary>
        /// Callback when loot changes
        /// </summary>
        private void OnLootChanged(SyncListOperation op, int index, ItemSlot oldItem, ItemSlot newItem, bool asServer) {
            // Check if bag is now empty (server-only)
            // Only despawn if initialized (to avoid despawning during Initialize's Clear())
            if (asServer && _isInitialized && _lootItems.Count == 0) {
                Debug.Log("[LootBag] All items taken, despawning.");
                DespawnBag();
            }
        }

        #region Server Methods

        /// <summary>
        /// Take item at specific index (server-only)
        /// </summary>
        [Server]
        public bool TakeItem(int lootIndex, NetworkObject player) {
            if (lootIndex < 0 || lootIndex >= _lootItems.Count) {
                Debug.LogWarning($"[LootBag] Invalid loot index {lootIndex}");
                return false;
            }

            ItemSlot item = _lootItems[lootIndex];
            if (item.IsEmpty) {
                Debug.LogWarning($"[LootBag] Loot index {lootIndex} is empty");
                return false;
            }

            // Get player inventory
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null) {
                Debug.LogWarning("[LootBag] Player does not have PlayerInventory!");
                return false;
            }

            // Check if player has space
            if (!inventory.HasSpace(item.ItemID, item.Quantity)) {
                Debug.LogWarning("[LootBag] Player inventory is full!");
                EventBus.Trigger("OnCombatError", "Inventory full!");
                return false;
            }

            // Add to player inventory
            bool success = inventory.AddItem(item.ItemID, item.Quantity, item.Tier, item.Rarity);
            if (!success) {
                Debug.LogWarning("[LootBag] Failed to add item to player inventory!");
                return false;
            }

            // Remove from loot bag
            _lootItems.RemoveAt(lootIndex);

            BaseItemData itemData = ItemDatabase.Instance.GetItem(item.ItemID);
            string itemName = itemData != null ? itemData.ItemName : $"Item {item.ItemID}";
            Debug.Log($"[LootBag] Player {player.name} took {item.Quantity}x {itemName}");

            return true;
        }

        /// <summary>
        /// Take all items (server-only)
        /// </summary>
        [Server]
        public void TakeAll(NetworkObject player) {
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null) {
                Debug.LogWarning("[LootBag] Player does not have PlayerInventory!");
                return;
            }

            int takenCount = 0;
            int failedCount = 0;

            // Take items in reverse order (to avoid index shifting issues)
            for (int i = _lootItems.Count - 1; i >= 0; i--) {
                ItemSlot item = _lootItems[i];
                if (item.IsEmpty) continue;

                // Check if player has space
                if (!inventory.HasSpace(item.ItemID, item.Quantity)) {
                    failedCount++;
                    continue;
                }

                // Add to inventory
                bool success = inventory.AddItem(item.ItemID, item.Quantity, item.Tier, item.Rarity);
                if (success) {
                    _lootItems.RemoveAt(i);
                    takenCount++;
                } else {
                    failedCount++;
                }
            }

            Debug.Log($"[LootBag] Player {player.name} took {takenCount} items (failed: {failedCount})");

            if (failedCount > 0) {
                EventBus.Trigger("OnCombatError", $"Inventory full! {failedCount} items not taken.");
            }
        }

        /// <summary>
        /// Despawn the loot bag
        /// </summary>
        [Server]
        private void DespawnBag() {
            if (base.IsServer) {
                base.Despawn();
            }
        }

        #endregion

        #region ILootSource Implementation (ServerRpc)

        /// <summary>
        /// Client requests to take item
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdTakeItem(int lootIndex, NetworkObject player) {
            TakeItem(lootIndex, player);
        }

        /// <summary>
        /// Client requests to take all items
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdTakeAll(NetworkObject player) {
            TakeAll(player);
        }
        
        /// <summary>
        /// Client requests to interact
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdTryInteract(NetworkObject player) {
            Interact(player);
        }
        
        public bool CanLoot(NetworkObject player) {
            return true; // Anyone can loot bags
        }

        #endregion

        #region IInteractable Implementation

        public void Interact(NetworkObject player) {
            if (!base.IsServer) return;

            Debug.Log($"[LootBag] {player.name} is interacting with loot bag");

            // Trigger client UI to open loot window
            // We pass 'this' which implements ILootSource, but TargetRpc needs specific handling or we use EventBus on client
            if (player.Owner.IsValid) {
                TargetOpenLootUI(player.Owner);
            }
        }

        public bool CanInteract(NetworkObject player) {
            // Anyone can interact with loot bags
            return _lootItems.Count > 0;
        }

        public string GetInteractionPrompt() {
            return $"Loot {_ownerName.Value}'s Bag";
        }

        #endregion

        #region TargetRpc

        /// <summary>
        /// Tell specific client to open loot UI
        /// </summary>
        [TargetRpc]
        private void TargetOpenLootUI(FishNet.Connection.NetworkConnection conn) {
            Debug.Log("[LootBag] Opening loot UI on client");
            // Pass 'this' as ILootSource. EventBus needs to handle ILootSource or we cast
            EventBus.Trigger("OnLootOpened", (ILootSource)this);
        }

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() {
            // Draw interact radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
        }
#endif
    }
}

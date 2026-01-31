using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using UnityEngine;
using Genesis.Items;
using Genesis.Data;
using Genesis.Core;

namespace Genesis.Simulation {
    /// <summary>
    /// Player inventory system with 25 slots
    /// Server-authoritative with SyncList
    /// </summary>
    public class PlayerInventory : NetworkBehaviour {
        [Header("Configuration")]
        [SerializeField] private int _inventorySize = 25;

        [Header("Network State")]
        [Tooltip("Synced inventory slots (SyncList)")]
        private readonly SyncList<ItemSlot> _inventorySlots = new SyncList<ItemSlot>();

        public IReadOnlyList<ItemSlot> InventorySlots => _inventorySlots;

        private void Awake() {
            // Subscribe to SyncList changes
            _inventorySlots.OnChange += OnInventoryChanged;
        }

        public override void OnStartServer() {
            base.OnStartServer();

            // Initialize empty inventory
            _inventorySlots.Clear();
            for (int i = 0; i < _inventorySize; i++) {
                _inventorySlots.Add(ItemSlot.Empty);
            }

            Debug.Log($"[PlayerInventory] Initialized {_inventorySize} slots.");
        }

        public override void OnStopServer() {
            base.OnStopServer();
            _inventorySlots.Clear();
        }

        /// <summary>
        /// Callback when inventory changes (synced to clients)
        /// </summary>
        private void OnInventoryChanged(SyncListOperation op, int index, ItemSlot oldItem, ItemSlot newItem, bool asServer) {
            Debug.Log($"[PlayerInventory] OnInventoryChanged - Op: {op}, Index: {index}, IsOwner: {IsOwner}, AsServer: {asServer}, NewItem: ItemID={newItem.ItemID} x{newItem.Quantity}");

            // Only trigger UI update on owner client (or host)
            if (IsOwner) {
                Debug.Log("[PlayerInventory] Triggering OnInventoryChanged event for UI");
                EventBus.Trigger("OnInventoryChanged");
            }
        }

        #region Server Methods

        /// <summary>
        /// Add item to inventory (server-only)
        /// </summary>
        [Server]
        public bool AddItem(int itemId, int quantity, ItemTier tier, ItemRarity rarity) {
            if (quantity <= 0) {
                Debug.LogWarning("[PlayerInventory] Cannot add item with quantity <= 0.");
                return false;
            }

            BaseItemData itemData = ItemDatabase.Instance.GetItem(itemId);
            if (itemData == null) {
                Debug.LogError($"[PlayerInventory] Item ID {itemId} not found in database!");
                return false;
            }

            // If item is stackable, try to stack first
            if (itemData.IsStackable) {
                int remaining = quantity;

                // Try to add to existing stacks
                for (int i = 0; i < _inventorySlots.Count && remaining > 0; i++) {
                    ItemSlot slot = _inventorySlots[i];

                    // Skip if empty or different item
                    if (slot.IsEmpty || slot.ItemID != itemId) continue;

                    // Skip if different tier/rarity (stacks must match)
                    if (slot.Tier != tier || slot.Rarity != rarity) continue;

                    // Calculate how much we can add to this stack
                    int spaceInStack = itemData.MaxStackSize - slot.Quantity;
                    if (spaceInStack <= 0) continue;

                    int amountToAdd = Mathf.Min(spaceInStack, remaining);
                    slot.Quantity += amountToAdd;
                    _inventorySlots[i] = slot;
                    remaining -= amountToAdd;

                    Debug.Log($"[PlayerInventory] Added {amountToAdd}x {itemData.ItemName} to slot {i} (now {slot.Quantity}).");
                }

                // If still have remaining, create new stacks
                while (remaining > 0) {
                    int emptySlot = FindFirstEmptySlot();
                    if (emptySlot == -1) {
                        Debug.LogWarning($"[PlayerInventory] Inventory full! Could not add {remaining}x {itemData.ItemName}.");
                        EventBus.Trigger("OnCombatError", "Inventory full!");
                        return false;
                    }

                    int amountToAdd = Mathf.Min(itemData.MaxStackSize, remaining);
                    _inventorySlots[emptySlot] = new ItemSlot(itemId, amountToAdd, tier, rarity);
                    remaining -= amountToAdd;

                    Debug.Log($"[PlayerInventory] Created new stack of {amountToAdd}x {itemData.ItemName} in slot {emptySlot}.");
                }

                return true;
            } else {
                // Non-stackable item: add one-by-one
                for (int i = 0; i < quantity; i++) {
                    int emptySlot = FindFirstEmptySlot();
                    if (emptySlot == -1) {
                        Debug.LogWarning($"[PlayerInventory] Inventory full! Could not add {itemData.ItemName}.");
                        EventBus.Trigger("OnCombatError", "Inventory full!");
                        return false;
                    }

                    _inventorySlots[emptySlot] = new ItemSlot(itemId, 1, tier, rarity);
                    Debug.Log($"[PlayerInventory] Added {itemData.ItemName} to slot {emptySlot}.");
                }

                return true;
            }
        }

        /// <summary>
        /// Remove item from specific slot (server-only)
        /// </summary>
        [Server]
        public bool RemoveItem(int slotIndex, int quantity) {
            if (slotIndex < 0 || slotIndex >= _inventorySlots.Count) {
                Debug.LogWarning($"[PlayerInventory] Invalid slot index {slotIndex}.");
                return false;
            }

            ItemSlot slot = _inventorySlots[slotIndex];
            if (slot.IsEmpty) {
                Debug.LogWarning($"[PlayerInventory] Slot {slotIndex} is empty.");
                return false;
            }

            if (quantity <= 0 || quantity > slot.Quantity) {
                Debug.LogWarning($"[PlayerInventory] Invalid quantity {quantity} (slot has {slot.Quantity}).");
                return false;
            }

            slot.Quantity -= quantity;

            if (slot.Quantity <= 0) {
                _inventorySlots[slotIndex] = ItemSlot.Empty;
                Debug.Log($"[PlayerInventory] Removed all items from slot {slotIndex}.");
            } else {
                _inventorySlots[slotIndex] = slot;
                Debug.Log($"[PlayerInventory] Removed {quantity} items from slot {slotIndex} (now {slot.Quantity}).");
            }

            return true;
        }

        /// <summary>
        /// Remove item by ID (searches inventory, server-only)
        /// </summary>
        [Server]
        public bool RemoveItemById(int itemId, int quantity) {
            int remaining = quantity;

            for (int i = 0; i < _inventorySlots.Count && remaining > 0; i++) {
                ItemSlot slot = _inventorySlots[i];
                if (slot.IsEmpty || slot.ItemID != itemId) continue;

                int amountToRemove = Mathf.Min(slot.Quantity, remaining);
                RemoveItem(i, amountToRemove);
                remaining -= amountToRemove;
            }

            if (remaining > 0) {
                Debug.LogWarning($"[PlayerInventory] Could not remove {remaining}x ItemID {itemId} (not enough in inventory).");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Clear all inventory slots (for death/loot drop)
        /// </summary>
        [Server]
        public void ClearInventory() {
            for (int i = 0; i < _inventorySlots.Count; i++) {
                _inventorySlots[i] = ItemSlot.Empty;
            }
            Debug.Log("[PlayerInventory] Inventory cleared.");
        }

        /// <summary>
        /// Get all non-empty items (for loot drop)
        /// </summary>
        [Server]
        public List<ItemSlot> GetAllItems() {
            List<ItemSlot> items = new List<ItemSlot>();

            foreach (var slot in _inventorySlots) {
                if (!slot.IsEmpty) {
                    items.Add(slot);
                }
            }

            return items;
        }

        #endregion

        #region Public Utility Methods (Client-Safe)

        /// <summary>
        /// Check if inventory has space for item
        /// </summary>
        public bool HasSpace(int itemId, int quantity) {
            BaseItemData itemData = ItemDatabase.Instance.GetItem(itemId);
            if (itemData == null) return false;

            if (itemData.IsStackable) {
                int remaining = quantity;

                // Check existing stacks
                for (int i = 0; i < _inventorySlots.Count && remaining > 0; i++) {
                    ItemSlot slot = _inventorySlots[i];
                    if (slot.IsEmpty || slot.ItemID != itemId) continue;

                    int spaceInStack = itemData.MaxStackSize - slot.Quantity;
                    remaining -= spaceInStack;
                }

                // Check empty slots
                int emptySlots = CountEmptySlots();
                remaining -= emptySlots * itemData.MaxStackSize;

                return remaining <= 0;
            } else {
                return CountEmptySlots() >= quantity;
            }
        }

        /// <summary>
        /// Find first empty slot index (-1 if none)
        /// </summary>
        public int FindFirstEmptySlot() {
            for (int i = 0; i < _inventorySlots.Count; i++) {
                if (_inventorySlots[i].IsEmpty) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Count number of empty slots
        /// </summary>
        public int CountEmptySlots() {
            int count = 0;
            foreach (var slot in _inventorySlots) {
                if (slot.IsEmpty) count++;
            }
            return count;
        }

        /// <summary>
        /// Count total quantity of specific item
        /// </summary>
        public int CountItem(int itemId) {
            int total = 0;
            foreach (var slot in _inventorySlots) {
                if (slot.ItemID == itemId) {
                    total += slot.Quantity;
                }
            }
            return total;
        }

        /// <summary>
        /// Get item at specific slot
        /// </summary>
        public ItemSlot GetSlot(int index) {
            if (index < 0 || index >= _inventorySlots.Count) {
                return ItemSlot.Empty;
            }
            return _inventorySlots[index];
        }

        /// <summary>
        /// Force trigger inventory changed event (for UI refresh)
        /// </summary>
        public void ForceRefreshUI() {
            if (IsOwner) {
                Debug.Log("[PlayerInventory] Forcing UI refresh");
                EventBus.Trigger("OnInventoryChanged");
            }
        }

        #endregion

        #region ServerRpc (Client → Server Commands)

        /// <summary>
        /// Client requests to use consumable from inventory
        /// </summary>
        [ServerRpc]
        public void CmdUseConsumable(int slotIndex) {
            // Get consumable handler
            ConsumableHandler consumableHandler = GetComponent<ConsumableHandler>();
            if (consumableHandler == null) {
                Debug.LogError("[PlayerInventory] ConsumableHandler not found!");
                return;
            }

            // Get item in slot
            ItemSlot slot = GetSlot(slotIndex);
            if (slot.IsEmpty) {
                Debug.LogWarning($"[PlayerInventory] Slot {slotIndex} is empty.");
                return;
            }

            // Get consumable data
            ConsumableItemData consumable = ItemDatabase.Instance.GetConsumable(slot.ItemID);
            if (consumable == null) {
                Debug.LogWarning($"[PlayerInventory] Item in slot {slotIndex} is not a consumable.");
                return;
            }

            // Try to use consumable
            bool success = consumableHandler.UseConsumable(consumable, slotIndex);
            if (!success) {
                Debug.LogWarning($"[PlayerInventory] Failed to use consumable from slot {slotIndex}.");
            }
        }

        #endregion
    }
}

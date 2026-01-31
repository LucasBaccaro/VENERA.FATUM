using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;
using Genesis.Items;
using Genesis.Data;
using Genesis.Core;

namespace Genesis.Simulation {
    /// <summary>
    /// Handles dropping loot when player dies
    /// Spawns LootBag with all inventory + equipment items
    /// </summary>
    public class LootDropper : NetworkBehaviour {
        [Header("References")]
        [Tooltip("LootBag prefab to spawn")]
        [SerializeField] private GameObject _lootBagPrefab;

        [Header("Settings")]
        [Tooltip("Offset from player position to spawn loot")]
        [SerializeField] private Vector3 _spawnOffset = new Vector3(0, 0.5f, 0);

        private PlayerInventory _playerInventory;
        private EquipmentManager _equipmentManager;
        private PlayerStats _playerStats;

        private void Awake() {
            // Get components
            _playerInventory = GetComponent<PlayerInventory>();
            _equipmentManager = GetComponent<EquipmentManager>();
            _playerStats = GetComponent<PlayerStats>();
        }

        private void OnEnable() {
            // Subscribe to death event
            EventBus.Subscribe<NetworkObject, NetworkObject>("OnPlayerDied", OnPlayerDied);
        }

        private void OnDisable() {
            // Unsubscribe from death event
            EventBus.Unsubscribe<NetworkObject, NetworkObject>("OnPlayerDied", OnPlayerDied);
        }

        /// <summary>
        /// Callback when any player dies
        /// </summary>
        private void OnPlayerDied(NetworkObject victim, NetworkObject killer) {
            if (!base.IsServer) return;

            // Check if the dead player is this player
            if (victim == null || victim != base.NetworkObject) return;

            Debug.Log($"[LootDropper] {gameObject.name} died. Dropping loot...");

            DropLoot(killer);
        }

        /// <summary>
        /// Drop all items as a loot bag (server-only)
        /// </summary>
        [Server]
        private void DropLoot(NetworkObject killer) {
            if (_lootBagPrefab == null) {
                Debug.LogError("[LootDropper] LootBag prefab is not assigned!");
                return;
            }

            List<ItemSlot> allItems = new List<ItemSlot>();
            int protectedCount = 0;

            // Collect items from inventory (filter protected)
            if (_playerInventory != null) {
                List<ItemSlot> inventoryItems = _playerInventory.GetAllItems();
                foreach (var item in inventoryItems) {
                    if (IsItemProtected(item)) {
                        protectedCount++;
                        continue; // Skip protected items
                    }
                    allItems.Add(item);
                }
                Debug.Log($"[LootDropper] Collected {allItems.Count} items from inventory ({protectedCount} protected, kept)");
            }

            // Collect items from equipment (filter protected)
            int equipmentBefore = allItems.Count;
            if (_equipmentManager != null) {
                List<ItemSlot> equipmentItems = _equipmentManager.GetAllEquipment();
                foreach (var item in equipmentItems) {
                    if (IsItemProtected(item)) {
                        protectedCount++;
                        continue; // Skip protected items
                    }
                    allItems.Add(item);
                }
                Debug.Log($"[LootDropper] Collected {allItems.Count - equipmentBefore} items from equipment ({protectedCount - (allItems.Count - equipmentBefore)} protected, kept)");
            }

            Debug.Log($"[LootDropper] Total items to drop: {allItems.Count} (protected: {protectedCount})");

            // Only spawn loot bag if there are items
            if (allItems.Count == 0) {
                Debug.Log("[LootDropper] No items to drop, skipping loot bag spawn.");
                return;
            }

            // Calculate spawn position
            Vector3 spawnPosition = transform.position + _spawnOffset;

            // Raycast down to find ground
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 10f)) {
                spawnPosition = hit.point + Vector3.up * 0.5f;
            }

            // Spawn loot bag
            GameObject lootBagObj = Instantiate(_lootBagPrefab, spawnPosition, Quaternion.identity);
            base.ServerManager.Spawn(lootBagObj);

            // Initialize loot bag
            LootBag lootBag = lootBagObj.GetComponent<LootBag>();
            if (lootBag != null) {
                string playerName = gameObject.name;
                lootBag.Initialize(allItems, playerName);
                Debug.Log($"[LootDropper] LootBag spawned at {spawnPosition} with {allItems.Count} items");
            } else {
                Debug.LogError("[LootDropper] LootBag component not found on spawned prefab!");
            }

            // Clear only non-protected items from inventory and equipment
            ClearNonProtectedItems();

            Debug.Log("[LootDropper] Non-protected items dropped. Protected items retained.");
        }

        /// <summary>
        /// Check if an item is protected (should not drop on death)
        /// </summary>
        private bool IsItemProtected(ItemSlot itemSlot) {
            if (itemSlot.IsEmpty) return false;

            BaseItemData itemData = ItemDatabase.Instance.GetItem(itemSlot.ItemID);
            if (itemData == null) {
                Debug.LogWarning($"[LootDropper] ItemID {itemSlot.ItemID} not found in database!");
                return false; // If item data not found, treat as non-protected
            }

            return itemData.IsProtected;
        }

        /// <summary>
        /// Clear only non-protected items from inventory and equipment
        /// </summary>
        [Server]
        private void ClearNonProtectedItems() {
            int inventoryCleared = 0;
            int equipmentCleared = 0;

            // Clear non-protected items from inventory
            if (_playerInventory != null) {
                var slots = _playerInventory.InventorySlots;
                for (int i = slots.Count - 1; i >= 0; i--) {
                    ItemSlot slot = slots[i];
                    if (slot.IsEmpty) continue;

                    if (!IsItemProtected(slot)) {
                        _playerInventory.RemoveItem(i, slot.Quantity);
                        inventoryCleared++;
                    }
                }
                Debug.Log($"[LootDropper] Cleared {inventoryCleared} non-protected items from inventory");
            }

            // Clear non-protected items from equipment
            if (_equipmentManager != null) {
                EquipmentSlot[] allSlots = {
                    EquipmentSlot.Head,
                    EquipmentSlot.Chest,
                    EquipmentSlot.Legs,
                    EquipmentSlot.Feet,
                    EquipmentSlot.Hands,
                    EquipmentSlot.Belt
                };

                foreach (var slot in allSlots) {
                    ItemSlot equipmentSlot = _equipmentManager.GetEquipmentSlot(slot);
                    if (!equipmentSlot.IsEmpty && !IsItemProtected(equipmentSlot)) {
                        _equipmentManager.UnequipSlot(slot);
                        equipmentCleared++;
                    }
                }
                Debug.Log($"[LootDropper] Cleared {equipmentCleared} non-protected items from equipment");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() {
            // Draw spawn position
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + _spawnOffset, 0.3f);
        }
#endif
    }
}

using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using UnityEngine;
using Genesis.Items;
using Genesis.Data;
using Genesis.Core;

namespace Genesis.Simulation {
    /// <summary>
    /// Equipment manager with 6 equipment slots
    /// Server-authoritative with SyncVars
    /// </summary>
    public class EquipmentManager : NetworkBehaviour {
        [Header("References")]
        [SerializeField] private PlayerStats _playerStats;

        [Header("Network State - Equipment Slots")]
        private readonly SyncVar<ItemSlot> _headSlot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _chestSlot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _legsSlot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _feetSlot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _handsSlot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _beltSlot = new SyncVar<ItemSlot>();

        // Cached base stats from class
        private float _baseMaxHealth = 100f;
        private float _baseMaxMana = 100f;

        // Current total spell power bonus
        private float _spellPowerBonus = 0f;
        public float SpellPowerBonus => _spellPowerBonus;

        private void Awake() {
            // Subscribe to equipment slot changes
            _headSlot.OnChange += OnEquipmentChanged;
            _chestSlot.OnChange += OnEquipmentChanged;
            _legsSlot.OnChange += OnEquipmentChanged;
            _feetSlot.OnChange += OnEquipmentChanged;
            _handsSlot.OnChange += OnEquipmentChanged;
            _beltSlot.OnChange += OnEquipmentChanged;

            // Auto-find PlayerStats if not assigned
            if (_playerStats == null) {
                _playerStats = GetComponent<PlayerStats>();
            }
        }

        public override void OnStartServer() {
            base.OnStartServer();

            // Initialize empty equipment slots
            _headSlot.Value = ItemSlot.Empty;
            _chestSlot.Value = ItemSlot.Empty;
            _legsSlot.Value = ItemSlot.Empty;
            _feetSlot.Value = ItemSlot.Empty;
            _handsSlot.Value = ItemSlot.Empty;
            _beltSlot.Value = ItemSlot.Empty;

            Debug.Log("[EquipmentManager] Initialized equipment slots.");
        }

        /// <summary>
        /// Callback when equipment changes (synced to clients)
        /// </summary>
        private void OnEquipmentChanged(ItemSlot oldSlot, ItemSlot newSlot, bool asServer) {
            // Recalculate stats on server
            if (asServer) {
                RecalculateStats();
            }

            // Trigger UI update on owner client
            if (IsOwner) {
                EventBus.Trigger("OnEquipmentChanged");
            }
        }

        #region Server Methods

        /// <summary>
        /// Update base stats from class (called by PlayerClassManager)
        /// </summary>
        [Server]
        public void UpdateBaseStats(float baseHealth, float baseMana) {
            _baseMaxHealth = baseHealth;
            _baseMaxMana = baseMana;

            Debug.Log($"[EquipmentManager] Updated base stats: HP={baseHealth}, Mana={baseMana}");

            // Recalculate with new base stats
            RecalculateStats();
        }

        /// <summary>
        /// Equip an item to a specific slot (server-only)
        /// </summary>
        [Server]
        public bool EquipItem(int itemId, ItemTier tier, ItemRarity rarity, EquipmentSlot slot) {
            // Get equipment data
            EquipmentItemData equipmentData = ItemDatabase.Instance.GetEquipment(itemId);
            if (equipmentData == null) {
                Debug.LogWarning($"[EquipmentManager] Item ID {itemId} is not equipment!");
                return false;
            }

            // Validate that item belongs to this slot
            if (equipmentData.Slot != slot) {
                Debug.LogWarning($"[EquipmentManager] Item {equipmentData.ItemName} cannot be equipped to {slot} slot (requires {equipmentData.Slot})!");
                return false;
            }

            // Create item slot
            ItemSlot newSlot = new ItemSlot(itemId, 1, tier, rarity);

            // Equip to appropriate slot
            switch (slot) {
                case EquipmentSlot.Head:
                    _headSlot.Value = newSlot;
                    break;
                case EquipmentSlot.Chest:
                    _chestSlot.Value = newSlot;
                    break;
                case EquipmentSlot.Legs:
                    _legsSlot.Value = newSlot;
                    break;
                case EquipmentSlot.Feet:
                    _feetSlot.Value = newSlot;
                    break;
                case EquipmentSlot.Hands:
                    _handsSlot.Value = newSlot;
                    break;
                case EquipmentSlot.Belt:
                    _beltSlot.Value = newSlot;
                    break;
            }

            Debug.Log($"[EquipmentManager] Equipped {equipmentData.ItemName} ({rarity}) to {slot} slot.");
            return true;
        }

        /// <summary>
        /// Unequip a specific slot (server-only)
        /// </summary>
        [Server]
        public ItemSlot UnequipSlot(EquipmentSlot slot) {
            ItemSlot unequippedItem = ItemSlot.Empty;

            switch (slot) {
                case EquipmentSlot.Head:
                    unequippedItem = _headSlot.Value;
                    _headSlot.Value = ItemSlot.Empty;
                    break;
                case EquipmentSlot.Chest:
                    unequippedItem = _chestSlot.Value;
                    _chestSlot.Value = ItemSlot.Empty;
                    break;
                case EquipmentSlot.Legs:
                    unequippedItem = _legsSlot.Value;
                    _legsSlot.Value = ItemSlot.Empty;
                    break;
                case EquipmentSlot.Feet:
                    unequippedItem = _feetSlot.Value;
                    _feetSlot.Value = ItemSlot.Empty;
                    break;
                case EquipmentSlot.Hands:
                    unequippedItem = _handsSlot.Value;
                    _handsSlot.Value = ItemSlot.Empty;
                    break;
                case EquipmentSlot.Belt:
                    unequippedItem = _beltSlot.Value;
                    _beltSlot.Value = ItemSlot.Empty;
                    break;
            }

            if (!unequippedItem.IsEmpty) {
                Debug.Log($"[EquipmentManager] Unequipped item from {slot} slot.");
            }

            return unequippedItem;
        }

        /// <summary>
        /// Clear all equipment (for death/loot drop)
        /// </summary>
        [Server]
        public void ClearAllEquipment() {
            _headSlot.Value = ItemSlot.Empty;
            _chestSlot.Value = ItemSlot.Empty;
            _legsSlot.Value = ItemSlot.Empty;
            _feetSlot.Value = ItemSlot.Empty;
            _handsSlot.Value = ItemSlot.Empty;
            _beltSlot.Value = ItemSlot.Empty;

            Debug.Log("[EquipmentManager] Cleared all equipment.");
        }

        /// <summary>
        /// Get all equipped items (for loot drop)
        /// </summary>
        [Server]
        public List<ItemSlot> GetAllEquipment() {
            List<ItemSlot> items = new List<ItemSlot>();

            if (!_headSlot.Value.IsEmpty) items.Add(_headSlot.Value);
            if (!_chestSlot.Value.IsEmpty) items.Add(_chestSlot.Value);
            if (!_legsSlot.Value.IsEmpty) items.Add(_legsSlot.Value);
            if (!_feetSlot.Value.IsEmpty) items.Add(_feetSlot.Value);
            if (!_handsSlot.Value.IsEmpty) items.Add(_handsSlot.Value);
            if (!_beltSlot.Value.IsEmpty) items.Add(_beltSlot.Value);

            return items;
        }

        /// <summary>
        /// Recalculate stats based on equipped items (server-only)
        /// </summary>
        [Server]
        public void RecalculateStats() {
            if (_playerStats == null) {
                Debug.LogWarning("[EquipmentManager] PlayerStats reference is null!");
                return;
            }

            // Start with base stats
            float totalMaxHealth = _baseMaxHealth;
            float totalMaxMana = _baseMaxMana;
            float totalSpellPower = 0f;

            // Helper function to process equipment slot
            void ProcessSlot(ItemSlot slot) {
                if (slot.IsEmpty) return;

                EquipmentItemData equipmentData = ItemDatabase.Instance.GetEquipment(slot.ItemID);
                if (equipmentData == null) return;

                // Get stat modifiers for this item's rarity
                List<StatModifier> stats = equipmentData.GetStatsForRarity(slot.Rarity);
                if (stats == null) return;

                // Apply each stat modifier
                foreach (var stat in stats) {
                    switch (stat.Type) {
                        case StatType.MaxHealth:
                            totalMaxHealth += stat.Value;
                            break;
                        case StatType.MaxMana:
                            totalMaxMana += stat.Value;
                            break;
                        case StatType.SpellPower:
                            totalSpellPower += stat.Value;
                            break;
                    }
                }
            }

            // Process all equipment slots
            ProcessSlot(_headSlot.Value);
            ProcessSlot(_chestSlot.Value);
            ProcessSlot(_legsSlot.Value);
            ProcessSlot(_feetSlot.Value);
            ProcessSlot(_handsSlot.Value);
            ProcessSlot(_beltSlot.Value);

            // Update PlayerStats
            _playerStats.SetMaxHealth(totalMaxHealth);
            _playerStats.SetMaxMana(totalMaxMana);
            _spellPowerBonus = totalSpellPower;

            Debug.Log($"[EquipmentManager] Stats recalculated: MaxHP={totalMaxHealth}, MaxMana={totalMaxMana}, SpellPower={totalSpellPower * 100f}%");
        }

        #endregion

        #region Public Utility Methods (Client-Safe)

        /// <summary>
        /// Get item in specific equipment slot
        /// </summary>
        public ItemSlot GetEquipmentSlot(EquipmentSlot slot) {
            switch (slot) {
                case EquipmentSlot.Head: return _headSlot.Value;
                case EquipmentSlot.Chest: return _chestSlot.Value;
                case EquipmentSlot.Legs: return _legsSlot.Value;
                case EquipmentSlot.Feet: return _feetSlot.Value;
                case EquipmentSlot.Hands: return _handsSlot.Value;
                case EquipmentSlot.Belt: return _beltSlot.Value;
                default: return ItemSlot.Empty;
            }
        }

        /// <summary>
        /// Check if specific slot is empty
        /// </summary>
        public bool IsSlotEmpty(EquipmentSlot slot) {
            return GetEquipmentSlot(slot).IsEmpty;
        }

        #endregion

        #region ServerRpc (Client → Server Commands)

        /// <summary>
        /// Client requests to equip item from inventory
        /// </summary>
        [ServerRpc]
        public void CmdEquipFromInventory(int inventorySlotIndex) {
            PlayerInventory inventory = GetComponent<PlayerInventory>();
            if (inventory == null) {
                Debug.LogError("[EquipmentManager] PlayerInventory not found!");
                return;
            }

            // Get item from inventory
            ItemSlot inventorySlot = inventory.GetSlot(inventorySlotIndex);
            if (inventorySlot.IsEmpty) {
                Debug.LogWarning($"[EquipmentManager] Inventory slot {inventorySlotIndex} is empty.");
                return;
            }

            // Get equipment data
            EquipmentItemData equipmentData = ItemDatabase.Instance.GetEquipment(inventorySlot.ItemID);
            if (equipmentData == null) {
                Debug.LogWarning($"[EquipmentManager] Item in inventory slot {inventorySlotIndex} is not equipment.");
                return;
            }

            // Get current item in equipment slot (if any)
            ItemSlot currentEquipment = GetEquipmentSlot(equipmentData.Slot);

            // Equip new item
            bool success = EquipItem(inventorySlot.ItemID, inventorySlot.Tier, inventorySlot.Rarity, equipmentData.Slot);
            if (!success) {
                Debug.LogWarning($"[EquipmentManager] Failed to equip item from inventory slot {inventorySlotIndex}.");
                return;
            }

            // Remove from inventory
            inventory.RemoveItem(inventorySlotIndex, 1);

            // If there was an item equipped, return it to inventory
            if (!currentEquipment.IsEmpty) {
                inventory.AddItem(currentEquipment.ItemID, 1, currentEquipment.Tier, currentEquipment.Rarity);
            }

            Debug.Log($"[EquipmentManager] Equipped {equipmentData.ItemName} from inventory slot {inventorySlotIndex}.");
        }

        /// <summary>
        /// Client requests to unequip item to inventory
        /// </summary>
        [ServerRpc]
        public void CmdUnequipToInventory(EquipmentSlot slot) {
            PlayerInventory inventory = GetComponent<PlayerInventory>();
            if (inventory == null) {
                Debug.LogError("[EquipmentManager] PlayerInventory not found!");
                return;
            }

            // Get current item in slot
            ItemSlot currentItem = GetEquipmentSlot(slot);
            if (currentItem.IsEmpty) {
                Debug.LogWarning($"[EquipmentManager] Equipment slot {slot} is already empty.");
                return;
            }

            // Check if inventory has space
            if (!inventory.HasSpace(currentItem.ItemID, 1)) {
                Debug.LogWarning("[EquipmentManager] Inventory is full!");
                EventBus.Trigger("OnCombatError", "Inventory full!");
                return;
            }

            // Unequip item
            ItemSlot unequippedItem = UnequipSlot(slot);

            // Add to inventory
            inventory.AddItem(unequippedItem.ItemID, 1, unequippedItem.Tier, unequippedItem.Rarity);

            Debug.Log($"[EquipmentManager] Unequipped item from {slot} slot to inventory.");
        }

        #endregion
    }
}

using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using UnityEngine;
using Genesis.Items;
using Genesis.Data;
using Genesis.Core;
using Genesis.Core.Persistence;

namespace Genesis.Simulation {
    /// <summary>
    /// Equipment manager with 6 equipment slots
    /// Server-authoritative with SyncVars
    /// </summary>
    public class EquipmentManager : NetworkBehaviour {
        [Header("References")]
        [SerializeField] private PlayerStats _playerStats;
        private PlayerClassManager _classManager;
        private PlayerAttributes _playerAttributes;

        [Header("Network State - Equipment Slots")]
        private readonly SyncVar<ItemSlot> _headSlot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _shouldersSlot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _chestSlot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _pantsSlot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _feetSlot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _handsSlot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _beltSlot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _weaponSlot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _offHandSlot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _ring1Slot = new SyncVar<ItemSlot>();
        private readonly SyncVar<ItemSlot> _ring2Slot = new SyncVar<ItemSlot>();

        // Cached base stats from class
        private float _baseMaxHealth = 100f;
        private float _baseMaxMana = 100f;
        private float _healthPerLevel = 0f;
        private float _manaPerLevel = 0f;

        private void Awake() {
            // Subscribe to equipment slot changes with specific slot info
            _headSlot.OnChange += (oldVal, newVal, asServer) => OnEquipmentChanged(EquipmentSlot.Head, oldVal, newVal, asServer);
            _shouldersSlot.OnChange += (oldVal, newVal, asServer) => OnEquipmentChanged(EquipmentSlot.Shoulders, oldVal, newVal, asServer);
            _chestSlot.OnChange += (oldVal, newVal, asServer) => OnEquipmentChanged(EquipmentSlot.Chest, oldVal, newVal, asServer);
            _pantsSlot.OnChange += (oldVal, newVal, asServer) => OnEquipmentChanged(EquipmentSlot.Pants, oldVal, newVal, asServer);
            _feetSlot.OnChange += (oldVal, newVal, asServer) => OnEquipmentChanged(EquipmentSlot.Feet, oldVal, newVal, asServer);
            _handsSlot.OnChange += (oldVal, newVal, asServer) => OnEquipmentChanged(EquipmentSlot.Hands, oldVal, newVal, asServer);
            _beltSlot.OnChange += (oldVal, newVal, asServer) => OnEquipmentChanged(EquipmentSlot.Belt, oldVal, newVal, asServer);
            _weaponSlot.OnChange += (oldVal, newVal, asServer) => OnEquipmentChanged(EquipmentSlot.Weapon, oldVal, newVal, asServer);
            _offHandSlot.OnChange += (oldVal, newVal, asServer) => OnEquipmentChanged(EquipmentSlot.OffHand, oldVal, newVal, asServer);
            _ring1Slot.OnChange += (oldVal, newVal, asServer) => OnEquipmentChanged(EquipmentSlot.Ring1, oldVal, newVal, asServer);
            _ring2Slot.OnChange += (oldVal, newVal, asServer) => OnEquipmentChanged(EquipmentSlot.Ring2, oldVal, newVal, asServer);

            // Auto-find components
            if (_playerStats == null) {
                _playerStats = GetComponent<PlayerStats>();
            }
            if (_classManager == null) {
                _classManager = GetComponent<PlayerClassManager>();
            }
            _playerAttributes = GetComponent<PlayerAttributes>();
        }

        public override void OnStartServer() {
            base.OnStartServer();

            // Initialize empty equipment slots
            _headSlot.Value = ItemSlot.Empty;
            _shouldersSlot.Value = ItemSlot.Empty;
            _chestSlot.Value = ItemSlot.Empty;
            _pantsSlot.Value = ItemSlot.Empty;
            _feetSlot.Value = ItemSlot.Empty;
            _handsSlot.Value = ItemSlot.Empty;
            _beltSlot.Value = ItemSlot.Empty;
            _weaponSlot.Value = ItemSlot.Empty;
            _offHandSlot.Value = ItemSlot.Empty;
            _ring1Slot.Value = ItemSlot.Empty;
            _ring2Slot.Value = ItemSlot.Empty;

            Debug.Log("[EquipmentManager] Initialized equipment slots.");
        }

        /// <summary>
        /// Event triggered when any equipment slot changes (called on all clients)
        /// Passes the slot that changed.
        /// </summary>
        public System.Action<EquipmentSlot> OnEquipmentChangedSync;

        /// <summary>
        /// Callback when equipment changes (synced to clients)
        /// </summary>
        private void OnEquipmentChanged(EquipmentSlot slot, ItemSlot oldSlot, ItemSlot newSlot, bool asServer) {
            // Recalculate stats on server
            if (asServer) {
                RecalculateStats();
            }

            // Trigger local visual update for all clients (Proxies and Owner)
            OnEquipmentChangedSync?.Invoke(slot);

            // Trigger global event for owner-only UI modules (Inventory, Character Panel, etc.)
            if (IsOwner) {
                EventBus.Trigger("OnEquipmentChanged");
                bool isEquip = !newSlot.IsEmpty;
                EventBus.Trigger("OnEquipmentSound", isEquip);
            }
        }

        #region Server Methods

        /// <summary>
        /// Update base stats from class (called by PlayerClassManager)
        /// </summary>
        [Server]
        public void UpdateBaseStats(float baseHealth, float baseMana, float healthPerLevel = 0f, float manaPerLevel = 0f) {
            _baseMaxHealth = baseHealth;
            _baseMaxMana = baseMana;
            _healthPerLevel = healthPerLevel;
            _manaPerLevel = manaPerLevel;

            Debug.Log($"[EquipmentManager] Updated base stats: HP={baseHealth}, Mana={baseMana}, HP/Lvl={healthPerLevel}, Mana/Lvl={manaPerLevel}");

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

            // Validate Class Restriction
            if (!string.IsNullOrEmpty(equipmentData.RequiredClass) && _classManager != null) {
                string currentClass = _classManager.GetCurrentClassName();
                if (equipmentData.RequiredClass != currentClass) {
                    Debug.LogWarning($"[EquipmentManager] Item {equipmentData.ItemName} requires class {equipmentData.RequiredClass} but player is {currentClass}");
                    TargetShowError(base.Owner, "This item is not for your class");
                    return false;
                }
            }

            // Validate Level Requirement
            if (equipmentData.RequiredLevel > 0 && _playerAttributes != null) {
                if (_playerAttributes.Level < equipmentData.RequiredLevel) {
                    TargetShowError(base.Owner, $"Requires level {equipmentData.RequiredLevel}");
                    return false;
                }
            }

            // Create item slot
            ItemSlot newSlot = new ItemSlot(itemId, 1, tier, rarity);

            // Equip to appropriate slot
            switch (slot) {
                case EquipmentSlot.Head:
                    _headSlot.Value = newSlot;
                    break;
                case EquipmentSlot.Shoulders:
                    _shouldersSlot.Value = newSlot;
                    break;
                case EquipmentSlot.Chest:
                    _chestSlot.Value = newSlot;
                    break;
                case EquipmentSlot.Pants:
                    _pantsSlot.Value = newSlot;
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
                case EquipmentSlot.Weapon:
                    _weaponSlot.Value = newSlot;
                    break;
                case EquipmentSlot.OffHand:
                    _offHandSlot.Value = newSlot;
                    break;
                case EquipmentSlot.Ring1:
                    _ring1Slot.Value = newSlot;
                    break;
                case EquipmentSlot.Ring2:
                    _ring2Slot.Value = newSlot;
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
                case EquipmentSlot.Shoulders:
                    unequippedItem = _shouldersSlot.Value;
                    _shouldersSlot.Value = ItemSlot.Empty;
                    break;
                case EquipmentSlot.Chest:
                    unequippedItem = _chestSlot.Value;
                    _chestSlot.Value = ItemSlot.Empty;
                    break;
                case EquipmentSlot.Pants:
                    unequippedItem = _pantsSlot.Value;
                    _pantsSlot.Value = ItemSlot.Empty;
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
                case EquipmentSlot.Weapon:
                    unequippedItem = _weaponSlot.Value;
                    _weaponSlot.Value = ItemSlot.Empty;
                    break;
                case EquipmentSlot.OffHand:
                    unequippedItem = _offHandSlot.Value;
                    _offHandSlot.Value = ItemSlot.Empty;
                    break;
                case EquipmentSlot.Ring1:
                    unequippedItem = _ring1Slot.Value;
                    _ring1Slot.Value = ItemSlot.Empty;
                    break;
                case EquipmentSlot.Ring2:
                    unequippedItem = _ring2Slot.Value;
                    _ring2Slot.Value = ItemSlot.Empty;
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
            _shouldersSlot.Value = ItemSlot.Empty;
            _chestSlot.Value = ItemSlot.Empty;
            _pantsSlot.Value = ItemSlot.Empty;
            _feetSlot.Value = ItemSlot.Empty;
            _handsSlot.Value = ItemSlot.Empty;
            _beltSlot.Value = ItemSlot.Empty;
            _weaponSlot.Value = ItemSlot.Empty;
            _offHandSlot.Value = ItemSlot.Empty;
            _ring1Slot.Value = ItemSlot.Empty;
            _ring2Slot.Value = ItemSlot.Empty;

            Debug.Log("[EquipmentManager] Cleared all equipment.");
        }

        /// <summary>
        /// Get all equipped items (for loot drop)
        /// </summary>
        [Server]
        public List<ItemSlot> GetAllEquipment() {
            List<ItemSlot> items = new List<ItemSlot>();

            if (!_headSlot.Value.IsEmpty) items.Add(_headSlot.Value);
            if (!_shouldersSlot.Value.IsEmpty) items.Add(_shouldersSlot.Value);
            if (!_chestSlot.Value.IsEmpty) items.Add(_chestSlot.Value);
            if (!_pantsSlot.Value.IsEmpty) items.Add(_pantsSlot.Value);
            if (!_feetSlot.Value.IsEmpty) items.Add(_feetSlot.Value);
            if (!_handsSlot.Value.IsEmpty) items.Add(_handsSlot.Value);
            if (!_beltSlot.Value.IsEmpty) items.Add(_beltSlot.Value);
            if (!_weaponSlot.Value.IsEmpty) items.Add(_weaponSlot.Value);
            if (!_offHandSlot.Value.IsEmpty) items.Add(_offHandSlot.Value);
            if (!_ring1Slot.Value.IsEmpty) items.Add(_ring1Slot.Value);
            if (!_ring2Slot.Value.IsEmpty) items.Add(_ring2Slot.Value);

            return items;
        }

        // ═══════════════════════════════════════════════════════
        // PERSISTENCE HYDRATION
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Directly set equipment slots from saved data (server-only).
        /// Slot order matches EquipmentSlot enum: Head=0, Shoulders=1, ..., Ring2=10
        /// </summary>
        [Server]
        public void HydrateFromSave(SerializedItemSlot[] slots) {
            if (slots == null || slots.Length != 11) return;

            SetSlotDirect(EquipmentSlot.Head, slots[0]);
            SetSlotDirect(EquipmentSlot.Shoulders, slots[1]);
            SetSlotDirect(EquipmentSlot.Chest, slots[2]);
            SetSlotDirect(EquipmentSlot.Pants, slots[3]);
            SetSlotDirect(EquipmentSlot.Feet, slots[4]);
            SetSlotDirect(EquipmentSlot.Hands, slots[5]);
            SetSlotDirect(EquipmentSlot.Belt, slots[6]);
            SetSlotDirect(EquipmentSlot.Weapon, slots[7]);
            SetSlotDirect(EquipmentSlot.OffHand, slots[8]);
            SetSlotDirect(EquipmentSlot.Ring1, slots[9]);
            SetSlotDirect(EquipmentSlot.Ring2, slots[10]);

            RecalculateStats();
        }

        [Server]
        private void SetSlotDirect(EquipmentSlot slot, SerializedItemSlot data) {
            ItemSlot itemSlot = data.itemId > 0
                ? new ItemSlot(data.itemId, data.quantity, (ItemTier)data.tier, (ItemRarity)data.rarity)
                : ItemSlot.Empty;

            switch (slot) {
                case EquipmentSlot.Head: _headSlot.Value = itemSlot; break;
                case EquipmentSlot.Shoulders: _shouldersSlot.Value = itemSlot; break;
                case EquipmentSlot.Chest: _chestSlot.Value = itemSlot; break;
                case EquipmentSlot.Pants: _pantsSlot.Value = itemSlot; break;
                case EquipmentSlot.Feet: _feetSlot.Value = itemSlot; break;
                case EquipmentSlot.Hands: _handsSlot.Value = itemSlot; break;
                case EquipmentSlot.Belt: _beltSlot.Value = itemSlot; break;
                case EquipmentSlot.Weapon: _weaponSlot.Value = itemSlot; break;
                case EquipmentSlot.OffHand: _offHandSlot.Value = itemSlot; break;
                case EquipmentSlot.Ring1: _ring1Slot.Value = itemSlot; break;
                case EquipmentSlot.Ring2: _ring2Slot.Value = itemSlot; break;
            }
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

            // Start with base stats + per-level growth
            int level = _playerAttributes != null ? _playerAttributes.Level : 1;
            float totalMaxHealth = _baseMaxHealth + (level - 1) * _healthPerLevel;
            float totalMaxMana = _baseMaxMana + (level - 1) * _manaPerLevel;

            // Attribute bonuses from equipment
            int equipStr = 0, equipAgi = 0, equipInt = 0, equipWis = 0, equipCon = 0;
            float equipHaste = 0f, equipLifeSteal = 0f, equipPenetration = 0f, equipBlock = 0f;
            float equipLootLuck = 0f, equipLockpicking = 0f, equipPerception = 0f, equipMoveSpeed = 0f;
            float equipArmor = 0f, equipMagicResistance = 0f;

            // Helper function to process equipment slot
            void ProcessSlot(ItemSlot slot) {
                if (slot.IsEmpty) return;

                EquipmentItemData equipmentData = ItemDatabase.Instance.GetEquipment(slot.ItemID);
                if (equipmentData == null) return;

                List<StatModifier> stats = equipmentData.GetStatsForRarity(slot.Rarity);
                if (stats == null) return;

                foreach (var stat in stats) {
                    switch (stat.Type) {
                        case StatType.MaxHealth: totalMaxHealth += stat.Value; break;
                        case StatType.MaxMana: totalMaxMana += stat.Value; break;
                        case StatType.Strength: equipStr += (int)stat.Value; break;
                        case StatType.Agility: equipAgi += (int)stat.Value; break;
                        case StatType.Intelligence: equipInt += (int)stat.Value; break;
                        case StatType.Wisdom: equipWis += (int)stat.Value; break;
                        case StatType.Constitution: equipCon += (int)stat.Value; break;
                        case StatType.Haste: equipHaste += stat.Value; break;
                        case StatType.LifeSteal: equipLifeSteal += stat.Value; break;
                        case StatType.Penetration: equipPenetration += stat.Value; break;
                        case StatType.Block: equipBlock += stat.Value; break;
                        case StatType.LootLuck: equipLootLuck += stat.Value; break;
                        case StatType.Lockpicking: equipLockpicking += stat.Value; break;
                        case StatType.Perception: equipPerception += stat.Value; break;
                        case StatType.MoveSpeed: equipMoveSpeed += stat.Value; break;
                        case StatType.Armor: equipArmor += stat.Value; break;
                        case StatType.MagicResistance: equipMagicResistance += stat.Value; break;
                    }
                }
            }

            // Process all 11 equipment slots
            ProcessSlot(_headSlot.Value);
            ProcessSlot(_shouldersSlot.Value);
            ProcessSlot(_chestSlot.Value);
            ProcessSlot(_pantsSlot.Value);
            ProcessSlot(_feetSlot.Value);
            ProcessSlot(_handsSlot.Value);
            ProcessSlot(_beltSlot.Value);
            ProcessSlot(_weaponSlot.Value);
            ProcessSlot(_offHandSlot.Value);
            ProcessSlot(_ring1Slot.Value);
            ProcessSlot(_ring2Slot.Value);

            // Add CON bonus to max health and WIS bonus to max mana
            if (_playerAttributes != null) {
                totalMaxHealth += (_playerAttributes.BaseConstitution + equipCon) *
                    (_playerAttributes.Config != null ? _playerAttributes.Config.HealthPerPoint : 5f);
                totalMaxMana += (_playerAttributes.BaseWisdom + equipWis) *
                    (_playerAttributes.Config != null ? _playerAttributes.Config.MaxManaPerPoint : 3f);
            }

            // Update PlayerStats
            _playerStats.SetMaxHealth(totalMaxHealth);
            _playerStats.SetMaxMana(totalMaxMana);

            // Update PlayerAttributes with equipment bonuses
            if (_playerAttributes != null) {
                _playerAttributes.SetEquipmentBonuses(
                    equipStr, equipAgi, equipInt, equipWis, equipCon,
                    equipHaste, equipLifeSteal, equipPenetration, equipBlock,
                    equipLootLuck, equipLockpicking, equipPerception, equipMoveSpeed,
                    equipArmor, equipMagicResistance);
            }

            Debug.Log($"[EquipmentManager] Stats recalculated: MaxHP={totalMaxHealth}, MaxMana={totalMaxMana}");
        }

        /// <summary>
        /// Re-validate all equipped items against the current class.
        /// Unequip invalid items to inventory.
        /// </summary>
        [Server]
        public void ValidateEquipmentForClass(string className) {
            PlayerInventory inventory = GetComponent<PlayerInventory>();
            if (inventory == null) return;

             void CheckSlot(EquipmentSlot slot, ItemSlot itemSlot) {
                if (itemSlot.IsEmpty) return;

                EquipmentItemData data = ItemDatabase.Instance.GetEquipment(itemSlot.ItemID);
                if (data == null) return;

                if (!string.IsNullOrEmpty(data.RequiredClass) && data.RequiredClass != className) {
                    Debug.Log($"[EquipmentManager] Auto-unequipping {data.ItemName} (Requires {data.RequiredClass}). New class is {className}.");
                    
                    if (inventory.HasSpace(itemSlot.ItemID, 1)) {
                        ItemSlot unequipped = UnequipSlot(slot);
                        inventory.AddItem(unequipped.ItemID, 1, unequipped.Tier, unequipped.Rarity);
                    } else {
                        // Inventory full - Drop to ground? For now just warning.
                         Debug.LogWarning($"[EquipmentManager] Inventory full! Could not auto-unequip {data.ItemName}. Item remains equipped but invalid.");
                         // Alternative: Force unequip and drop world item (Phase 10)
                    }
                }
            }

            CheckSlot(EquipmentSlot.Head, _headSlot.Value);
            CheckSlot(EquipmentSlot.Shoulders, _shouldersSlot.Value);
            CheckSlot(EquipmentSlot.Chest, _chestSlot.Value);
            CheckSlot(EquipmentSlot.Pants, _pantsSlot.Value);
            CheckSlot(EquipmentSlot.Feet, _feetSlot.Value);
            CheckSlot(EquipmentSlot.Hands, _handsSlot.Value);
            CheckSlot(EquipmentSlot.Belt, _beltSlot.Value);
            CheckSlot(EquipmentSlot.Weapon, _weaponSlot.Value);
            CheckSlot(EquipmentSlot.OffHand, _offHandSlot.Value);
            CheckSlot(EquipmentSlot.Ring1, _ring1Slot.Value);
            CheckSlot(EquipmentSlot.Ring2, _ring2Slot.Value);

            // Recalculate stats after potential changes
            RecalculateStats();
        }

        #endregion

        #region Public Utility Methods (Client-Safe)

        /// <summary>
        /// Get item in specific equipment slot
        /// </summary>
        public ItemSlot GetEquipmentSlot(EquipmentSlot slot) {
            switch (slot) {
                case EquipmentSlot.Head: return _headSlot.Value;
                case EquipmentSlot.Shoulders: return _shouldersSlot.Value;
                case EquipmentSlot.Chest: return _chestSlot.Value;
                case EquipmentSlot.Pants: return _pantsSlot.Value;
                case EquipmentSlot.Feet: return _feetSlot.Value;
                case EquipmentSlot.Hands: return _handsSlot.Value;
                case EquipmentSlot.Belt: return _beltSlot.Value;
                case EquipmentSlot.Weapon: return _weaponSlot.Value;
                case EquipmentSlot.OffHand: return _offHandSlot.Value;
                case EquipmentSlot.Ring1: return _ring1Slot.Value;
                case EquipmentSlot.Ring2: return _ring2Slot.Value;
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
        [TargetRpc]
        private void TargetShowError(FishNet.Connection.NetworkConnection conn, string message) {
            EventBus.Trigger("OnCombatError", message);
        }
    }
}

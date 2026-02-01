using FishNet.Object;
using System.Collections;
using UnityEngine;
using Genesis.Items;

namespace Genesis.Simulation {
    /// <summary>
    /// Grants starter items when player spawns
    /// Adds potions and basic T0 equipment
    /// </summary>
    public class StarterItemGranter : NetworkBehaviour {
        [Header("Starter Consumables")]
        [Tooltip("Health potion item ID")]
        [SerializeField] private int _healthPotionID = 1001;

        [Tooltip("Mana potion item ID")]
        [SerializeField] private int _manaPotionID = 1002;

        [Tooltip("Number of each potion to grant")]
        [SerializeField] private int _potionQuantity = 5;

        [Header("Starter Equipment")]
        [Tooltip("List of equipment item IDs to grant to the inventory")]
        [SerializeField] private int[] _starterEquipmentIDs = { 2001, 2002, 2003, 2004, 2005, 2006, 2007, 2008 };

        [Header("Settings")]
        [Tooltip("Delay before granting items (to ensure components are initialized)")]
        [SerializeField] private float _grantDelay = 0.5f;

        [Tooltip("Default tier for starter equipment")]
        [SerializeField] private ItemTier _starterTier = ItemTier.T0;

        [Tooltip("Default rarity for starter equipment")]
        [SerializeField] private ItemRarity _starterRarity = ItemRarity.Common;

        private PlayerInventory _playerInventory;
        private EquipmentManager _equipmentManager;

        private void Awake() {
            // Get components
            _playerInventory = GetComponent<PlayerInventory>();
            _equipmentManager = GetComponent<EquipmentManager>();
        }

        public override void OnStartServer() {
            base.OnStartServer();

            // Grant items after a short delay
            StartCoroutine(GrantStarterItemsDelayed());
        }

        /// <summary>
        /// Grant starter items with delay (server-only)
        /// </summary>
        private IEnumerator GrantStarterItemsDelayed() {
            yield return new WaitForSeconds(_grantDelay);

            if (_playerInventory == null || _equipmentManager == null) {
                Debug.LogError("[StarterItemGranter] Missing required components!");
                yield break;
            }

            Debug.Log($"[StarterItemGranter] Granting starter items to {gameObject.name}");

            // Add potions to inventory
            AddPotion(_healthPotionID, _potionQuantity);
            AddPotion(_manaPotionID, _potionQuantity);

            // Add Starter Equipment to inventory (unequipped)
            foreach (int itemId in _starterEquipmentIDs) {
                if (itemId > 0) {
                    _playerInventory.AddItem(itemId, 1, _starterTier, _starterRarity);
                }
            }

            Debug.Log($"[StarterItemGranter] Finished granting starter items to {gameObject.name}");
        }

        /// <summary>
        /// Add potion to inventory (server-only)
        /// </summary>
        [Server]
        private void AddPotion(int itemId, int quantity) {
            if (_playerInventory == null) return;

            bool success = _playerInventory.AddItem(itemId, quantity, ItemTier.T0, ItemRarity.Common);
            if (success) {
                Debug.Log($"[StarterItemGranter] Added {quantity}x ItemID {itemId} to inventory");
            } else {
                Debug.LogWarning($"[StarterItemGranter] Failed to add {quantity}x ItemID {itemId}");
            }
        }

        /// <summary>
        /// Equip item (server-only)
        /// </summary>
        [Server]
        private void EquipItem(int itemId, EquipmentSlot slot) {
            if (_equipmentManager == null) return;

            bool success = _equipmentManager.EquipItem(itemId, _starterTier, _starterRarity, slot);
            if (success) {
                Debug.Log($"[StarterItemGranter] Equipped ItemID {itemId} to {slot} slot");
            } else {
                Debug.LogWarning($"[StarterItemGranter] Failed to equip ItemID {itemId} to {slot}");
            }
        }
    }
}

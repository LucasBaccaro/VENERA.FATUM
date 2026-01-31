using FishNet.Object;
using UnityEngine;
using Genesis.Items;
using Genesis.Core;

namespace Genesis.Simulation {
    /// <summary>
    /// Handles consumable usage (potions, food, buffs)
    /// Server-authoritative with global cooldown
    /// </summary>
    public class ConsumableHandler : NetworkBehaviour {
        [Header("References")]
        [SerializeField] private PlayerStats _playerStats;
        [SerializeField] private PlayerInventory _playerInventory;

        [Header("Cooldown Settings")]
        [Tooltip("Global potion cooldown in seconds")]
        [SerializeField] private float _globalPotionCooldown = 3f;

        private float _lastPotionUseTime = -999f;

        private void Awake() {
            // Auto-find references if not assigned
            if (_playerStats == null) {
                _playerStats = GetComponent<PlayerStats>();
            }

            if (_playerInventory == null) {
                _playerInventory = GetComponent<PlayerInventory>();
            }
        }

        #region Server Methods

        /// <summary>
        /// Use a consumable item (server-only)
        /// </summary>
        [Server]
        public bool UseConsumable(ConsumableItemData consumable, int inventorySlotIndex) {
            if (consumable == null) {
                Debug.LogWarning("[ConsumableHandler] Consumable data is null!");
                return false;
            }

            // Check if player is dead
            if (_playerStats.IsDead) {
                Debug.LogWarning("[ConsumableHandler] Cannot use consumable while dead!");
                EventBus.Trigger("OnCombatError", "Cannot use while dead!");
                return false;
            }

            // Check global cooldown
            float timeSinceLastUse = Time.time - _lastPotionUseTime;
            if (timeSinceLastUse < _globalPotionCooldown) {
                float remaining = _globalPotionCooldown - timeSinceLastUse;
                Debug.LogWarning($"[ConsumableHandler] Potion on cooldown! {remaining:F1}s remaining.");
                EventBus.Trigger("OnCombatError", $"On cooldown! ({remaining:F1}s)");
                return false;
            }

            // Apply effect based on consumable type
            bool effectApplied = false;

            switch (consumable.ConsumableType) {
                case ConsumableType.HealthPotion:
                    // Check if already at max HP
                    if (_playerStats.CurrentHealth >= _playerStats.MaxHealth) {
                        Debug.LogWarning("[ConsumableHandler] Already at max HP!");
                        EventBus.Trigger("OnCombatError", "Already at max HP!");
                        return false;
                    }

                    // Heal player
                    _playerStats.Heal(consumable.RestoreAmount, base.NetworkObject);
                    effectApplied = true;
                    Debug.Log($"[ConsumableHandler] Used {consumable.ItemName}, restored {consumable.RestoreAmount} HP.");
                    break;

                case ConsumableType.ManaPotion:
                    // Check if already at max Mana
                    if (_playerStats.CurrentMana >= _playerStats.MaxMana) {
                        Debug.LogWarning("[ConsumableHandler] Already at max Mana!");
                        EventBus.Trigger("OnCombatError", "Already at max Mana!");
                        return false;
                    }

                    // Restore mana
                    _playerStats.RestoreMana(consumable.RestoreAmount);
                    effectApplied = true;
                    Debug.Log($"[ConsumableHandler] Used {consumable.ItemName}, restored {consumable.RestoreAmount} Mana.");
                    break;

                default:
                    Debug.LogWarning($"[ConsumableHandler] Consumable type '{consumable.ConsumableType}' not implemented yet!");
                    return false;
            }

            if (effectApplied) {
                // Update last use time
                _lastPotionUseTime = Time.time;

                // Remove item from inventory
                if (_playerInventory != null) {
                    _playerInventory.RemoveItem(inventorySlotIndex, 1);
                }

                return true;
            }

            return false;
        }

        #endregion

        #region Public Utility Methods (Client-Safe)

        /// <summary>
        /// Get remaining cooldown time for potions
        /// </summary>
        public float GetPotionCooldownRemaining() {
            float timeSinceLastUse = Time.time - _lastPotionUseTime;
            float remaining = _globalPotionCooldown - timeSinceLastUse;
            return Mathf.Max(0f, remaining);
        }

        /// <summary>
        /// Check if potions are on cooldown
        /// </summary>
        public bool IsPotionOnCooldown() {
            return GetPotionCooldownRemaining() > 0f;
        }

        #endregion
    }
}

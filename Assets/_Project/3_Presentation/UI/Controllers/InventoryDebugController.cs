using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Genesis.Simulation;
using Genesis.Items;
using Genesis.Data;
using Genesis.Core;

namespace Genesis.Presentation {
    public class InventoryDebugController : MonoBehaviour {
        [Header("UI")]
        [SerializeField] private UIDocument _uiDocument;

        [Header("References")]
        private PlayerInventory _playerInventory;

        private VisualElement _inventoryWindow;
        private VisualElement _inventoryList;
        private bool _isVisible = true;

        private void Awake() {
            if (_uiDocument == null) {
                _uiDocument = GetComponent<UIDocument>();
            }
        }

        private void OnEnable() {
            EventBus.Subscribe("OnInventoryChanged", OnInventoryChanged);
        }

        private void OnDisable() {
            EventBus.Unsubscribe("OnInventoryChanged", OnInventoryChanged);
        }

        private void Start() {
            // Wait longer for network initialization
            Invoke(nameof(InitializeUI), 1.0f);
        }

        private void InitializeUI() {
            var root = _uiDocument.rootVisualElement;
            _inventoryWindow = root.Q<VisualElement>("InventoryWindow");
            _inventoryList = root.Q<VisualElement>("InventoryList");

            // Find player's inventory - try multiple times if needed
            StartCoroutine(FindPlayerInventoryCoroutine());
        }

        private System.Collections.IEnumerator FindPlayerInventoryCoroutine() {
            int attempts = 0;
            const int maxAttempts = 10;

            while (_playerInventory == null && attempts < maxAttempts) {
                var allInventories = FindObjectsOfType<PlayerInventory>();
                foreach (var inventory in allInventories) {
                    if (inventory.IsOwner) {
                        _playerInventory = inventory;
                        Debug.Log($"[InventoryDebugController] Found owner PlayerInventory on attempt {attempts + 1}");

                        // Force immediate refresh
                        _playerInventory.ForceRefreshUI();

                        // Start periodic refresh for first few seconds to catch starter items
                        StartCoroutine(PeriodicRefreshCoroutine());
                        yield break;
                    }
                }

                attempts++;
                Debug.LogWarning($"[InventoryDebugController] PlayerInventory not found (attempt {attempts}/{maxAttempts})");
                yield return new WaitForSeconds(0.2f);
            }

            if (_playerInventory == null) {
                Debug.LogError("[InventoryDebugController] Failed to find owner PlayerInventory after all attempts!");
            }
        }

        private System.Collections.IEnumerator PeriodicRefreshCoroutine() {
            // Refresh every 0.5s for the first 3 seconds to catch starter items
            float elapsed = 0f;
            const float duration = 3f;
            const float interval = 0.5f;

            while (elapsed < duration) {
                if (_playerInventory != null) {
                    _playerInventory.ForceRefreshUI();
                }
                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }

            Debug.Log("[InventoryDebugController] Periodic refresh completed");
        }

        private void Update() {
            // Toggle with 'I' key
            if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame) {
                ToggleInventory();
            }
        }

        private void ToggleInventory() {
            _isVisible = !_isVisible;
            if (_inventoryWindow != null) {
                _inventoryWindow.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void OnInventoryChanged() {
            Debug.Log("[InventoryDebugController] OnInventoryChanged event received");
            RefreshInventory();
        }

        private void RefreshInventory() {
            if (_inventoryList == null) {
                Debug.LogWarning("[InventoryDebugController] RefreshInventory called but _inventoryList is null");
                return;
            }

            if (_playerInventory == null) {
                Debug.LogWarning("[InventoryDebugController] RefreshInventory called but _playerInventory is null");
                return;
            }

            _inventoryList.Clear();

            var slots = _playerInventory.InventorySlots;
            Debug.Log($"[InventoryDebugController] Refreshing inventory with {slots.Count} total slots");

            int nonEmptyCount = 0;
            for (int i = 0; i < slots.Count; i++) {
                var slot = slots[i];
                if (slot.IsEmpty) continue;

                nonEmptyCount++;
                var itemData = ItemDatabase.Instance.GetItem(slot.ItemID);
                if (itemData == null) {
                    Debug.LogWarning($"[InventoryDebugController] ItemID {slot.ItemID} not found in database!");
                    continue;
                }

                // CRITICAL: Capture index in local variable to avoid closure bug
                int slotIndex = i;

                // Create item row
                var itemRow = new VisualElement();
                itemRow.style.flexDirection = FlexDirection.Row;
                itemRow.style.alignItems = Align.Center;
                itemRow.style.marginBottom = 5;
                itemRow.style.paddingTop = 5;
                itemRow.style.paddingBottom = 5;
                itemRow.style.paddingLeft = 5;
                itemRow.style.paddingRight = 5;
                itemRow.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);

                // Item icon
                var iconContainer = new VisualElement();
                iconContainer.style.width = 40;
                iconContainer.style.height = 40;
                iconContainer.style.marginRight = 10;
                iconContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
                iconContainer.style.borderLeftWidth = 2;
                iconContainer.style.borderRightWidth = 2;
                iconContainer.style.borderTopWidth = 2;
                iconContainer.style.borderBottomWidth = 2;
                iconContainer.style.borderLeftColor = GetRarityColor(slot.Rarity);
                iconContainer.style.borderRightColor = GetRarityColor(slot.Rarity);
                iconContainer.style.borderTopColor = GetRarityColor(slot.Rarity);
                iconContainer.style.borderBottomColor = GetRarityColor(slot.Rarity);

                // Set icon sprite
                if (itemData.Icon != null) {
                    iconContainer.style.backgroundImage = new StyleBackground(itemData.Icon);
                    iconContainer.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                }

                // Right-click interaction
                iconContainer.RegisterCallback<MouseDownEvent>(evt => {
                    if (evt.button == 1) { // Right-click
                        if (itemData.Type == ItemType.Consumable) {
                            UseItem(slotIndex);
                        } else if (itemData.Type == ItemType.Equipment) {
                            EquipItem(slotIndex);
                        }
                        evt.StopPropagation();
                    }
                });

                itemRow.Add(iconContainer);

                // Item info container
                var infoContainer = new VisualElement();
                infoContainer.style.flexGrow = 1;
                infoContainer.style.flexDirection = FlexDirection.Column;

                // Item name
                string itemText = itemData.ItemName;
                if (itemData.IsProtected) {
                    itemText += " [PROTECTED]";
                }
                var nameLabel = new Label(itemText);
                nameLabel.style.color = GetRarityColor(slot.Rarity);
                nameLabel.style.fontSize = 12;
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                infoContainer.Add(nameLabel);

                // Quantity and action hint
                string actionHint = itemData.Type == ItemType.Consumable ? "Right-click to use" : "Right-click to equip";
                var quantityLabel = new Label($"x{slot.Quantity} • {actionHint}");
                quantityLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                quantityLabel.style.fontSize = 10;
                infoContainer.Add(quantityLabel);

                itemRow.Add(infoContainer);
                _inventoryList.Add(itemRow);
            }

            Debug.Log($"[InventoryDebugController] Displayed {nonEmptyCount} items in UI");

            // If empty, show message
            if (_inventoryList.childCount == 0) {
                var emptyLabel = new Label("Inventario vacío");
                emptyLabel.style.color = Color.gray;
                emptyLabel.style.fontSize = 12;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.marginTop = 20;
                _inventoryList.Add(emptyLabel);
            }
        }

        private void UseItem(int slotIndex) {
            if (_playerInventory != null) {
                _playerInventory.CmdUseConsumable(slotIndex);
                Debug.Log($"[InventoryDebugController] Usado consumible del slot {slotIndex}");
            }
        }

        private void EquipItem(int slotIndex) {
            var equipmentManager = _playerInventory?.GetComponent<EquipmentManager>();
            if (equipmentManager != null) {
                equipmentManager.CmdEquipFromInventory(slotIndex);
                Debug.Log($"[InventoryDebugController] Equipando item del slot {slotIndex}");
            }
        }

        private Color GetRarityColor(ItemRarity rarity) {
            switch (rarity) {
                case ItemRarity.Common: return Color.white;
                case ItemRarity.Uncommon: return new Color(0.12f, 1f, 0f); // Green
                case ItemRarity.Rare: return new Color(0f, 0.44f, 0.87f); // Blue
                case ItemRarity.Epic: return new Color(0.64f, 0.21f, 0.93f); // Purple
                default: return Color.white;
            }
        }
    }
}

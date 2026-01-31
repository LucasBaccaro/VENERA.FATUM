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
                itemRow.style.justifyContent = Justify.SpaceBetween;
                itemRow.style.marginBottom = 3;
                itemRow.style.paddingTop = 3;
                itemRow.style.paddingBottom = 3;
                itemRow.style.paddingLeft = 5;
                itemRow.style.paddingRight = 5;
                itemRow.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

                // Item name and quantity
                string itemText = $"{itemData.ItemName} x{slot.Quantity}";
                if (itemData.IsProtected) {
                    itemText += " [PROTECTED]";
                }
                var nameLabel = new Label(itemText);
                nameLabel.style.color = GetRarityColor(slot.Rarity);
                nameLabel.style.fontSize = 12;
                itemRow.Add(nameLabel);

                // Button container
                var buttonContainer = new VisualElement();
                buttonContainer.style.flexDirection = FlexDirection.Row;
                buttonContainer.style.alignItems = Align.Center;

                // Use button (for consumables)
                if (itemData.Type == ItemType.Consumable) {
                    var useButton = new Button(() => UseItem(slotIndex));
                    useButton.text = "Usar";
                    useButton.style.width = 50;
                    useButton.style.height = 20;
                    useButton.style.backgroundColor = new Color(0.3f, 0.6f, 0.3f);
                    useButton.style.color = Color.white;
                    useButton.style.marginRight = 3;
                    buttonContainer.Add(useButton);
                }

                // Equip button (for equipment)
                if (itemData.Type == ItemType.Equipment) {
                    var equipButton = new Button(() => EquipItem(slotIndex));
                    equipButton.text = "Equipar";
                    equipButton.style.width = 60;
                    equipButton.style.height = 20;
                    equipButton.style.backgroundColor = new Color(0.3f, 0.5f, 0.7f);
                    equipButton.style.color = Color.white;
                    buttonContainer.Add(equipButton);
                }

                itemRow.Add(buttonContainer);
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

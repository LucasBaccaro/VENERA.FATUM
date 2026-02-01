using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Genesis.Simulation;
using Genesis.Items;
using Genesis.Data;
using Genesis.Core;
using System.Collections.Generic;

namespace Genesis.Presentation.UI {
    public class InventoryController : MonoBehaviour {
        [Header("UI")]
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private bool _startVisible = false;

        [Header("References")]
        private PlayerInventory _playerInventory;
        private EquipmentManager _equipmentManager;

        private VisualElement _window;
        private VisualElement _grid;
        private Button _closeButton;
        
        // Slot data
        private List<VisualElement> _slots = new List<VisualElement>();
        private List<VisualElement> _icons = new List<VisualElement>();
        private List<Label> _quantities = new List<Label>();
        private List<VisualElement> _bgs = new List<VisualElement>();

        private bool _isVisible = false;

        private void Awake() {
            if (_uiDocument == null) {
                _uiDocument = GetComponent<UIDocument>();
            }
        }

        private void OnEnable() {
            EventBus.Subscribe("OnInventoryChanged", RefreshUI);
        }

        private void OnDisable() {
            EventBus.Unsubscribe("OnInventoryChanged", RefreshUI);
        }

        private void Start() {
            InitializeUI();
            
            // Wait for networking to find the local player
            Invoke(nameof(FindPlayer), 0.5f);
        }

        private void InitializeUI() {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;

            _window = root.Q<VisualElement>("InventoryWindow");
            _grid = root.Q<VisualElement>("InventoryGrid");
            _closeButton = root.Q<Button>("CloseButton");

            if (_closeButton != null) {
                _closeButton.clicked += () => ToggleVisibility(false);
            }

            // Bind slots (0-24)
            _slots.Clear();
            _icons.Clear();
            _quantities.Clear();
            _bgs.Clear();

            for (int i = 0; i < 25; i++) {
                var slot = root.Q<VisualElement>($"InventorySlot{i}");
                if (slot != null) {
                    _slots.Add(slot);
                    _icons.Add(slot.Q<VisualElement>($"Icon{i}"));
                    _quantities.Add(slot.Q<Label>($"Qty{i}"));
                    _bgs.Add(slot.Q<VisualElement>(className: "item-slot-bg"));

                    // Set up click interaction
                    int slotIndex = i;
                    slot.RegisterCallback<MouseDownEvent>(evt => OnSlotClicked(evt, slotIndex));
                }
            }

            // Set initial visibility
            ToggleVisibility(_startVisible);
            
            Debug.Log($"[InventoryController] Initialized with {_slots.Count} slots.");
        }

        private void Update() {
            // Toggle with 'I' key
            if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame) {
                ToggleVisibility(!_isVisible);
            }
            
            // Close with Escape if visible
            if (_isVisible && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) {
                ToggleVisibility(false);
            }
        }

        public void ToggleVisibility(bool visible) {
            _isVisible = visible;
            if (_window != null) {
                _window.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
                
                // If opening, refresh to ensure data is current
                if (_isVisible) RefreshUI();
            }
        }

        private void FindPlayer() {
            var allPlayers = FindObjectsOfType<PlayerInventory>();
            foreach (var inventory in allPlayers) {
                if (inventory.IsOwner) {
                    SetPlayerInventory(inventory);
                    return;
                }
            }
            
            // Retry if not found yet (common in network spawn)
            if (_playerInventory == null) Invoke(nameof(FindPlayer), 1.0f);
        }

        public void SetPlayerInventory(PlayerInventory inventory) {
            _playerInventory = inventory;
            _equipmentManager = inventory.GetComponent<EquipmentManager>();
            Debug.Log("[InventoryController] PlayerInventory connected.");
            RefreshUI();
        }

        private void RefreshUI() {
            if (_playerInventory == null || _slots.Count == 0) return;

            var slotsData = _playerInventory.InventorySlots;
            
            for (int i = 0; i < _slots.Count; i++) {
                if (i >= slotsData.Count) {
                    ClearSlot(i);
                    continue;
                }

                var slotData = slotsData[i];
                if (slotData.IsEmpty) {
                    ClearSlot(i);
                } else {
                    UpdateSlot(i, slotData);
                }
            }
        }

        private void UpdateSlot(int index, ItemSlot data) {
            var itemData = ItemDatabase.Instance.GetItem(data.ItemID);
            if (itemData == null) {
                ClearSlot(index);
                return;
            }

            // Update Icon
            if (_icons[index] != null) {
                _icons[index].style.backgroundImage = new StyleBackground(itemData.Icon);
                _icons[index].style.display = DisplayStyle.Flex;
            }

            // Update Quantity
            if (_quantities[index] != null) {
                _quantities[index].text = data.Quantity > 1 ? data.Quantity.ToString() : "";
            }

            // Update Bg Rarity (using tint as a simple way to show rarity)
            if (_bgs[index] != null) {
                _bgs[index].ClearClassList();
                _bgs[index].AddToClassList("item-slot-bg");
                _bgs[index].AddToClassList(GetRarityClass(data.Rarity));
            }
        }

        private void ClearSlot(int index) {
            if (_icons[index] != null) _icons[index].style.display = DisplayStyle.None;
            if (_quantities[index] != null) _quantities[index].text = "";
            if (_bgs[index] != null) {
                _bgs[index].ClearClassList();
                _bgs[index].AddToClassList("item-slot-bg");
            }
        }

        private void OnSlotClicked(MouseDownEvent evt, int index) {
            if (_playerInventory == null) return;
            
            var slotsData = _playerInventory.InventorySlots;
            if (index >= slotsData.Count || slotsData[index].IsEmpty) return;

            // Right click to use/equip
            if (evt.button == 1) {
                var slotData = slotsData[index];
                var itemData = ItemDatabase.Instance.GetItem(slotData.ItemID);
                
                if (itemData != null) {
                    if (itemData.Type == ItemType.Consumable) {
                        _playerInventory.CmdUseConsumable(index);
                        Debug.Log($"[InventoryController] Consumed item at slot {index}");
                    } else if (itemData.Type == ItemType.Equipment && _equipmentManager != null) {
                        _equipmentManager.CmdEquipFromInventory(index);
                        Debug.Log($"[InventoryController] Equipped item from slot {index}");
                    }
                }
                evt.StopPropagation();
            }
        }

        private string GetRarityClass(ItemRarity rarity) {
            switch (rarity) {
                case ItemRarity.Uncommon: return "rarity-uncommon";
                case ItemRarity.Rare: return "rarity-rare";
                case ItemRarity.Epic: return "rarity-epic";
                default: return "rarity-common";
            }
        }
    }
}

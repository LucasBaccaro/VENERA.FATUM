using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Genesis.Simulation;
using Genesis.Items;
using Genesis.Data;
using Genesis.Core;
using System.Collections.Generic;

namespace Genesis.Presentation {
    public class CharacterPanelController : MonoBehaviour {
        [Header("UI")]
        [SerializeField] private UIDocument _uiDocument;

        [Header("References")]
        private EquipmentManager _equipmentManager;
        private PlayerStats _playerStats;

        private VisualElement _characterPanelWindow;
        private Label _maxHealthLabel;
        private Label _maxManaLabel;
        private Label _spellPowerLabel;
        private VisualElement _itemStatsList;
        
        private Dictionary<EquipmentSlot, VisualElement> _slotElements = new Dictionary<EquipmentSlot, VisualElement>();
        private bool _isVisible = false;

        private void Awake() {
            if (_uiDocument == null) {
                _uiDocument = GetComponent<UIDocument>();
            }
        }

        private void OnEnable() {
            EventBus.Subscribe("OnEquipmentChanged", OnEquipmentChanged);
        }

        private void OnDisable() {
            EventBus.Unsubscribe("OnEquipmentChanged", OnEquipmentChanged);
        }

        private void Start() {
            // Wait for network initialization
            Invoke(nameof(InitializeUI), 1.0f);
        }

        private void InitializeUI() {
            var root = _uiDocument.rootVisualElement;
            _characterPanelWindow = root.Q<VisualElement>("CharacterPanelWindow");
            _maxHealthLabel = root.Q<Label>("MaxHealthLabel");
            _maxManaLabel = root.Q<Label>("MaxManaLabel");
            _spellPowerLabel = root.Q<Label>("SpellPowerLabel");
            _itemStatsList = root.Q<VisualElement>("ItemStatsList");

            // Register slots
            _slotElements[EquipmentSlot.Head] = root.Q<VisualElement>("HeadSlot");
            _slotElements[EquipmentSlot.Shoulders] = root.Q<VisualElement>("ShouldersSlot");
            _slotElements[EquipmentSlot.Chest] = root.Q<VisualElement>("ChestSlot");
            _slotElements[EquipmentSlot.Hands] = root.Q<VisualElement>("HandsSlot");
            _slotElements[EquipmentSlot.Pants] = root.Q<VisualElement>("LegsSlot"); // Renamed in UI for clarity
            _slotElements[EquipmentSlot.Belt] = root.Q<VisualElement>("BeltSlot");
            _slotElements[EquipmentSlot.Feet] = root.Q<VisualElement>("BootsSlot"); // Renamed in UI for clarity
            _slotElements[EquipmentSlot.Weapon] = root.Q<VisualElement>("MainHandSlot"); // Renamed in UI for clarity
            _slotElements[EquipmentSlot.OffHand] = root.Q<VisualElement>("OffHandSlot");

            // Hide initially
            _characterPanelWindow.style.display = DisplayStyle.None;

            // Find player's equipment manager
            StartCoroutine(FindPlayerComponentsCoroutine());
        }

        private System.Collections.IEnumerator FindPlayerComponentsCoroutine() {
            int attempts = 0;
            const int maxAttempts = 10;

            while (_equipmentManager == null && attempts < maxAttempts) {
                var allEquipmentManagers = Object.FindObjectsByType<EquipmentManager>(FindObjectsSortMode.None);
                foreach (var manager in allEquipmentManagers) {
                    if (manager.IsOwner) {
                        _equipmentManager = manager;
                        _playerStats = manager.GetComponent<PlayerStats>();
                        Debug.Log($"[CharacterPanelController] Found owner EquipmentManager on attempt {attempts + 1}");
                        RefreshCharacterPanel();
                        yield break;
                    }
                }

                attempts++;
                yield return new WaitForSeconds(0.2f);
            }
        }

        private void Update() {
            // Toggle with 'C' key
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) {
                ToggleCharacterPanel();
            }
        }

        private void ToggleCharacterPanel() {
            _isVisible = !_isVisible;
            if (_characterPanelWindow != null) {
                _characterPanelWindow.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
                if (_isVisible) {
                    RefreshCharacterPanel();
                }
            }
        }

        private void OnEquipmentChanged() {
            RefreshCharacterPanel();
        }

        private void RefreshCharacterPanel() {
            if (_equipmentManager == null || _playerStats == null) {
                return;
            }

            // Update stats
            if (_maxHealthLabel != null) _maxHealthLabel.text = $"{_playerStats.MaxHealth:F0}";
            if (_maxManaLabel != null) _maxManaLabel.text = $"{_playerStats.MaxMana:F0}";
            if (_spellPowerLabel != null) _spellPowerLabel.text = $"+{(_equipmentManager.SpellPowerBonus * 100f):F0}%";

            // Update Equipment Slots
            foreach (var kvp in _slotElements) {
                UpdateSlotUI(kvp.Key, kvp.Value);
            }

            // Clear item bonuses by default
            if (_itemStatsList != null) _itemStatsList.Clear();
        }

        private void ShowItemBonuses(EquipmentSlot slot) {
            if (_itemStatsList == null) return;
            _itemStatsList.Clear();

            ItemSlot itemSlot = _equipmentManager.GetEquipmentSlot(slot);
            if (itemSlot.IsEmpty) return;

            var itemData = ItemDatabase.Instance.GetItem(itemSlot.ItemID) as EquipmentItemData;
            if (itemData == null) return;

            var stats = itemData.GetStatsForRarity(itemSlot.Rarity);
            if (stats == null || stats.Count == 0) return;

            // Create entry for this item
            VisualElement entry = new VisualElement();
            entry.AddToClassList("item-stat-entry");
            entry.style.marginBottom = 10;

            // Header with Icon and Name
            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 2;

            if (itemData.Icon != null) {
                VisualElement smallIcon = new VisualElement();
                smallIcon.style.width = 20;
                smallIcon.style.height = 20;
                smallIcon.style.backgroundImage = new StyleBackground(itemData.Icon);
                smallIcon.style.marginRight = 8;
                header.Add(smallIcon);
            }

            Label itemName = new Label(itemData.ItemName);
            itemName.AddToClassList("item-stat-name");
            header.Add(itemName);
            entry.Add(header);

            foreach (var stat in stats) {
                Label statLabel = new Label(stat.ToString());
                statLabel.AddToClassList("item-stat-values");
                statLabel.style.marginLeft = 28; // Align with name after icon
                entry.Add(statLabel);
            }

            _itemStatsList.Add(entry);
        }

        private void HideItemBonuses() {
            if (_itemStatsList != null) _itemStatsList.Clear();
        }

        private void UpdateSlotUI(EquipmentSlot slot, VisualElement slotElement) {
            if (slotElement == null) return;

            ItemSlot itemSlot = _equipmentManager.GetEquipmentSlot(slot);
            var iconElement = slotElement.Q<VisualElement>(className: "char-item-icon");
            var bgElement = slotElement.Q<VisualElement>(className: "char-item-slot-bg");

            if (itemSlot.IsEmpty) {
                if (iconElement != null) iconElement.style.backgroundImage = null;
                if (bgElement != null) {
                    bgElement.style.display = DisplayStyle.None; // Hide when empty
                    bgElement.ClearClassList();
                    bgElement.AddToClassList("char-item-slot-bg");
                }
                slotElement.RemoveFromClassList("slot-highlight");
            } else {
                var itemData = ItemDatabase.Instance.GetItem(itemSlot.ItemID);
                if (itemData != null && itemData.Icon != null && iconElement != null) {
                    iconElement.style.backgroundImage = new StyleBackground(itemData.Icon);
                }

                // Show and manage rarity classes on BG element
                if (bgElement != null) {
                    bgElement.style.display = DisplayStyle.Flex; // Show when equipped
                    bgElement.ClearClassList();
                    bgElement.AddToClassList("char-item-slot-bg");
                    bgElement.AddToClassList(GetRarityClassName(itemSlot.Rarity));
                }
                
                slotElement.AddToClassList("slot-highlight");

                // Register actions
                slotElement.UnregisterCallback<MouseDownEvent>(OnSlotClicked);
                slotElement.RegisterCallback<MouseDownEvent>(e => OnSlotClicked(e, slot));

                // Register hover for stats
                slotElement.UnregisterCallback<MouseEnterEvent>(OnMouseEnterSlot);
                slotElement.RegisterCallback<MouseEnterEvent>(e => ShowItemBonuses(slot));

                slotElement.UnregisterCallback<MouseLeaveEvent>(OnMouseLeaveSlot);
                slotElement.RegisterCallback<MouseLeaveEvent>(e => HideItemBonuses());
            }
        }

        private void OnMouseEnterSlot(MouseEnterEvent e) {
            // Placeholder if we need event-specific logic, but using lambda for simplicity above
        }

        private void OnMouseLeaveSlot(MouseLeaveEvent e) {
            // Placeholder
        }

        private void OnSlotClicked(MouseDownEvent evt, EquipmentSlot slot) {
            if (evt.button == 1) { // Right-click
                UnequipSlot(slot);
                evt.StopPropagation();
            }
        }

        // Overload for Unregister
        private void OnSlotClicked(MouseDownEvent evt) { }

        private void UnequipSlot(EquipmentSlot slot) {
            if (_equipmentManager != null) {
                _equipmentManager.CmdUnequipToInventory(slot);
            }
        }

        private string GetRarityClassName(ItemRarity rarity) {
            switch (rarity) {
                case ItemRarity.Common: return "rarity-common";
                case ItemRarity.Uncommon: return "rarity-uncommon";
                case ItemRarity.Rare: return "rarity-rare";
                case ItemRarity.Epic: return "rarity-epic";
                default: return "rarity-common";
            }
        }
    }
}

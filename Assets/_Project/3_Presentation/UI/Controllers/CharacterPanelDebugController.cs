using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Genesis.Simulation;
using Genesis.Items;
using Genesis.Data;
using Genesis.Core;

namespace Genesis.Presentation {
    public class CharacterPanelDebugController : MonoBehaviour {
        [Header("UI")]
        [SerializeField] private UIDocument _uiDocument;

        [Header("References")]
        private EquipmentManager _equipmentManager;
        private PlayerStats _playerStats;

        private VisualElement _characterPanelWindow;
        private Label _maxHealthLabel;
        private Label _maxManaLabel;
        private Label _spellPowerLabel;
        private VisualElement _equipmentList;
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
            // Wait longer for network initialization
            Invoke(nameof(InitializeUI), 1.0f);
        }

        private void InitializeUI() {
            var root = _uiDocument.rootVisualElement;
            _characterPanelWindow = root.Q<VisualElement>("CharacterPanelWindow");
            _maxHealthLabel = root.Q<Label>("MaxHealthLabel");
            _maxManaLabel = root.Q<Label>("MaxManaLabel");
            _spellPowerLabel = root.Q<Label>("SpellPowerLabel");
            _equipmentList = root.Q<VisualElement>("EquipmentList");

            // Hide initially
            _characterPanelWindow.style.display = DisplayStyle.None;

            // Find player's equipment manager
            StartCoroutine(FindPlayerComponentsCoroutine());
        }

        private System.Collections.IEnumerator FindPlayerComponentsCoroutine() {
            int attempts = 0;
            const int maxAttempts = 10;

            while (_equipmentManager == null && attempts < maxAttempts) {
                var allEquipmentManagers = FindObjectsOfType<EquipmentManager>();
                foreach (var manager in allEquipmentManagers) {
                    if (manager.IsOwner) {
                        _equipmentManager = manager;
                        _playerStats = manager.GetComponent<PlayerStats>();
                        Debug.Log($"[CharacterPanelDebugController] Found owner EquipmentManager on attempt {attempts + 1}");
                        RefreshCharacterPanel();
                        yield break;
                    }
                }

                attempts++;
                Debug.LogWarning($"[CharacterPanelDebugController] EquipmentManager not found (attempt {attempts}/{maxAttempts})");
                yield return new WaitForSeconds(0.2f);
            }

            if (_equipmentManager == null) {
                Debug.LogError("[CharacterPanelDebugController] Failed to find owner EquipmentManager after all attempts!");
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
            Debug.Log("[CharacterPanelDebugController] OnEquipmentChanged event received");
            RefreshCharacterPanel();
        }

        private void RefreshCharacterPanel() {
            if (_equipmentList == null || _equipmentManager == null || _playerStats == null) {
                return;
            }

            // Update stats
            _maxHealthLabel.text = $"Max Health: {_playerStats.MaxHealth:F0}";
            _maxManaLabel.text = $"Max Mana: {_playerStats.MaxMana:F0}";
            _spellPowerLabel.text = $"Spell Power: +{(_equipmentManager.SpellPowerBonus * 100f):F0}%";

            // Clear equipment list
            _equipmentList.Clear();

            // Create equipment slots (in order)
            CreateEquipmentSlot(EquipmentSlot.Head, "Head");
            CreateEquipmentSlot(EquipmentSlot.Chest, "Chest");
            CreateEquipmentSlot(EquipmentSlot.Legs, "Legs");
            CreateEquipmentSlot(EquipmentSlot.Feet, "Feet");
            CreateEquipmentSlot(EquipmentSlot.Hands, "Hands");
            CreateEquipmentSlot(EquipmentSlot.Belt, "Belt");

            Debug.Log("[CharacterPanelDebugController] Character panel refreshed");
        }

        private void CreateEquipmentSlot(EquipmentSlot slot, string slotName) {
            ItemSlot itemSlot = _equipmentManager.GetEquipmentSlot(slot);

            // Create slot container
            var slotContainer = new VisualElement();
            slotContainer.style.flexDirection = FlexDirection.Row;
            slotContainer.style.alignItems = Align.Center;
            slotContainer.style.marginBottom = 5;
            slotContainer.style.paddingTop = 5;
            slotContainer.style.paddingBottom = 5;
            slotContainer.style.paddingLeft = 8;
            slotContainer.style.paddingRight = 8;
            slotContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);

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

            if (itemSlot.IsEmpty) {
                // Empty slot - gray border
                iconContainer.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
                iconContainer.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
                iconContainer.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
                iconContainer.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            } else {
                // Equipped item - rarity border
                iconContainer.style.borderLeftColor = GetRarityColor(itemSlot.Rarity);
                iconContainer.style.borderRightColor = GetRarityColor(itemSlot.Rarity);
                iconContainer.style.borderTopColor = GetRarityColor(itemSlot.Rarity);
                iconContainer.style.borderBottomColor = GetRarityColor(itemSlot.Rarity);

                var itemData = ItemDatabase.Instance.GetItem(itemSlot.ItemID);
                if (itemData != null && itemData.Icon != null) {
                    iconContainer.style.backgroundImage = new StyleBackground(itemData.Icon);
                    iconContainer.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                }

                // Right-click to unequip
                iconContainer.RegisterCallback<MouseDownEvent>(evt => {
                    if (evt.button == 1) { // Right-click
                        UnequipSlot(slot);
                        evt.StopPropagation();
                    }
                });
            }

            slotContainer.Add(iconContainer);

            // Slot info container
            var infoContainer = new VisualElement();
            infoContainer.style.flexGrow = 1;
            infoContainer.style.flexDirection = FlexDirection.Column;

            // Slot label
            var slotLabel = new Label(slotName);
            slotLabel.style.fontSize = 11;
            slotLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            slotLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            infoContainer.Add(slotLabel);

            // Item info
            if (itemSlot.IsEmpty) {
                var emptyLabel = new Label("(Empty)");
                emptyLabel.style.fontSize = 10;
                emptyLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                infoContainer.Add(emptyLabel);
            } else {
                var itemData = ItemDatabase.Instance.GetItem(itemSlot.ItemID);
                if (itemData != null) {
                    string itemName = itemData.ItemName;
                    if (itemData.IsProtected) {
                        itemName += " [PROTECTED]";
                    }
                    var itemNameLabel = new Label(itemName);
                    itemNameLabel.style.fontSize = 12;
                    itemNameLabel.style.color = GetRarityColor(itemSlot.Rarity);
                    infoContainer.Add(itemNameLabel);

                    // Show stats
                    var equipmentData = itemData as EquipmentItemData;
                    if (equipmentData != null) {
                        var stats = equipmentData.GetStatsForRarity(itemSlot.Rarity);
                        if (stats != null && stats.Count > 0) {
                            var statsText = "";
                            foreach (var stat in stats) {
                                if (stat.Type == StatType.SpellPower) {
                                    statsText += $"+{stat.Value * 100f:F0}% {stat.Type}, ";
                                } else {
                                    statsText += $"+{stat.Value:F0} {stat.Type}, ";
                                }
                            }
                            statsText = statsText.TrimEnd(',', ' ');

                            var statsLabel = new Label(statsText);
                            statsLabel.style.fontSize = 9;
                            statsLabel.style.color = new Color(0.6f, 0.8f, 0.6f);
                            infoContainer.Add(statsLabel);
                        }
                    }

                    // Action hint
                    var hintLabel = new Label("Right-click to unequip");
                    hintLabel.style.fontSize = 9;
                    hintLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                    infoContainer.Add(hintLabel);
                }
            }

            slotContainer.Add(infoContainer);
            _equipmentList.Add(slotContainer);
        }

        private void UnequipSlot(EquipmentSlot slot) {
            if (_equipmentManager != null) {
                _equipmentManager.CmdUnequipToInventory(slot);
                Debug.Log($"[CharacterPanelDebugController] Unequipping {slot} slot");
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

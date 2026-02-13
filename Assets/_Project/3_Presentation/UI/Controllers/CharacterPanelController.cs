using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Genesis.Simulation;
using Genesis.Items;
using Genesis.Data;
using Genesis.Core;
using Genesis.Presentation.UI;
using System.Collections.Generic;
using System.Collections;

namespace Genesis.Presentation {
    public class CharacterPanelController : MonoBehaviour {
        [Header("UI")]
        [SerializeField] private UIDocument _uiDocument;

        [Header("References")]
        private EquipmentManager _equipmentManager;
        private PlayerStats _playerStats;
        private PlayerAttributes _playerAttributes;

        private VisualElement _characterPanelWindow;
        private Label _maxHealthLabel;
        private Label _maxManaLabel;
        private VisualElement _itemStatsList;

        // Attribute labels
        private Label _levelLabel;
        private Label _unspentPointsLabel;
        private Label _strLabel, _agiLabel, _intLabel, _wisLabel, _conLabel;
        private Button _strBtn, _agiBtn, _intBtn, _wisBtn, _conBtn;
        // Derived stat labels
        private Label _physDmgLabel, _magDmgLabel, _critLabel, _spellCritLabel;
        private Label _healPowerLabel, _evasionLabel, _overpowerLabel;
        // Sub-stat labels
        private Label _hasteLabel, _lifeStealLabel, _penetrationLabel, _blockLabel;
        private Label _armorLabel, _magicResLabel;
        private Label _moveSpeedLabel;
        // World stat labels
        private Label _lootLuckLabel, _lockpickingLabel, _perceptionLabel;

        private Dictionary<EquipmentSlot, VisualElement> _slotElements = new Dictionary<EquipmentSlot, VisualElement>();
        private bool _isVisible = false;
        private bool _isPlayerDead = false;

        private void Awake() {
            if (_uiDocument == null) {
                _uiDocument = GetComponent<UIDocument>();
            }
        }

        private void OnEnable() {
            EventBus.Subscribe("OnEquipmentChanged", OnEquipmentChanged);
            EventBus.Subscribe("OnAttributesChanged", OnStatsChanged);
            EventBus.Subscribe("OnDerivedStatsChanged", OnStatsChanged);
            EventBus.Subscribe<int>("OnUnspentPointsChanged", OnPointsChanged);
            EventBus.Subscribe<int>("OnLevelChanged", OnLevelChanged);
            EventBus.Subscribe("OnLocalPlayerDied", OnPlayerDied);
            EventBus.Subscribe("OnLocalPlayerRespawned", OnPlayerRespawned);
        }

        private void OnDisable() {
            EventBus.Unsubscribe("OnEquipmentChanged", OnEquipmentChanged);
            EventBus.Unsubscribe("OnAttributesChanged", OnStatsChanged);
            EventBus.Unsubscribe("OnDerivedStatsChanged", OnStatsChanged);
            EventBus.Unsubscribe<int>("OnUnspentPointsChanged", OnPointsChanged);
            EventBus.Unsubscribe<int>("OnLevelChanged", OnLevelChanged);
            EventBus.Unsubscribe("OnLocalPlayerDied", OnPlayerDied);
            EventBus.Unsubscribe("OnLocalPlayerRespawned", OnPlayerRespawned);
        }

        private void OnPlayerDied() {
            _isPlayerDead = true;
            if (_isVisible) ToggleCharacterPanel();
        }

        private void OnPlayerRespawned() {
            _isPlayerDead = false;
        }

        private bool _uiInitialized = false;

        private void Start() {
            InitializeUI();
        }

        private void InitializeUI() {
            var root = _uiDocument.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            _characterPanelWindow = root.Q<VisualElement>("CharacterPanelWindow");
            _maxHealthLabel = root.Q<Label>("MaxHealthLabel");
            _maxManaLabel = root.Q<Label>("MaxManaLabel");
            _itemStatsList = root.Q<VisualElement>("ItemStatsList");

            // Register slots
            _slotElements[EquipmentSlot.Head] = root.Q<VisualElement>("HeadSlot");
            _slotElements[EquipmentSlot.Shoulders] = root.Q<VisualElement>("ShouldersSlot");
            _slotElements[EquipmentSlot.Chest] = root.Q<VisualElement>("ChestSlot");
            _slotElements[EquipmentSlot.Hands] = root.Q<VisualElement>("HandsSlot");
            _slotElements[EquipmentSlot.Pants] = root.Q<VisualElement>("LegsSlot");
            _slotElements[EquipmentSlot.Belt] = root.Q<VisualElement>("BeltSlot");
            _slotElements[EquipmentSlot.Feet] = root.Q<VisualElement>("BootsSlot");
            _slotElements[EquipmentSlot.Weapon] = root.Q<VisualElement>("MainHandSlot");
            _slotElements[EquipmentSlot.OffHand] = root.Q<VisualElement>("OffHandSlot");
            _slotElements[EquipmentSlot.Ring1] = root.Q<VisualElement>("Ring1Slot");
            _slotElements[EquipmentSlot.Ring2] = root.Q<VisualElement>("Ring2Slot");

            // Attribute labels
            _levelLabel = root.Q<Label>("LevelLabel");
            _unspentPointsLabel = root.Q<Label>("UnspentPointsLabel");
            _strLabel = root.Q<Label>("StrLabel");
            _agiLabel = root.Q<Label>("AgiLabel");
            _intLabel = root.Q<Label>("IntLabel");
            _wisLabel = root.Q<Label>("WisLabel");
            _conLabel = root.Q<Label>("ConLabel");

            // Attribute + buttons
            _strBtn = root.Q<Button>("StrPlusBtn");
            _agiBtn = root.Q<Button>("AgiPlusBtn");
            _intBtn = root.Q<Button>("IntPlusBtn");
            _wisBtn = root.Q<Button>("WisPlusBtn");
            _conBtn = root.Q<Button>("ConPlusBtn");

            _strBtn?.RegisterCallback<ClickEvent>(e => AllocatePoint(0));
            _agiBtn?.RegisterCallback<ClickEvent>(e => AllocatePoint(1));
            _intBtn?.RegisterCallback<ClickEvent>(e => AllocatePoint(2));
            _wisBtn?.RegisterCallback<ClickEvent>(e => AllocatePoint(3));
            _conBtn?.RegisterCallback<ClickEvent>(e => AllocatePoint(4));

            // Derived stats
            _physDmgLabel = root.Q<Label>("PhysDmgLabel");
            _magDmgLabel = root.Q<Label>("MagDmgLabel");
            _critLabel = root.Q<Label>("CritLabel");
            _spellCritLabel = root.Q<Label>("SpellCritLabel");
            _healPowerLabel = root.Q<Label>("HealPowerLabel");
            _evasionLabel = root.Q<Label>("EvasionLabel");
            _overpowerLabel = root.Q<Label>("OverpowerLabel");
            // Sub-stats
            _hasteLabel = root.Q<Label>("HasteLabel");
            _lifeStealLabel = root.Q<Label>("LifeStealLabel");
            _penetrationLabel = root.Q<Label>("PenetrationLabel");
            _blockLabel = root.Q<Label>("BlockLabel");
            _armorLabel = root.Q<Label>("ArmorLabel");
            _magicResLabel = root.Q<Label>("MagicResLabel");
            _moveSpeedLabel = root.Q<Label>("MoveSpeedLabel");
            // World stats
            _lootLuckLabel = root.Q<Label>("LootLuckLabel");
            _lockpickingLabel = root.Q<Label>("LockpickingLabel");
            _perceptionLabel = root.Q<Label>("PerceptionLabel");

            // Hide initially
            _characterPanelWindow.style.display = DisplayStyle.None;
            _uiInitialized = true;
        }

        private bool TryFindPlayerComponents() {
            if (_equipmentManager != null && _playerStats != null && _playerAttributes != null)
                return true;

            var allEquipmentManagers = Object.FindObjectsByType<EquipmentManager>(FindObjectsSortMode.None);
            foreach (var manager in allEquipmentManagers) {
                if (manager.IsOwner) {
                    _equipmentManager = manager;
                    _playerStats = manager.GetComponent<PlayerStats>();
                    _playerAttributes = manager.GetComponent<PlayerAttributes>();
                    Debug.Log("[CharacterPanelController] Found owner EquipmentManager");
                    return true;
                }
            }
            return false;
        }

        private void Update() {
            if (_isPlayerDead) return;
            if (LoginController.IsActive) return;

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
                    TryFindPlayerComponents();
                    RefreshCharacterPanel();
                }
            }
        }

        private void AllocatePoint(int attrIndex) {
            if (_playerAttributes == null || _playerAttributes.UnspentPoints <= 0) return;
            _playerAttributes.CmdAllocatePoint(attrIndex);
        }

        private void OnEquipmentChanged() {
            RefreshCharacterPanel();
        }

        private void OnStatsChanged() {
            RefreshCharacterPanel();
        }

        private void OnPointsChanged(int newPoints) {
            RefreshCharacterPanel();
        }

        private void OnLevelChanged(int newLevel) {
            RefreshCharacterPanel();
        }

        private void RefreshCharacterPanel() {
            if (!_uiInitialized) return;
            if (_equipmentManager == null || _playerStats == null || _playerAttributes == null) {
                if (!TryFindPlayerComponents()) return;
            }

            // Update base stats
            if (_maxHealthLabel != null) _maxHealthLabel.text = $"{_playerStats.MaxHealth:F0}";
            if (_maxManaLabel != null) _maxManaLabel.text = $"{_playerStats.MaxMana:F0}";

            // Update attributes if available
            if (_playerAttributes != null) {
                if (_levelLabel != null) _levelLabel.text = $"{_playerAttributes.Level}";
                if (_strLabel != null) _strLabel.text = $"{_playerAttributes.Strength}";
                if (_agiLabel != null) _agiLabel.text = $"{_playerAttributes.Agility}";
                if (_intLabel != null) _intLabel.text = $"{_playerAttributes.Intelligence}";
                if (_wisLabel != null) _wisLabel.text = $"{_playerAttributes.Wisdom}";
                if (_conLabel != null) _conLabel.text = $"{_playerAttributes.Constitution}";

                // Unspent points
                int pts = _playerAttributes.UnspentPoints;
                if (_unspentPointsLabel != null) {
                    _unspentPointsLabel.text = pts > 0 ? $"{pts} pts" : "";
                }

                // Enable/disable + buttons
                bool hasPoints = pts > 0;
                _strBtn?.SetEnabled(hasPoints);
                _agiBtn?.SetEnabled(hasPoints);
                _intBtn?.SetEnabled(hasPoints);
                _wisBtn?.SetEnabled(hasPoints);
                _conBtn?.SetEnabled(hasPoints);

                // Derived stats
                if (_physDmgLabel != null) _physDmgLabel.text = $"+{_playerAttributes.PhysicalDamageBonus * 100f:F1}%";
                if (_magDmgLabel != null) _magDmgLabel.text = $"+{_playerAttributes.MagicDamageBonus * 100f:F1}%";
                if (_critLabel != null) _critLabel.text = $"{_playerAttributes.CritChance * 100f:F1}%";
                if (_spellCritLabel != null) _spellCritLabel.text = $"{_playerAttributes.SpellCritChance * 100f:F1}%";
                if (_healPowerLabel != null) _healPowerLabel.text = $"+{_playerAttributes.HealingPowerBonus * 100f:F1}%";
                if (_evasionLabel != null) _evasionLabel.text = $"{_playerAttributes.EvasionChance * 100f:F1}%";
                if (_overpowerLabel != null) _overpowerLabel.text = $"{_playerAttributes.OverpowerChance * 100f:F1}%";

                // Sub-stats
                if (_hasteLabel != null) _hasteLabel.text = $"+{_playerAttributes.Haste * 100f:F1}%";
                if (_lifeStealLabel != null) _lifeStealLabel.text = $"{_playerAttributes.LifeSteal * 100f:F1}%";
                if (_penetrationLabel != null) _penetrationLabel.text = $"{_playerAttributes.Penetration * 100f:F1}%";
                if (_blockLabel != null) _blockLabel.text = $"{_playerAttributes.BlockValue:F0}";
                if (_armorLabel != null) _armorLabel.text = $"{_playerAttributes.Armor:F0}";
                if (_magicResLabel != null) _magicResLabel.text = $"{_playerAttributes.MagicResistance:F0}";
                if (_moveSpeedLabel != null) _moveSpeedLabel.text = $"+{_playerAttributes.MoveSpeed * 100f:F1}%";

                // World stats
                if (_lootLuckLabel != null) _lootLuckLabel.text = $"+{_playerAttributes.LootLuck * 100f:F1}%";
                if (_lockpickingLabel != null) _lockpickingLabel.text = $"{_playerAttributes.Lockpicking:F0}";
                if (_perceptionLabel != null) _perceptionLabel.text = $"{_playerAttributes.Perception:F0}";
            }

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
                    bgElement.style.display = DisplayStyle.None;
                    bgElement.ClearClassList();
                    bgElement.AddToClassList("char-item-slot-bg");
                }
                slotElement.RemoveFromClassList("slot-highlight");
            } else {
                var itemData = ItemDatabase.Instance.GetItem(itemSlot.ItemID);
                if (itemData != null && itemData.Icon != null && iconElement != null) {
                    iconElement.style.backgroundImage = new StyleBackground(itemData.Icon);
                }

                if (bgElement != null) {
                    bgElement.style.display = DisplayStyle.Flex;
                    bgElement.ClearClassList();
                    bgElement.AddToClassList("char-item-slot-bg");
                    bgElement.AddToClassList(GetRarityClassName(itemSlot.Rarity));
                }

                slotElement.AddToClassList("slot-highlight");

                slotElement.UnregisterCallback<MouseDownEvent>(OnSlotClicked);
                slotElement.RegisterCallback<MouseDownEvent>(e => OnSlotClicked(e, slot));

                slotElement.UnregisterCallback<MouseEnterEvent>(OnMouseEnterSlot);
                slotElement.RegisterCallback<MouseEnterEvent>(e => ShowItemBonuses(slot));

                slotElement.UnregisterCallback<MouseLeaveEvent>(OnMouseLeaveSlot);
                slotElement.RegisterCallback<MouseLeaveEvent>(e => HideItemBonuses());
            }
        }

        private void OnMouseEnterSlot(MouseEnterEvent e) { }
        private void OnMouseLeaveSlot(MouseLeaveEvent e) { }

        private void OnSlotClicked(MouseDownEvent evt, EquipmentSlot slot) {
            if (evt.button == 1) {
                UnequipSlot(slot);
                evt.StopPropagation();
            }
        }

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

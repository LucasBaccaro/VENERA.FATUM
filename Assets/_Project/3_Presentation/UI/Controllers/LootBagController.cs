using UnityEngine;
using UnityEngine.UIElements;
using Genesis.Simulation;
using Genesis.Items;
using Genesis.Data;
using Genesis.Core;
using Genesis.Presentation.Audio;
using System.Collections.Generic;

namespace Genesis.Presentation.UI {
    public class LootBagController : MonoBehaviour {
        [Header("UI")]
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _window;
        private VisualElement _grid;
        private Label _title;
        private Button _takeAllButton;
        private Button _closeButton;

        private ILootSource _currentLootSource;
        private Vector3 _playerPositionOnOpen;
        private const float _moveThreshold = 0.1f;

        // Slot data
        private List<VisualElement> _slots = new List<VisualElement>();
        private List<VisualElement> _icons = new List<VisualElement>();
        private List<Label> _quantities = new List<Label>();
        private List<VisualElement> _bgs = new List<VisualElement>();

        private void Awake() {
            if (_uiDocument == null) {
                _uiDocument = GetComponent<UIDocument>();
            }
        }

        private void OnEnable() {
            // Updated event name to be generic
            EventBus.Subscribe<ILootSource>("OnLootOpened", OnLootOpened);
        }

        private void OnDisable() {
            EventBus.Unsubscribe<ILootSource>("OnLootOpened", OnLootOpened);
        }

        private void Start() {
            InitializeUI();
        }

        private void InitializeUI() {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;

            root.pickingMode = PickingMode.Ignore;
            _window = root.Q<VisualElement>("LootBagWindow");
            _grid = root.Q<VisualElement>("LootGrid");
            _title = root.Q<Label>("LootTitle");
            _takeAllButton = root.Q<Button>("TakeAllButton");
            _closeButton = root.Q<Button>("CloseButton");

            if (_takeAllButton != null) _takeAllButton.clicked += OnTakeAllClicked;
            if (_closeButton != null) _closeButton.clicked += CloseWindow;

            // Bind slots (0-11)
            _slots.Clear();
            _icons.Clear();
            _quantities.Clear();
            _bgs.Clear();

            for (int i = 0; i < 12; i++) {
                var slot = root.Q<VisualElement>($"LootSlot{i}");
                if (slot != null) {
                    _slots.Add(slot);
                    _icons.Add(slot.Q<VisualElement>($"Icon{i}"));
                    _quantities.Add(slot.Q<Label>($"Qty{i}"));
                    _bgs.Add(slot.Q<VisualElement>(className: "item-slot-bg"));

                    int slotIndex = i;
                    slot.RegisterCallback<MouseDownEvent>(evt => OnSlotClicked(evt, slotIndex));
                    slot.RegisterCallback<MouseEnterEvent>(evt => OnSlotMouseEnter(evt, slotIndex));
                    slot.RegisterCallback<MouseLeaveEvent>(evt => OnSlotMouseLeave(evt, slotIndex));
                }
            }

            // Hide initially
            _window.style.display = DisplayStyle.None;
            
            Debug.Log($"[LootBagController] Initialized with {_slots.Count} slots.");
        }

        private void Update() {
            if (_currentLootSource == null || _window == null || _window.style.display == DisplayStyle.None)
                return;

            // Close if player moved from where they opened the loot
            var player = FindLocalPlayer();
            if (player != null) {
                float distance = Vector3.Distance(player.transform.position, _playerPositionOnOpen);
                if (distance > _moveThreshold) {
                    CloseWindow();
                }
            }
        }

        private void OnLootOpened(ILootSource lootSource) {
            if (_currentLootSource != null) {
                _currentLootSource.OnItemsChanged -= RefreshUI;
            }

            _currentLootSource = lootSource;
            _currentLootSource.OnItemsChanged += RefreshUI;

            // Cache player position at the moment of opening
            var player = FindLocalPlayer();
            if (player != null) {
                _playerPositionOnOpen = player.transform.position;
            }

            if (_window != null) {
                _window.style.display = DisplayStyle.Flex;
                _title.text = lootSource.LootName.ToUpper();
                RefreshUI();
            }

            // Sonido especial si el cofre tiene un item Epic o mejor
            if (lootSource is ChestController) {
                bool hasEpic = false;
                foreach (var item in lootSource.LootItems) {
                    if (!item.IsEmpty && item.Rarity >= ItemRarity.Epic) {
                        hasEpic = true;
                        break;
                    }
                }
                if (hasEpic) {
                    EventBus.Trigger("OnEpicChestItem");
                }
            }
        }

        private void RefreshUI() {
            if (_currentLootSource == null || _slots.Count == 0) return;

            var items = _currentLootSource.LootItems;
            
            for (int i = 0; i < _slots.Count; i++) {
                if (i >= items.Count) {
                    ClearSlot(i);
                    continue;
                }

                var slotData = items[i];
                if (slotData.IsEmpty) {
                    ClearSlot(i);
                } else {
                    UpdateSlot(i, slotData);
                }
            }
            
            // If empty after refresh, close window
            if (items.Count == 0) {
                CloseWindow();
            }
        }

        private void UpdateSlot(int index, ItemSlot data) {
            var itemData = ItemDatabase.Instance.GetItem(data.ItemID);
            if (itemData == null) {
                ClearSlot(index);
                return;
            }

            if (_icons[index] != null) {
                _icons[index].style.backgroundImage = new StyleBackground(itemData.Icon);
                _icons[index].style.display = DisplayStyle.Flex;
            }

            if (_quantities[index] != null) {
                _quantities[index].text = data.Quantity > 1 ? data.Quantity.ToString() : "";
            }

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
            if (_currentLootSource == null) return;
            
            var items = _currentLootSource.LootItems;
            if (index >= items.Count || items[index].IsEmpty) return;

            // Simple click or right-click to take
            if (evt.button == 0 || evt.button == 1) {
                TakeItem(index);
                evt.StopPropagation();
            }
        }

        private void OnSlotMouseEnter(MouseEnterEvent evt, int index) {
            if (_currentLootSource == null || ItemTooltipController.Instance == null) return;

            var items = _currentLootSource.LootItems;
            if (index >= items.Count || items[index].IsEmpty) return;

            var slotData = items[index];
            var itemData = ItemDatabase.Instance.GetItem(slotData.ItemID);
            
            if (itemData != null) {
                ItemTooltipController.Instance.Show(itemData, slotData.Rarity);
            }
        }

        private void OnSlotMouseLeave(MouseLeaveEvent evt, int index) {
            ItemTooltipController.Instance?.Hide();
        }

        private void TakeItem(int index) {
            var localPlayer = FindLocalPlayer();
            if (localPlayer != null) {
                _currentLootSource.CmdTakeItem(index, localPlayer);
                UISoundPlayer.PlayLootPickup();
            }
        }

        private void OnTakeAllClicked() {
            var localPlayer = FindLocalPlayer();
            if (localPlayer != null && _currentLootSource != null) {
                _currentLootSource.CmdTakeAll(localPlayer);
                UISoundPlayer.PlayLootPickup();
                CloseWindow();
            }
        }

        private void CloseWindow() {
            if (_window != null) _window.style.display = DisplayStyle.None;
            if (_currentLootSource != null) {
                _currentLootSource.OnItemsChanged -= RefreshUI;
                _currentLootSource = null;
            }
        }

        private FishNet.Object.NetworkObject FindLocalPlayer() {
            var allPlayers = Object.FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
            foreach (var inventory in allPlayers) {
                if (inventory.IsOwner) return inventory.NetworkObject;
            }
            return null;
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

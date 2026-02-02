using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Genesis.Simulation;
using Genesis.Items;
using Genesis.Data;
using Genesis.Core;
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

        private void Update() {
            // Press 'E' to open nearest loot bag (matching debug behavior)
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) {
                TryOpenNearestLootSource();
            }
        }

        private void TryOpenNearestLootSource() {
            var player = FindLocalPlayer();
            if (player == null) return;

            // Find all Interactables that are ILootSource
            // Note: This is a bit expensive, but acceptable for now (similar to original code)
            var allInteractables = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            
            ILootSource nearest = null;
            float minDistance = float.MaxValue;

            foreach (var obj in allInteractables) {
                if (obj is ILootSource lootSource && obj is IInteractable interactable) {
                    float distance = Vector3.Distance(player.transform.position, obj.transform.position);
                    // Interact range 3m
                    if (distance < 3f && distance < minDistance) {
                        nearest = lootSource;
                        minDistance = distance;
                    }
                }
            }

            if (nearest != null) {
                // Send interaction request to server
                // The server will then respond (TargetRpc) to open the UI
                if (nearest is ILootSource source) {
                    source.CmdTryInteract(player);
                }
            }
        }

        private void InitializeUI() {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;

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
                }
            }

            // Hide initially
            _window.style.display = DisplayStyle.None;
            
            Debug.Log($"[LootBagController] Initialized with {_slots.Count} slots.");
        }

        private void OnLootOpened(ILootSource lootSource) {
            _currentLootSource = lootSource;
            if (_window != null) {
                _window.style.display = DisplayStyle.Flex;
                _title.text = lootSource.LootName.ToUpper();
                RefreshUI();
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

        private void TakeItem(int index) {
            var localPlayer = FindLocalPlayer();
            if (localPlayer != null) {
                _currentLootSource.CmdTakeItem(index, localPlayer);
                // Refresh after short delay to catch network sync
                Invoke(nameof(RefreshUI), 0.1f);
            }
        }

        private void OnTakeAllClicked() {
            var localPlayer = FindLocalPlayer();
            if (localPlayer != null && _currentLootSource != null) {
                _currentLootSource.CmdTakeAll(localPlayer);
                CloseWindow();
            }
        }

        private void CloseWindow() {
            if (_window != null) _window.style.display = DisplayStyle.None;
            _currentLootSource = null;
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

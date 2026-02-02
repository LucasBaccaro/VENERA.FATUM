using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Genesis.Simulation;
using Genesis.Items;
using Genesis.Data;
using Genesis.Core;

namespace Genesis.Presentation {
    public class LootBagDebugController : MonoBehaviour {
        [Header("UI")]
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _lootBagWindow;
        private Label _lootTitle;
        private VisualElement _lootList;
        private Button _takeAllButton;
        private Button _closeButton;

        private ILootSource _currentLootSource;

        private void Awake() {
            if (_uiDocument == null) {
                _uiDocument = GetComponent<UIDocument>();
            }
        }

        private void OnEnable() {
            EventBus.Subscribe<ILootSource>("OnLootOpened", OnLootOpened);
        }

        private void OnDisable() {
            EventBus.Unsubscribe<ILootSource>("OnLootOpened", OnLootOpened);
        }

        private void Start() {
            InitializeUI();
        }

        private void InitializeUI() {
            var root = _uiDocument.rootVisualElement;
            _lootBagWindow = root.Q<VisualElement>("LootBagWindow");
            _lootTitle = root.Q<Label>("LootTitle");
            _lootList = root.Q<VisualElement>("LootList");
            _takeAllButton = root.Q<Button>("TakeAllButton");
            _closeButton = root.Q<Button>("CloseButton");

            // Hide initially
            _lootBagWindow.style.display = DisplayStyle.None;

            // Setup button callbacks
            _takeAllButton.clicked += OnTakeAllClicked;
            _closeButton.clicked += OnCloseClicked;
        }

        private void OnLootOpened(ILootSource lootSource) {
            _currentLootSource = lootSource;
            ShowLootBag();
        }

        private void ShowLootBag() {
            if (_currentLootSource == null) return;

            _lootBagWindow.style.display = DisplayStyle.Flex;
            _lootTitle.text = $"LOOT: {_currentLootSource.LootName}";

            RefreshLoot();
        }

        private void RefreshLoot() {
            if (_lootList == null || _currentLootSource == null) return;

            _lootList.Clear();

            var items = _currentLootSource.LootItems;
            for (int i = 0; i < items.Count; i++) {
                var slot = items[i];
                if (slot.IsEmpty) continue;

                var itemData = ItemDatabase.Instance.GetItem(slot.ItemID);
                if (itemData == null) continue;

                int index = i; // Capture index for button callback

                // Create item row
                var itemRow = new VisualElement();
                itemRow.style.flexDirection = FlexDirection.Row;
                itemRow.style.alignItems = Align.Center;
                itemRow.style.marginBottom = 5;
                itemRow.style.paddingTop = 5;
                itemRow.style.paddingBottom = 5;
                itemRow.style.paddingLeft = 8;
                itemRow.style.paddingRight = 8;
                itemRow.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);

                // Item icon
                var iconContainer = new VisualElement();
                iconContainer.style.width = 45;
                iconContainer.style.height = 45;
                iconContainer.style.marginRight = 12;
                iconContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
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
#pragma warning disable CS0618
                    iconContainer.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
#pragma warning restore CS0618
                }

                // Right-click to take
                iconContainer.RegisterCallback<MouseDownEvent>(evt => {
                    if (evt.button == 1) { // Right-click
                        OnTakeItemClicked(index);
                        evt.StopPropagation();
                    }
                });

                itemRow.Add(iconContainer);

                // Item info
                var infoContainer = new VisualElement();
                infoContainer.style.flexGrow = 1;
                infoContainer.style.flexDirection = FlexDirection.Column;

                var nameLabel = new Label(itemData.ItemName);
                nameLabel.style.color = GetRarityColor(slot.Rarity);
                nameLabel.style.fontSize = 14;
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                var quantityLabel = new Label($"x{slot.Quantity} • Right-click to loot");
                quantityLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                quantityLabel.style.fontSize = 11;

                infoContainer.Add(nameLabel);
                infoContainer.Add(quantityLabel);
                itemRow.Add(infoContainer);

                _lootList.Add(itemRow);
            }

            // If empty, show message and close
            if (_lootList.childCount == 0) {
                var emptyLabel = new Label("Bolsa vacía");
                emptyLabel.style.color = Color.gray;
                emptyLabel.style.fontSize = 14;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.marginTop = 20;
                _lootList.Add(emptyLabel);

                // Auto-close after 1 second
                Invoke(nameof(OnCloseClicked), 1f);
            }
        }

        private void OnTakeItemClicked(int index) {
            if (_currentLootSource == null) return;

            var player = FindLocalPlayer();
            if (player == null) return;

            _currentLootSource.CmdTakeItem(index, player);
            Debug.Log($"[LootBagDebugController] Tomado item index {index}");

            // Refresh loot UI after a small delay
            Invoke(nameof(RefreshLoot), 0.1f);

            // Force refresh player inventory UI
            Invoke(nameof(ForceInventoryRefresh), 0.2f);
        }

        private void OnTakeAllClicked() {
            if (_currentLootSource == null) return;

            var player = FindLocalPlayer();
            if (player == null) return;

            _currentLootSource.CmdTakeAll(player);
            Debug.Log("[LootBagDebugController] Tomando todos los items");

            // Force refresh player inventory UI
            Invoke(nameof(ForceInventoryRefresh), 0.2f);

            OnCloseClicked();
        }

        private void ForceInventoryRefresh() {
            var player = FindLocalPlayer();
            if (player == null) return;

            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null) {
                inventory.ForceRefreshUI();
                Debug.Log("[LootBagDebugController] Forced inventory refresh after looting");
            }
        }

        private void OnCloseClicked() {
            _lootBagWindow.style.display = DisplayStyle.None;
            _currentLootSource = null;
        }

        private FishNet.Object.NetworkObject FindLocalPlayer() {
            var allPlayers = Object.FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
            foreach (var player in allPlayers) {
                if (player.IsOwner) {
                    return player.NetworkObject;
                }
            }
            return null;
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

        private void Update() {
            // Press 'E' to open nearest loot bag (debug)
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) {
                TryOpenNearestLootSource();
            }
        }

        private void TryOpenNearestLootSource() {
            var player = FindLocalPlayer();
            if (player == null) return;

            // Debug controller will check all Interactables that implement ILootSource
            var allInteractables = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            
            ILootSource nearest = null;
            float minDistance = float.MaxValue;

            foreach (var obj in allInteractables) {
                if (obj is ILootSource lootSource && obj is IInteractable interactable) {
                    float distance = Vector3.Distance(player.transform.position, obj.transform.position);
                    // Standard interact range 3m
                    if (distance < 3f && distance < minDistance) {
                        nearest = lootSource;
                        minDistance = distance;
                    }
                }
            }

            if (nearest != null) {
                OnLootOpened(nearest);
            }
        }
    }
}

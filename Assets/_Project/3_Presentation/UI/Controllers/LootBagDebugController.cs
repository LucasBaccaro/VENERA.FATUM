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

        private LootBag _currentLootBag;

        private void Awake() {
            if (_uiDocument == null) {
                _uiDocument = GetComponent<UIDocument>();
            }
        }

        private void OnEnable() {
            EventBus.Subscribe<LootBag>("OnLootBagOpened", OnLootBagOpened);
        }

        private void OnDisable() {
            EventBus.Unsubscribe<LootBag>("OnLootBagOpened", OnLootBagOpened);
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

        private void OnLootBagOpened(LootBag lootBag) {
            _currentLootBag = lootBag;
            ShowLootBag();
        }

        private void ShowLootBag() {
            if (_currentLootBag == null) return;

            _lootBagWindow.style.display = DisplayStyle.Flex;
            _lootTitle.text = $"LOOT: {_currentLootBag.OwnerName}";

            RefreshLoot();
        }

        private void RefreshLoot() {
            if (_lootList == null || _currentLootBag == null) return;

            _lootList.Clear();

            var items = _currentLootBag.LootItems;
            for (int i = 0; i < items.Count; i++) {
                var slot = items[i];
                if (slot.IsEmpty) continue;

                var itemData = ItemDatabase.Instance.GetItem(slot.ItemID);
                if (itemData == null) continue;

                int index = i; // Capture index for button callback

                // Create item row
                var itemRow = new VisualElement();
                itemRow.style.flexDirection = FlexDirection.Row;
                itemRow.style.justifyContent = Justify.SpaceBetween;
                itemRow.style.marginBottom = 5;
                itemRow.style.paddingTop = 5;
                itemRow.style.paddingBottom = 5;
                itemRow.style.paddingLeft = 8;
                itemRow.style.paddingRight = 8;
                itemRow.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);

                // Item info
                var infoContainer = new VisualElement();
                infoContainer.style.flexDirection = FlexDirection.Column;

                var nameLabel = new Label(itemData.ItemName);
                nameLabel.style.color = GetRarityColor(slot.Rarity);
                nameLabel.style.fontSize = 14;
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                var quantityLabel = new Label($"Cantidad: {slot.Quantity}");
                quantityLabel.style.color = Color.gray;
                quantityLabel.style.fontSize = 11;

                infoContainer.Add(nameLabel);
                infoContainer.Add(quantityLabel);
                itemRow.Add(infoContainer);

                // Take button
                var takeButton = new Button(() => OnTakeItemClicked(index));
                takeButton.text = "TAKE";
                takeButton.style.width = 70;
                takeButton.style.height = 35;
                takeButton.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f);
                takeButton.style.color = Color.white;
                takeButton.style.fontSize = 12;
                takeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
                itemRow.Add(takeButton);

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
            if (_currentLootBag == null) return;

            var player = FindLocalPlayer();
            if (player == null) return;

            _currentLootBag.CmdTakeItem(index, player);
            Debug.Log($"[LootBagDebugController] Tomado item index {index}");

            // Refresh loot UI after a small delay
            Invoke(nameof(RefreshLoot), 0.1f);

            // Force refresh player inventory UI
            Invoke(nameof(ForceInventoryRefresh), 0.2f);
        }

        private void OnTakeAllClicked() {
            if (_currentLootBag == null) return;

            var player = FindLocalPlayer();
            if (player == null) return;

            _currentLootBag.CmdTakeAll(player);
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
            _currentLootBag = null;
        }

        private FishNet.Object.NetworkObject FindLocalPlayer() {
            var allPlayers = FindObjectsOfType<PlayerInventory>();
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
                TryOpenNearestLootBag();
            }
        }

        private void TryOpenNearestLootBag() {
            var player = FindLocalPlayer();
            if (player == null) return;

            var allLootBags = FindObjectsOfType<LootBag>();
            LootBag nearest = null;
            float minDistance = float.MaxValue;

            foreach (var bag in allLootBags) {
                float distance = Vector3.Distance(player.transform.position, bag.transform.position);
                if (distance < 3f && distance < minDistance) {
                    nearest = bag;
                    minDistance = distance;
                }
            }

            if (nearest != null) {
                OnLootBagOpened(nearest);
            }
        }
    }
}

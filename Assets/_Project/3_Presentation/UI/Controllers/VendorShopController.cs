using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Genesis.Core;
using Genesis.Data;
using Genesis.Items;
using Genesis.Simulation;

namespace Genesis.Presentation.UI {

    public class VendorShopController : MonoBehaviour {

        [Header("UI")]
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _window;
        private Label _vendorTitle;
        private Label _playerGoldLabel;
        private VisualElement _itemList;
        private Button _closeButton;

        private string _currentNpcId;
        private VendorInventoryData _currentVendorData;
        private VendorTransactionHandler _transactionHandler;
        private bool _isVisible;

        private void Awake() {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable() {
            EventBus.Subscribe<string, string>("OnVendorUIOpen", OnVendorUIOpen);
            EventBus.Subscribe<int>("OnGoldChanged", OnGoldChanged);
            EventBus.Subscribe<bool, string>("OnVendorBuyResult", OnBuyResult);
        }

        private void OnDisable() {
            EventBus.Unsubscribe<string, string>("OnVendorUIOpen", OnVendorUIOpen);
            EventBus.Unsubscribe<int>("OnGoldChanged", OnGoldChanged);
            EventBus.Unsubscribe<bool, string>("OnVendorBuyResult", OnBuyResult);
        }

        private void Start() {
            InitializeUI();
        }

        private void Update() {
            if (!_isVisible) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) {
                CloseWindow();
            }
        }

        private void InitializeUI() {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            if (root == null) return;

            root.pickingMode = PickingMode.Ignore;
            _window = root.Q<VisualElement>("VendorWindow");
            _vendorTitle = root.Q<Label>("VendorTitle");
            _playerGoldLabel = root.Q<Label>("PlayerGoldLabel");
            _itemList = root.Q<VisualElement>("VendorItemList");
            _closeButton = root.Q<Button>("CloseButton");

            if (_closeButton != null) _closeButton.clicked += CloseWindow;

            if (_window != null) _window.style.display = DisplayStyle.None;
        }

        private void OnVendorUIOpen(string npcId, string displayName) {
            _currentNpcId = npcId;

            // Find vendor data from the NPC
            var npcs = FindObjectsByType<NpcController>(FindObjectsSortMode.None);
            foreach (var npc in npcs) {
                if (npc.NpcID == npcId && npc.NpcData != null && npc.NpcData.IsVendor) {
                    _currentVendorData = npc.NpcData.VendorInventory;
                    break;
                }
            }

            if (_currentVendorData == null) {
                Debug.LogWarning($"[VendorShop] No vendor data found for NPC {npcId}");
                return;
            }

            // Find transaction handler
            if (_transactionHandler == null) {
                _transactionHandler = FindFirstObjectByType<VendorTransactionHandler>();
            }

            if (_window != null) {
                _vendorTitle.text = displayName.ToUpper();
                UpdateGoldDisplay();
                BuildItemList();
                _window.style.display = DisplayStyle.Flex;
                _isVisible = true;
                Audio.UISoundPlayer.PlayOpen();
            }
        }

        private void BuildItemList() {
            if (_itemList == null || _currentVendorData == null) return;
            _itemList.Clear();

            for (int i = 0; i < _currentVendorData.Items.Length; i++) {
                VendorItem vendorItem = _currentVendorData.Items[i];
                BaseItemData itemData = ItemDatabase.Instance.GetItem(vendorItem.ItemID);
                if (itemData == null) continue;

                int itemIndex = i;
                VisualElement row = CreateItemRow(itemData, vendorItem, itemIndex);
                _itemList.Add(row);
            }
        }

        private VisualElement CreateItemRow(BaseItemData itemData, VendorItem vendorItem, int index) {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.marginBottom = 2;
            row.style.backgroundColor = new Color(0, 0, 0, 0.3f);
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;

            // Icon
            var icon = new VisualElement();
            icon.style.width = 36;
            icon.style.height = 36;
            icon.style.marginRight = 8;
            icon.style.borderBottomLeftRadius = 4;
            icon.style.borderBottomRightRadius = 4;
            icon.style.borderTopLeftRadius = 4;
            icon.style.borderTopRightRadius = 4;
            if (itemData.Icon != null) {
                icon.style.backgroundImage = new StyleBackground(itemData.Icon);
            }
            row.Add(icon);

            // Name + Price container
            var info = new VisualElement();
            info.style.flexGrow = 1;
            info.style.flexDirection = FlexDirection.Column;

            var nameLabel = new Label(itemData.ItemName);
            nameLabel.style.color = GetRarityColor(vendorItem.Rarity);
            nameLabel.style.fontSize = 13;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            info.Add(nameLabel);

            var priceLabel = new Label($"{vendorItem.Price} gold");
            priceLabel.style.color = new Color(1f, 0.84f, 0f);
            priceLabel.style.fontSize = 11;
            info.Add(priceLabel);

            row.Add(info);

            // Buy button
            var buyBtn = new Button(() => OnBuyClicked(index));
            buyBtn.text = "Buy";
            buyBtn.style.width = 50;
            buyBtn.style.height = 28;
            buyBtn.style.fontSize = 12;
            row.Add(buyBtn);

            // Tooltip on hover
            row.RegisterCallback<MouseEnterEvent>(evt => {
                if (ItemTooltipController.Instance != null) {
                    ItemTooltipController.Instance.Show(itemData, vendorItem.Rarity);
                }
            });
            row.RegisterCallback<MouseLeaveEvent>(evt => {
                ItemTooltipController.Instance?.Hide();
            });

            return row;
        }

        private void OnBuyClicked(int itemIndex) {
            if (_transactionHandler == null || string.IsNullOrEmpty(_currentNpcId)) return;
            _transactionHandler.CmdBuyItem(_currentNpcId, itemIndex);
        }

        private void OnGoldChanged(int newGold) {
            UpdateGoldDisplay();
        }

        private void UpdateGoldDisplay() {
            if (_playerGoldLabel == null) return;

            var player = FindLocalPlayer();
            if (player != null) {
                var attrs = player.GetComponent<PlayerAttributes>();
                if (attrs != null) {
                    _playerGoldLabel.text = attrs.Gold.ToString();
                    return;
                }
            }
            _playerGoldLabel.text = "0";
        }

        private void OnBuyResult(bool success, string message) {
            if (success) {
                UpdateGoldDisplay();
            }
        }

        private void CloseWindow() {
            if (_window != null) _window.style.display = DisplayStyle.None;
            _isVisible = false;
            _currentVendorData = null;
            _currentNpcId = null;
            ItemTooltipController.Instance?.Hide();
            Audio.UISoundPlayer.PlayClose();
        }

        private FishNet.Object.NetworkObject FindLocalPlayer() {
            var allPlayers = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
            foreach (var inventory in allPlayers) {
                if (inventory.IsOwner) return inventory.NetworkObject;
            }
            return null;
        }

        private Color GetRarityColor(ItemRarity rarity) {
            return rarity switch {
                ItemRarity.Uncommon => new Color(0.12f, 1f, 0f),
                ItemRarity.Rare => new Color(0f, 0.44f, 0.87f),
                ItemRarity.Epic => new Color(0.64f, 0.21f, 0.93f),
                _ => Color.white
            };
        }
    }
}

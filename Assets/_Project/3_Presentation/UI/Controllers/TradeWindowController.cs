using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Genesis.Simulation;
using Genesis.Items;
using Genesis.Data;
using Genesis.Core;

namespace Genesis.Presentation.UI {

    public class TradeWindowController : MonoBehaviour {

        [SerializeField] private UIDocument _uiDocument;

        private TradeManager _tradeManager;
        private PlayerInventory _playerInventory;

        // Trade Window elements
        private VisualElement _tradeWindow;
        private Label _tradeTitle;
        private Label _localName;
        private Label _partnerName;
        private Label _statusLabel;
        private TextField _localGoldInput;
        private Label _partnerGoldLabel;
        private Button _lockButton;
        private Button _acceptButton;
        private Button _cancelButton;
        private VisualElement _countdownOverlay;
        private Label _countdownLabel;

        // Request popup elements
        private VisualElement _requestPopup;
        private Label _requestLabel;
        private Button _acceptRequestButton;
        private Button _declineRequestButton;

        // Slot caches
        private VisualElement[] _localIcons = new VisualElement[6];
        private Label[] _localQtys = new Label[6];
        private VisualElement[] _localBgs = new VisualElement[6];
        private VisualElement[] _localSlots = new VisualElement[6];
        private VisualElement[] _partnerIcons = new VisualElement[6];
        private Label[] _partnerQtys = new Label[6];
        private VisualElement[] _partnerBgs = new VisualElement[6];

        private bool _isTradeOpen;
        private bool _isLocked;
        private uint _pendingSessionId;
        private float _countdownTime;
        private bool _countdownActive;

        private void Awake() {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable() {
            EventBus.Subscribe<uint, string>("OnTradeRequested", OnTradeRequested);
            EventBus.Subscribe<uint, string>("OnTradeOpened", OnTradeOpened);
            EventBus.Subscribe<TradeOfferSnapshot>("OnTradeOfferUpdated", OnTradeOfferUpdated);
            EventBus.Subscribe<TradeLockSnapshot>("OnTradeLockChanged", OnTradeLockChanged);
            EventBus.Subscribe<float>("OnTradeCountdownStarted", OnTradeCountdownStarted);
            EventBus.Subscribe("OnTradeCompleted", OnTradeCompleted);
            EventBus.Subscribe<string>("OnTradeCancelled", OnTradeCancelled);
        }

        private void OnDisable() {
            EventBus.Unsubscribe<uint, string>("OnTradeRequested", OnTradeRequested);
            EventBus.Unsubscribe<uint, string>("OnTradeOpened", OnTradeOpened);
            EventBus.Unsubscribe<TradeOfferSnapshot>("OnTradeOfferUpdated", OnTradeOfferUpdated);
            EventBus.Unsubscribe<TradeLockSnapshot>("OnTradeLockChanged", OnTradeLockChanged);
            EventBus.Unsubscribe<float>("OnTradeCountdownStarted", OnTradeCountdownStarted);
            EventBus.Unsubscribe("OnTradeCompleted", OnTradeCompleted);
            EventBus.Unsubscribe<string>("OnTradeCancelled", OnTradeCancelled);
        }

        private void Start() {
            InitializeUI();
        }

        private void Update() {
            if (_countdownActive) {
                _countdownTime -= Time.deltaTime;
                if (_countdownTime > 0f) {
                    _countdownLabel.text = Mathf.CeilToInt(_countdownTime).ToString();
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // PUBLIC SETUP
        // ═══════════════════════════════════════════════════════

        public void SetReferences(TradeManager tradeManager, PlayerInventory inventory) {
            _tradeManager = tradeManager;
            _playerInventory = inventory;
        }

        // ═══════════════════════════════════════════════════════
        // UI INITIALIZATION
        // ═══════════════════════════════════════════════════════

        private void InitializeUI() {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            if (root == null) return;

            root.pickingMode = PickingMode.Ignore;

            // Trade Window
            _tradeWindow = root.Q<VisualElement>("TradeWindow");
            _tradeTitle = root.Q<Label>("TradeTitle");
            _localName = root.Q<Label>("LocalName");
            _partnerName = root.Q<Label>("PartnerName");
            _statusLabel = root.Q<Label>("StatusLabel");
            _localGoldInput = root.Q<TextField>("LocalGoldInput");
            _partnerGoldLabel = root.Q<Label>("PartnerGoldLabel");
            _lockButton = root.Q<Button>("LockButton");
            _acceptButton = root.Q<Button>("AcceptButton");
            _cancelButton = root.Q<Button>("CancelButton");
            _countdownOverlay = root.Q<VisualElement>("CountdownOverlay");
            _countdownLabel = root.Q<Label>("CountdownLabel");

            // Request Popup
            _requestPopup = root.Q<VisualElement>("TradeRequestPopup");
            _requestLabel = root.Q<Label>("RequestLabel");
            _acceptRequestButton = root.Q<Button>("AcceptRequestButton");
            _declineRequestButton = root.Q<Button>("DeclineRequestButton");

            // Cache slot elements
            for (int i = 0; i < 6; i++) {
                _localSlots[i] = root.Q<VisualElement>($"LocalSlot{i}");
                _localIcons[i] = root.Q<VisualElement>($"LocalIcon{i}");
                _localQtys[i] = root.Q<Label>($"LocalQty{i}");
                if (_localSlots[i] != null)
                    _localBgs[i] = _localSlots[i].Q<VisualElement>(className: "item-slot-bg");

                var partnerSlot = root.Q<VisualElement>($"PartnerSlot{i}");
                _partnerIcons[i] = root.Q<VisualElement>($"PartnerIcon{i}");
                _partnerQtys[i] = root.Q<Label>($"PartnerQty{i}");
                if (partnerSlot != null)
                    _partnerBgs[i] = partnerSlot.Q<VisualElement>(className: "item-slot-bg");
            }

            // Button callbacks
            if (_lockButton != null)
                _lockButton.clicked += OnLockClicked;
            if (_acceptButton != null)
                _acceptButton.clicked += OnAcceptClicked;
            if (_cancelButton != null)
                _cancelButton.clicked += OnCancelClicked;
            if (_acceptRequestButton != null)
                _acceptRequestButton.clicked += OnAcceptRequest;
            if (_declineRequestButton != null)
                _declineRequestButton.clicked += OnDeclineRequest;

            // Gold input change
            if (_localGoldInput != null) {
                _localGoldInput.RegisterValueChangedCallback(OnGoldInputChanged);
            }

            // Local slot click -> remove item from trade
            for (int i = 0; i < 6; i++) {
                int slotIndex = i;
                if (_localSlots[i] != null) {
                    _localSlots[i].RegisterCallback<MouseDownEvent>(evt => {
                        if (evt.button == 0 && _isTradeOpen && !_isLocked && _tradeManager != null) {
                            _tradeManager.CmdRemoveTradeItem(slotIndex);
                        }
                    });
                }
            }

            // Accept starts disabled
            if (_acceptButton != null) _acceptButton.SetEnabled(false);

            // Hide everything at start
            if (_tradeWindow != null) _tradeWindow.style.display = DisplayStyle.None;
            if (_requestPopup != null) _requestPopup.style.display = DisplayStyle.None;
            if (_countdownOverlay != null) _countdownOverlay.style.display = DisplayStyle.None;
        }

        // ═══════════════════════════════════════════════════════
        // EVENT HANDLERS
        // ═══════════════════════════════════════════════════════

        private void OnTradeRequested(uint sessionId, string requesterName) {
            Debug.Log($"[TradeWindow] OnTradeRequested received: sessionId={sessionId}, from={requesterName}, tradeManager={(_tradeManager != null ? _tradeManager.gameObject.name : "NULL")}");
            _pendingSessionId = sessionId;
            if (_requestLabel != null) _requestLabel.text = $"{requesterName} wants to trade";
            if (_requestPopup != null) _requestPopup.style.display = DisplayStyle.Flex;
            Audio.UISoundPlayer.PlayOpen();
        }

        private void OnTradeOpened(uint sessionId, string partnerDisplayName) {
            _isTradeOpen = true;
            _isLocked = false;
            _countdownActive = false;

            if (_partnerName != null) _partnerName.text = partnerDisplayName;
            if (_localName != null) _localName.text = "You";
            if (_statusLabel != null) _statusLabel.text = "";
            if (_localGoldInput != null) { _localGoldInput.value = "0"; _localGoldInput.SetEnabled(true); }
            if (_lockButton != null) { _lockButton.text = "Lock"; _lockButton.RemoveFromClassList("locked"); _lockButton.SetEnabled(true); }
            if (_acceptButton != null) { _acceptButton.text = "Accept"; _acceptButton.RemoveFromClassList("accepted"); _acceptButton.SetEnabled(false); }
            if (_countdownOverlay != null) _countdownOverlay.style.display = DisplayStyle.None;

            // Clear all slots
            for (int i = 0; i < 6; i++) {
                ClearSlot(_localIcons[i], _localQtys[i], _localBgs[i]);
                ClearSlot(_partnerIcons[i], _partnerQtys[i], _partnerBgs[i]);
            }

            if (_requestPopup != null) _requestPopup.style.display = DisplayStyle.None;
            if (_tradeWindow != null) _tradeWindow.style.display = DisplayStyle.Flex;
            Audio.UISoundPlayer.PlayOpen();
        }

        private void OnTradeOfferUpdated(TradeOfferSnapshot snapshot) {
            // Update local slots
            for (int i = 0; i < 6; i++) {
                UpdateSlot(_localIcons[i], _localQtys[i], _localBgs[i],
                    snapshot.MyItemIds[i], snapshot.MyItemQtys[i],
                    (ItemTier)snapshot.MyItemTiers[i], (ItemRarity)snapshot.MyItemRarities[i]);

                UpdateSlot(_partnerIcons[i], _partnerQtys[i], _partnerBgs[i],
                    snapshot.PartnerItemIds[i], snapshot.PartnerItemQtys[i],
                    (ItemTier)snapshot.PartnerItemTiers[i], (ItemRarity)snapshot.PartnerItemRarities[i]);
            }

            // Update gold displays
            if (_localGoldInput != null) _localGoldInput.SetValueWithoutNotify(snapshot.MyGold.ToString());
            if (_partnerGoldLabel != null) _partnerGoldLabel.text = snapshot.PartnerGold.ToString();

            // Update lock/accept states from snapshot
            UpdateLockAcceptUI(snapshot.MyLocked, snapshot.MyAccepted, snapshot.PartnerLocked, snapshot.PartnerAccepted);
        }

        private void OnTradeLockChanged(TradeLockSnapshot snapshot) {
            UpdateLockAcceptUI(snapshot.MyLocked, snapshot.MyAccepted, snapshot.PartnerLocked, snapshot.PartnerAccepted);
        }

        private void OnTradeCountdownStarted(float duration) {
            _countdownActive = true;
            _countdownTime = duration;
            if (_countdownOverlay != null) _countdownOverlay.style.display = DisplayStyle.Flex;
            if (_countdownLabel != null) _countdownLabel.text = Mathf.CeilToInt(duration).ToString();
            if (_lockButton != null) _lockButton.SetEnabled(false);
            if (_acceptButton != null) _acceptButton.SetEnabled(false);
            if (_statusLabel != null) _statusLabel.text = "Completing trade...";
        }

        private void OnTradeCompleted() {
            if (_statusLabel != null) _statusLabel.text = "Trade completed!";
            _countdownActive = false;
            Audio.UISoundPlayer.PlayOpen();

            // Close after brief delay
            Invoke(nameof(CloseTradeWindow), 1f);
        }

        private void OnTradeCancelled(string reason) {
            _countdownActive = false;
            if (_statusLabel != null) _statusLabel.text = reason;

            // Close popups/windows
            if (_requestPopup != null) _requestPopup.style.display = DisplayStyle.None;

            if (_isTradeOpen) {
                Audio.UISoundPlayer.PlayClose();
                Invoke(nameof(CloseTradeWindow), 1f);
            }
        }

        // ═══════════════════════════════════════════════════════
        // BUTTON CALLBACKS
        // ═══════════════════════════════════════════════════════

        private void OnLockClicked() {
            if (_tradeManager == null) return;

            if (_isLocked) {
                _tradeManager.CmdUnlockTrade();
            } else {
                // Submit gold before locking
                SubmitGold();
                _tradeManager.CmdLockTrade();
            }
        }

        private void OnAcceptClicked() {
            if (_tradeManager == null) return;
            _tradeManager.CmdAcceptTrade();
        }

        private void OnCancelClicked() {
            if (_tradeManager == null) return;
            _tradeManager.CmdCancelTrade();
        }

        private void OnAcceptRequest() {
            if (_tradeManager == null) {
                Debug.LogWarning("[TradeWindow] OnAcceptRequest: _tradeManager is NULL");
                return;
            }
            Debug.Log($"[TradeWindow] Accepting trade: sessionId={_pendingSessionId}, tradeManager owner={_tradeManager.gameObject.name}");
            _tradeManager.CmdRespondTrade(_pendingSessionId, true);
            if (_requestPopup != null) _requestPopup.style.display = DisplayStyle.None;
        }

        private void OnDeclineRequest() {
            if (_tradeManager == null) {
                Debug.LogWarning("[TradeWindow] OnDeclineRequest: _tradeManager is NULL");
                return;
            }
            Debug.Log($"[TradeWindow] Declining trade: sessionId={_pendingSessionId}");
            _tradeManager.CmdRespondTrade(_pendingSessionId, false);
            if (_requestPopup != null) _requestPopup.style.display = DisplayStyle.None;
        }

        private void OnGoldInputChanged(ChangeEvent<string> evt) {
            // Only submit when not locked
            if (_isLocked) return;
        }

        private void SubmitGold() {
            if (_tradeManager == null || _localGoldInput == null) return;
            if (int.TryParse(_localGoldInput.value, out int amount)) {
                if (amount < 0) amount = 0;
                _tradeManager.CmdSetTradeGold(amount);
            }
        }

        // ═══════════════════════════════════════════════════════
        // INVENTORY SLOT CLICK (Add item to trade)
        // ═══════════════════════════════════════════════════════

        public void OnInventorySlotClicked(int inventorySlotIndex) {
            if (!_isTradeOpen || _isLocked || _tradeManager == null) return;
            _tradeManager.CmdAddTradeItem(inventorySlotIndex);
        }

        public bool IsTradeOpen => _isTradeOpen;

        // ═══════════════════════════════════════════════════════
        // UI HELPERS
        // ═══════════════════════════════════════════════════════

        private void UpdateLockAcceptUI(bool myLocked, bool myAccepted, bool partnerLocked, bool partnerAccepted) {
            _isLocked = myLocked;

            // Lock button
            if (_lockButton != null) {
                _lockButton.text = myLocked ? "Unlock" : "Lock";
                if (myLocked) _lockButton.AddToClassList("locked");
                else _lockButton.RemoveFromClassList("locked");
            }

            // Gold input disabled when locked
            if (_localGoldInput != null) _localGoldInput.SetEnabled(!myLocked);

            // Accept button: only enabled when both locked
            if (_acceptButton != null) {
                bool canAccept = myLocked && partnerLocked && !myAccepted;
                _acceptButton.SetEnabled(canAccept);
                if (myAccepted) {
                    _acceptButton.text = "Accepted";
                    _acceptButton.AddToClassList("accepted");
                } else {
                    _acceptButton.text = "Accept";
                    _acceptButton.RemoveFromClassList("accepted");
                }
            }

            // Status label
            if (_statusLabel != null) {
                if (myLocked && partnerLocked) {
                    _statusLabel.text = "Both locked - ready to accept";
                } else if (myLocked) {
                    _statusLabel.text = "Waiting for partner to lock...";
                } else if (partnerLocked) {
                    _statusLabel.text = "Partner is locked";
                } else {
                    _statusLabel.text = "";
                }
            }
        }

        private void UpdateSlot(VisualElement icon, Label qty, VisualElement bg, int itemId, int quantity, ItemTier tier, ItemRarity rarity) {
            if (itemId == 0) {
                ClearSlot(icon, qty, bg);
                return;
            }

            var itemData = ItemDatabase.Instance?.GetItem(itemId);
            if (itemData == null) {
                ClearSlot(icon, qty, bg);
                return;
            }

            if (icon != null) {
                icon.style.backgroundImage = new StyleBackground(itemData.Icon);
                icon.style.display = DisplayStyle.Flex;
            }
            if (qty != null) {
                qty.text = quantity > 1 ? quantity.ToString() : "";
            }
            if (bg != null) {
                bg.ClearClassList();
                bg.AddToClassList("item-slot-bg");
                bg.AddToClassList(GetRarityClass(rarity));
            }
        }

        private void ClearSlot(VisualElement icon, Label qty, VisualElement bg) {
            if (icon != null) icon.style.display = DisplayStyle.None;
            if (qty != null) qty.text = "";
            if (bg != null) {
                bg.ClearClassList();
                bg.AddToClassList("item-slot-bg");
            }
        }

        private void CloseTradeWindow() {
            _isTradeOpen = false;
            _isLocked = false;
            _countdownActive = false;
            if (_tradeWindow != null) _tradeWindow.style.display = DisplayStyle.None;
            if (_countdownOverlay != null) _countdownOverlay.style.display = DisplayStyle.None;
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

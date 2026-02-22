using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using FishNet.Object;
using Genesis.Simulation;

namespace Genesis.Presentation.UI {

    public class PlayerContextMenuController : MonoBehaviour {

        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private LayerMask _playerLayer;

        private TradeManager _tradeManager;
        private NetworkObject _localPlayerNob;

        private VisualElement _popup;
        private Label _targetNameLabel;
        private Button _tradeButton;

        private NetworkObject _targetPlayer;
        private bool _isVisible;
        private Camera _mainCamera;
        private int _showFrame; // Frame when popup was shown

        private void Awake() {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();
        }

        private void Start() {
            InitializeUI();
        }

        public void SetReferences(TradeManager tradeManager, NetworkObject localNob) {
            _tradeManager = tradeManager;
            _localPlayerNob = localNob;
            Debug.Log($"[ContextMenu] References set: tradeManager={tradeManager.gameObject.name}, localNob={localNob.ObjectId}");
        }

        private void InitializeUI() {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            if (root == null) return;

            root.pickingMode = PickingMode.Ignore;

            _popup = root.Q<VisualElement>("ContextMenuPopup");
            _targetNameLabel = root.Q<Label>("TargetNameLabel");
            _tradeButton = root.Q<Button>("TradeButton");

            if (_tradeButton != null) {
                _tradeButton.clicked += OnTradeClicked;
            }

            if (_popup != null) _popup.style.display = DisplayStyle.None;
        }

        private void Update() {
            if (_localPlayerNob == null) return;
            if (LoginController.IsActive) return;

            // Right-click detection
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) {
                if (_isVisible) {
                    Hide();
                } else {
                    TryShowContextMenu();
                }
            }

            // Hide on left click (but skip the frame the popup was shown to avoid race)
            if (_isVisible && Time.frameCount > _showFrame + 1) {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) {
                    // Check if click is outside popup bounds
                    Vector2 mousePos = Mouse.current.position.ReadValue();
                    float uiY = Screen.height - mousePos.y;
                    var bounds = _popup.worldBound;
                    if (!bounds.Contains(new Vector2(mousePos.x, uiY))) {
                        Hide();
                    }
                }
            }

            // Hide on Escape
            if (_isVisible) {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) {
                    Hide();
                }

                // Hide if target moves too far
                if (_targetPlayer != null && _localPlayerNob != null) {
                    float dist = Vector3.Distance(_localPlayerNob.transform.position, _targetPlayer.transform.position);
                    if (dist > TradeConstants.TRADE_RANGE * 2f) {
                        Hide();
                    }
                }
            }
        }

        private void TryShowContextMenu() {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, _playerLayer)) {
                var nob = hit.collider.GetComponentInParent<NetworkObject>();
                if (nob == null || nob == _localPlayerNob) return;

                // Verify it's a player (has TradeManager)
                var tm = nob.GetComponent<TradeManager>();
                if (tm == null) return;

                _targetPlayer = nob;

                // Get player name
                string playerName = "Player";
                var classManager = nob.GetComponent<PlayerClassManager>();
                if (classManager != null && !string.IsNullOrEmpty(classManager.PlayerName)) {
                    playerName = classManager.PlayerName;
                }

                if (_targetNameLabel != null) _targetNameLabel.text = playerName;

                // Position popup near mouse, clamped to screen
                PositionPopup(mousePos);

                if (_popup != null) _popup.style.display = DisplayStyle.Flex;
                _isVisible = true;
                _showFrame = Time.frameCount;

                Debug.Log($"[ContextMenu] Showing menu for player: {playerName} (ObjectId={nob.ObjectId})");
            }
        }

        private void PositionPopup(Vector2 screenPos) {
            if (_popup == null) return;

            float x = screenPos.x;
            float y = Screen.height - screenPos.y;

            float popupWidth = 140f;
            float popupHeight = 80f;
            float padding = 10f;

            if (x + popupWidth > Screen.width - padding) x = Screen.width - popupWidth - padding;
            if (y + popupHeight > Screen.height - padding) y = Screen.height - popupHeight - padding;
            if (x < padding) x = padding;
            if (y < padding) y = padding;

            _popup.style.left = x;
            _popup.style.top = y;
        }

        private void OnTradeClicked() {
            if (_tradeManager == null || _targetPlayer == null) {
                Debug.LogWarning($"[ContextMenu] OnTradeClicked FAILED: tradeManager={_tradeManager != null}, targetPlayer={_targetPlayer != null}");
                return;
            }
            Debug.Log($"[ContextMenu] Requesting trade with ObjectId={_targetPlayer.ObjectId}");
            _tradeManager.CmdRequestTrade(_targetPlayer.ObjectId);
            Hide();
        }

        private void Hide() {
            if (_popup != null) _popup.style.display = DisplayStyle.None;
            _isVisible = false;
            _targetPlayer = null;
        }
    }
}

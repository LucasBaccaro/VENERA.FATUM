using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using Genesis.Core.Networking;

namespace Genesis.Presentation.UI {

    /// <summary>
    /// Controls the Login UI: validates player name, stores selection in LoginData,
    /// then triggers network connection via NetworkBootstrap.
    /// </summary>
    public class LoginController : MonoBehaviour {

        /// <summary>
        /// True while the login screen is visible. Other systems should
        /// check this to suppress keyboard shortcuts.
        /// </summary>
        public static bool IsActive { get; private set; }

        [SerializeField] private UIDocument uiDocument;

        private VisualElement _root;
        private TextField _nameInput;
        private Button _mageButton;
        private Button _warriorButton;
        private Button _connectButton;
        private Label _statusLabel;

        private int _selectedClassIndex = 0;

        void Awake() {
            // Signal NetworkBootstrap to wait BEFORE its Start() runs
            LoginData.LoginRequired = true;
        }

        void Start() {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (uiDocument == null) {
                Debug.LogError("[LoginController] No UIDocument found!");
                return;
            }

            // Ensure login renders on top of all other UI
            uiDocument.sortingOrder = 100;

            _root = uiDocument.rootVisualElement;
            if (_root == null) return;

            _nameInput = _root.Q<TextField>("NameInput");
            _mageButton = _root.Q<Button>("MageButton");
            _warriorButton = _root.Q<Button>("WarriorButton");
            _connectButton = _root.Q<Button>("ConnectButton");
            _statusLabel = _root.Q<Label>("StatusLabel");

            _mageButton?.RegisterCallback<ClickEvent>(_ => SelectClass(0));
            _warriorButton?.RegisterCallback<ClickEvent>(_ => SelectClass(1));
            _connectButton?.RegisterCallback<ClickEvent>(_ => OnConnectClicked());

            // Default selection
            SelectClass(0);
            IsActive = true;

            Debug.Log("[LoginController] Login screen initialized.");
        }

        private void SelectClass(int index) {
            _selectedClassIndex = index;

            if (_mageButton != null && _warriorButton != null) {
                SetButtonSelected(_mageButton, index == 0);
                SetButtonSelected(_warriorButton, index == 1);
            }
        }

        private void SetButtonSelected(Button button, bool selected) {
            button.EnableInClassList("class-button--selected", selected);
            button.EnableInClassList("class-button--unselected", !selected);
        }

        private void OnConnectClicked() {
            string playerName = _nameInput?.value?.Trim() ?? "";

            if (playerName.Length < 2) {
                if (_statusLabel != null)
                    _statusLabel.text = "Name must be at least 2 characters.";
                return;
            }

            // Store in static DTO
            LoginData.PlayerName = playerName;
            LoginData.ClassIndex = _selectedClassIndex;
            LoginData.IsSet = true;

            // Disable controls while connecting
            _connectButton?.SetEnabled(false);
            _nameInput?.SetEnabled(false);
            _mageButton?.SetEnabled(false);
            _warriorButton?.SetEnabled(false);

            if (_statusLabel != null)
                _statusLabel.text = "Connecting...";

            // Find NetworkBootstrap and start connection
            var bootstrap = Object.FindFirstObjectByType<NetworkBootstrap>();
            if (bootstrap == null) {
                Debug.LogError("[LoginController] NetworkBootstrap not found in scene!");
                if (_statusLabel != null)
                    _statusLabel.text = "Error: NetworkBootstrap not found.";
                ReEnableControls();
                return;
            }

#if UNITY_EDITOR
            // ParrelSync clone → client only, main editor → host
            bool isClone = System.IO.File.Exists(
                System.IO.Path.Combine(Application.dataPath.Replace("/Assets", ""), ".clone"));
            if (isClone) {
                bootstrap.StartClientLocal();
            } else {
                bootstrap.StartHost();
            }
#else
            bootstrap.StartClient();
#endif

            Debug.Log($"[LoginController] Connecting as '{playerName}', class index {_selectedClassIndex}");

            // Keep UI visible as overlay until player spawns and camera is ready
            StartCoroutine(WaitForPlayerSpawn());
        }

        private IEnumerator WaitForPlayerSpawn() {
            // Wait until the camera has a valid target (player spawned)
            while (LostArkCamera.Instance == null || LostArkCamera.Instance.target == null)
                yield return null;

            // Extra wait for chunks/visuals to finish loading
            yield return new WaitForSeconds(2f);

            // Camera is tracking the player, safe to hide login
            _root.style.display = DisplayStyle.None;
            IsActive = false;
            Debug.Log("[LoginController] Player spawned, hiding login screen.");
        }

        private void ReEnableControls() {
            _connectButton?.SetEnabled(true);
            _nameInput?.SetEnabled(true);
            _mageButton?.SetEnabled(true);
            _warriorButton?.SetEnabled(true);
        }

        void OnDestroy() {
            IsActive = false;
            LoginData.LoginRequired = false;
        }
    }
}

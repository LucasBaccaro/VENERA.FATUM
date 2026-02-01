using UnityEngine;
using UnityEngine.UIElements;

namespace Genesis.Presentation.UI {
    public class MicroMenuController : MonoBehaviour {
        [Header("UI")]
        [SerializeField] private UIDocument _uiDocument;

        private Button _btnBag;
        private Button _btnChar;
        private Button _btnMap;
        private Button _btnSetup;

        private void OnEnable() {
            if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) {
                Debug.LogWarning("[MicroMenuController] No UIDocument found.");
                return;
            }

            var root = _uiDocument.rootVisualElement;
            if (root == null) {
                Debug.LogWarning($"[MicroMenuController] [{gameObject.name}] UI Document has no root element. Check if VisualTreeAsset is assigned.");
                return;
            }

            _btnBag = root.Q<Button>("BtnBag");
            _btnChar = root.Q<Button>("BtnChar");
            _btnMap = root.Q<Button>("BtnMap");
            _btnSetup = root.Q<Button>("BtnSetup");

            if (_btnBag != null) _btnBag.clicked += () => OnBagClicked();
            if (_btnChar != null) _btnChar.clicked += () => OnCharClicked();
            if (_btnMap != null) _btnMap.clicked += () => OnMapClicked();
            if (_btnSetup != null) _btnSetup.clicked += () => OnSetupClicked();

            Debug.Log($"[MicroMenuController] [{gameObject.name}] UI Initialized successfully.");
        }

        private void OnBagClicked() {
            Debug.Log("[MicroMenu] Bag Clicked");
        }

        private void OnCharClicked() {
            Debug.Log("[MicroMenu] Char Clicked");
        }

        private void OnMapClicked() {
            Debug.Log("[MicroMenu] Map Clicked");
        }

        private void OnSetupClicked() {
            Debug.Log("[MicroMenu] Setup Clicked");
        }
    }
}

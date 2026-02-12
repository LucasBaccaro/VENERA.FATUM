using UnityEngine;
using UnityEngine.UIElements;
using Genesis.Core;

namespace Genesis.Presentation.UI {

    public class InteractionPromptController : MonoBehaviour {

        [SerializeField] private UIDocument _uiDocument;

        private Label _promptLabel;
        private VisualElement _promptContainer;

        private void Awake() {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable() {
            EventBus.Subscribe<string>("OnInteractionPromptChanged", OnPromptChanged);
        }

        private void OnDisable() {
            EventBus.Unsubscribe<string>("OnInteractionPromptChanged", OnPromptChanged);
        }

        private void Start() {
            InitializeUI();
        }

        private void InitializeUI() {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;

            root.pickingMode = PickingMode.Ignore;
            _promptContainer = root.Q<VisualElement>("InteractionPromptContainer");
            _promptLabel = root.Q<Label>("InteractionPromptLabel");

            // Create UI elements if they don't exist in the UXML
            if (_promptContainer == null) {
                _promptContainer = new VisualElement();
                _promptContainer.name = "InteractionPromptContainer";
                _promptContainer.style.position = Position.Absolute;
                _promptContainer.style.bottom = 180;
                _promptContainer.style.left = Length.Percent(50);
                _promptContainer.style.translate = new Translate(Length.Percent(-50), 0);
                _promptContainer.style.backgroundColor = new Color(0, 0, 0, 0.7f);
                _promptContainer.style.borderTopLeftRadius = 6;
                _promptContainer.style.borderTopRightRadius = 6;
                _promptContainer.style.borderBottomLeftRadius = 6;
                _promptContainer.style.borderBottomRightRadius = 6;
                _promptContainer.style.paddingTop = 8;
                _promptContainer.style.paddingBottom = 8;
                _promptContainer.style.paddingLeft = 16;
                _promptContainer.style.paddingRight = 16;
                _promptContainer.style.display = DisplayStyle.None;

                _promptLabel = new Label("Press E");
                _promptLabel.name = "InteractionPromptLabel";
                _promptLabel.style.color = Color.white;
                _promptLabel.style.fontSize = 14;
                _promptLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _promptLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

                _promptContainer.Add(_promptLabel);
                root.Add(_promptContainer);
            }

            _promptContainer.style.display = DisplayStyle.None;
        }

        private void OnPromptChanged(string prompt) {
            if (_promptContainer == null) return;

            if (string.IsNullOrEmpty(prompt)) {
                _promptContainer.style.display = DisplayStyle.None;
            } else {
                _promptLabel.text = $"[E] {prompt}";
                _promptContainer.style.display = DisplayStyle.Flex;
            }
        }
    }
}

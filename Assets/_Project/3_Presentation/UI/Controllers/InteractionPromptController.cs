using UnityEngine;
using UnityEngine.UIElements;
using Genesis.Core;

namespace Genesis.Presentation.UI {

    public class InteractionPromptController : MonoBehaviour {

        [SerializeField] private UIDocument _uiDocument;

        private Label _promptLabel;
        private VisualElement _promptContainer;
        private GameObject _currentTarget;
        private Camera _mainCamera;

        private void Awake() {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();
            _mainCamera = Camera.main;
        }

        private void OnEnable() {
            EventBus.Subscribe<string>("OnInteractionPromptChanged", OnPromptChanged);
            EventBus.Subscribe<GameObject>("OnNearestInteractableChanged", OnTargetChanged);
        }

        private void OnDisable() {
            EventBus.Unsubscribe<string>("OnInteractionPromptChanged", OnPromptChanged);
            EventBus.Unsubscribe<GameObject>("OnNearestInteractableChanged", OnTargetChanged);
        }

        private void Start() {
            InitializeUI();
        }

        private void Update() {
            if (_currentTarget == null || _promptContainer == null || _promptContainer.style.display == DisplayStyle.None)
                return;

            UpdatePosition();
        }

        private void UpdatePosition() {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            // Use the target's pivot (feet)
            Vector3 worldPos = _currentTarget.transform.position;
            
            // Convert world to panel space
            Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                _promptContainer.panel, 
                worldPos, 
                _mainCamera
            );

            // Update container style to be centered on the feet/pivot
            _promptContainer.style.left = panelPos.x;
            _promptContainer.style.top = panelPos.y; 
            
            // Reset fixed positioning from UXML
            _promptContainer.style.bottom = StyleKeyword.Null;
        }

        private void InitializeUI() {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;

            root.pickingMode = PickingMode.Ignore;
            _promptContainer = root.Q<VisualElement>("InteractionPromptContainer");
            _promptLabel = root.Q<Label>("InteractionPromptLabel");

            if (_promptContainer != null) {
                _promptContainer.style.position = Position.Absolute;
                // Center horizontally (-50%) and start vertically from the point (0%)
                // If the pivot is at the center of the object, this will put it at its "feet".
                _promptContainer.style.translate = new Translate(Length.Percent(-50), 0);
            }

            _promptContainer.style.display = DisplayStyle.None;
        }

        private void OnTargetChanged(GameObject target) {
            _currentTarget = target;
            if (_currentTarget != null) {
                UpdatePosition();
            }
        }

        private void OnPromptChanged(string prompt) {
            if (_promptContainer == null) return;

            if (string.IsNullOrEmpty(prompt)) {
                _promptContainer.style.display = DisplayStyle.None;
            } else {
                _promptLabel.text = $"[E] {prompt}";
                _promptContainer.style.display = DisplayStyle.Flex;
                UpdatePosition();
            }
        }
    }
}

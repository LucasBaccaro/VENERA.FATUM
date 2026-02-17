using UnityEngine;
using UnityEngine.UIElements;
using Genesis.Simulation;

namespace Genesis.Presentation.UI
{
    public class MinimapController : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _root;
        private VisualElement _minimapTexture;
        private VisualElement _playerIcon;
        private Button _btnZoomIn;
        private Button _btnZoomOut;

        private Transform _playerTransform;

        void Start()
        {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();

            InitializeUI();
        }

        void OnDestroy()
        {
            if (_btnZoomIn != null) _btnZoomIn.clicked -= OnZoomIn;
            if (_btnZoomOut != null) _btnZoomOut.clicked -= OnZoomOut;
        }

        void Update()
        {
            UpdatePlayerIcon();
            TryBindRenderTexture();
        }

        public void SetTarget(Transform target)
        {
            _playerTransform = target;
        }

        private void InitializeUI()
        {
            _root = _uiDocument.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;

            _minimapTexture = _root.Q<VisualElement>("MinimapTexture");
            _playerIcon = _root.Q<VisualElement>("MinimapPlayerIcon");
            _btnZoomIn = _root.Q<Button>("BtnZoomIn");
            _btnZoomOut = _root.Q<Button>("BtnZoomOut");

            if (_btnZoomIn != null) _btnZoomIn.clicked += OnZoomIn;
            if (_btnZoomOut != null) _btnZoomOut.clicked += OnZoomOut;
        }

        private bool _textureBound;

        private void TryBindRenderTexture()
        {
            if (_textureBound || _minimapTexture == null) return;

            if (MinimapCamera.Instance != null && MinimapCamera.Instance.RenderTexture != null)
            {
                _minimapTexture.style.backgroundImage = Background.FromRenderTexture(MinimapCamera.Instance.RenderTexture);
                _textureBound = true;
            }
        }

        private void UpdatePlayerIcon()
        {
            if (_playerIcon == null || _playerTransform == null) return;

            float yaw = _playerTransform.eulerAngles.y;
            _playerIcon.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(yaw)));
        }

        private void OnZoomIn()
        {
            if (MinimapCamera.Instance != null)
                MinimapCamera.Instance.ZoomIn();
        }

        private void OnZoomOut()
        {
            if (MinimapCamera.Instance != null)
                MinimapCamera.Instance.ZoomOut();
        }
    }
}

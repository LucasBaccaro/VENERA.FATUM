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
        private VisualElement _minimapContainer;

        private Transform _playerTransform;

        void Start()
        {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();

            InitializeUI();
        }

        void OnDestroy()
        {
            if (_minimapContainer != null)
                _minimapContainer.UnregisterCallback<WheelEvent>(OnWheelZoom);
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
            
            _minimapContainer = _root.Q<VisualElement>("MinimapContainer");
            _minimapTexture = _root.Q<VisualElement>("MinimapTexture");
            _playerIcon = _root.Q<VisualElement>("MinimapPlayerIcon");

            if (_minimapContainer != null)
            {
                _minimapContainer.RegisterCallback<WheelEvent>(OnWheelZoom);
            }
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

        private void OnWheelZoom(WheelEvent evt)
        {
            if (MinimapCamera.Instance == null) return;
            
            if (evt.delta.y < 0)
                MinimapCamera.Instance.ZoomIn();
            else if (evt.delta.y > 0)
                MinimapCamera.Instance.ZoomOut();
                
            evt.StopPropagation();
        }
    }
}

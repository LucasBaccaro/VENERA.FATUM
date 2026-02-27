using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Genesis.Simulation
{
    public class MinimapCamera : MonoBehaviour
    {
        public static MinimapCamera Instance { get; private set; }

        [Header("Follow")]
        [SerializeField] private Transform _target;
        [SerializeField] private float _height = 50f;

        [Header("Zoom")]
        [SerializeField] private float _minOrthoSize = 15f;
        [SerializeField] private float _maxOrthoSize = 60f;
        [SerializeField] private float _zoomStep = 5f;

        [Header("Render Texture")]
        [SerializeField] private int _textureSize = 512;

        [Header("Performance")]
        [SerializeField] private int _renderInterval = 5;

        private Camera _camera;
        private RenderTexture _renderTexture;
        private int _frameCounter;
        private Shader _unlitShader;

        public RenderTexture RenderTexture => _renderTexture;

        void Awake()
        {
            Instance = this;
            _unlitShader = Shader.Find("Unlit/Texture");
            CreateRenderTexture();
            CreateCamera();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }

            if (_camera != null)
                Destroy(_camera.gameObject);
        }

        void LateUpdate()
        {
            if (_target == null || _camera == null) return;

            Vector3 pos = _target.position;
            _camera.transform.position = new Vector3(pos.x, pos.y + _height, pos.z);

            _frameCounter++;
            if (_frameCounter >= _renderInterval)
            {
                _frameCounter = 0;
                if (_unlitShader != null)
                    _camera.RenderWithShader(_unlitShader, "");
                else
                    _camera.Render();
            }
        }

        public void SetTarget(Transform target)
        {
            _target = target;

            if (target != null && _camera != null)
            {
                Vector3 pos = target.position;
                _camera.transform.position = new Vector3(pos.x, pos.y + _height, pos.z);
            }
        }

        public void ZoomIn()
        {
            if (_camera == null) return;
            _camera.orthographicSize = Mathf.Max(_minOrthoSize, _camera.orthographicSize - _zoomStep);
        }

        public void ZoomOut()
        {
            if (_camera == null) return;
            _camera.orthographicSize = Mathf.Min(_maxOrthoSize, _camera.orthographicSize + _zoomStep);
        }

        private void CreateRenderTexture()
        {
            _renderTexture = new RenderTexture(_textureSize, _textureSize, 16);
            _renderTexture.antiAliasing = 4;
            _renderTexture.filterMode = FilterMode.Bilinear;
            _renderTexture.name = "MinimapRT";
            _renderTexture.Create();
        }

        private void CreateCamera()
        {
            GameObject camGO = new GameObject("MinimapCamera");
            camGO.transform.SetParent(transform);
            camGO.transform.localPosition = new Vector3(0f, _height, 0f);
            camGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            _camera = camGO.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = (_minOrthoSize + _maxOrthoSize) * 0.5f;
            _camera.targetTexture = _renderTexture;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.1f, 0.12f, 0.1f, 1f);
            _camera.depth = -10;

            // Disable shadows and post-processing via URP camera data
            var urpData = camGO.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderShadows = false;
            urpData.renderPostProcessing = false;

            // Disable automatic rendering — we render manually every N frames
            _camera.enabled = false;

            // Culling mask: only Default + Ground + Environment
            _camera.cullingMask =
                (1 << LayerMask.NameToLayer("Default")) |
                (1 << LayerMask.NameToLayer("Ground")) |
                (1 << LayerMask.NameToLayer("Environment"));
        }
    }
}

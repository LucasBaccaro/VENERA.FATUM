using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using Genesis.Core;

namespace Genesis.Simulation {

    public class EnemyHealthBar : MonoBehaviour {

        [Header("Settings")]
        [SerializeField] private Vector3 _offset = new Vector3(0, 2.2f, 0);
        [SerializeField] private float _hideDelay = 3f;
        [SerializeField] private Vector2 _barSize = new Vector2(1f, 0.12f);

        private EnemyMob _mob;
        private Transform _cameraTransform;
        private float _lastDamageTime;
        private bool _isVisible;

        private Canvas _canvas;
        private Image _fillImage;
        private GameObject _canvasGO;

        private void Awake() {
            _mob = GetComponentInParent<EnemyMob>();
            if (_mob == null) _mob = GetComponent<EnemyMob>();

            CreateHealthBarUI();
        }

        private void CreateHealthBarUI() {
            // Create canvas GO as child
            _canvasGO = new GameObject("HealthBarCanvas");
            _canvasGO.transform.SetParent(transform, false);
            _canvasGO.transform.localPosition = _offset;

            // World-space canvas
            _canvas = _canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 100;

            var scaler = _canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100;

            var rt = _canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = _barSize;
            rt.localScale = Vector3.one;

            // Background (dark)
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(_canvasGO.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            var bgRt = bgGO.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Fill (red)
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(_canvasGO.transform, false);
            _fillImage = fillGO.AddComponent<Image>();
            _fillImage.color = new Color(0.85f, 0.15f, 0.15f, 1f);
            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Horizontal;
            _fillImage.fillAmount = 1f;
            var fillRt = fillGO.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            _canvasGO.SetActive(false);
        }

        private void Start() {
            if (Camera.main != null) {
                _cameraTransform = Camera.main.transform;
            }
        }

        private void OnEnable() {
            EventBus.Subscribe<NetworkObject, float, float>("OnEnemyHealthChanged", OnHealthChanged);
        }

        private void OnDisable() {
            EventBus.Unsubscribe<NetworkObject, float, float>("OnEnemyHealthChanged", OnHealthChanged);
        }

        private void OnHealthChanged(NetworkObject enemy, float current, float max) {
            if (_mob == null) return;

            var mobNob = _mob.GetComponent<NetworkObject>();
            if (mobNob == null || mobNob != enemy) return;

            float ratio = max > 0 ? current / max : 0f;

            if (_fillImage != null)
                _fillImage.fillAmount = ratio;

            _lastDamageTime = Time.time;

            if (!_isVisible && current < max) {
                Show();
            }

            if (current <= 0) {
                Hide();
            }
        }

        private void LateUpdate() {
            if (!_isVisible) return;

            if (_cameraTransform == null && Camera.main != null) {
                _cameraTransform = Camera.main.transform;
            }

            if (_cameraTransform != null && _canvasGO != null) {
                _canvasGO.transform.position = transform.position + _offset;
                _canvasGO.transform.LookAt(
                    _canvasGO.transform.position + _cameraTransform.forward
                );
            }

            if (Time.time - _lastDamageTime > _hideDelay) {
                Hide();
            }
        }

        private void Show() {
            _isVisible = true;
            if (_canvasGO != null) _canvasGO.SetActive(true);
        }

        private void Hide() {
            _isVisible = false;
            if (_canvasGO != null) _canvasGO.SetActive(false);
        }
    }
}

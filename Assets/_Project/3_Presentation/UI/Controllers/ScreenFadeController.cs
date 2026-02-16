using UnityEngine;
using UnityEngine.UIElements;
using Genesis.Core;
using System.Collections;

namespace Genesis.Presentation.UI
{
    public class ScreenFadeController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        private VisualElement _overlay;
        private Coroutine _fadeCoroutine;

        // ═══════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════

        private void Start()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (uiDocument == null)
            {
                Debug.LogError("[ScreenFadeController] No UIDocument found!");
                return;
            }

            InitializeOverlay();
        }

        private void InitializeOverlay()
        {
            var root = uiDocument.rootVisualElement;
            if (root == null) return;

            _overlay = new VisualElement();
            _overlay.name = "FadeOverlay";
            _overlay.style.position = Position.Absolute;
            _overlay.style.top = 0;
            _overlay.style.bottom = 0;
            _overlay.style.left = 0;
            _overlay.style.right = 0;
            _overlay.style.backgroundColor = Color.black;
            _overlay.style.opacity = 0f;
            _overlay.pickingMode = PickingMode.Ignore;

            root.Add(_overlay);
        }

        // ═══════════════════════════════════════════════════════
        // EVENT SUBSCRIPTIONS
        // ═══════════════════════════════════════════════════════

        private void OnEnable()
        {
            EventBus.Subscribe<float>(PortalEvents.TELEPORT_FADE_OUT, OnFadeOut);
            EventBus.Subscribe<float>(PortalEvents.TELEPORT_FADE_IN, OnFadeIn);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<float>(PortalEvents.TELEPORT_FADE_OUT, OnFadeOut);
            EventBus.Unsubscribe<float>(PortalEvents.TELEPORT_FADE_IN, OnFadeIn);
        }

        // ═══════════════════════════════════════════════════════
        // FADE LOGIC
        // ═══════════════════════════════════════════════════════

        private void OnFadeOut(float duration)
        {
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeCoroutine(0f, 1f, duration));
        }

        private void OnFadeIn(float duration)
        {
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeCoroutine(1f, 0f, duration));
        }

        private IEnumerator FadeCoroutine(float from, float to, float duration)
        {
            if (_overlay == null) yield break;

            // Block input during black screen
            _overlay.pickingMode = to > 0f ? PickingMode.Position : PickingMode.Ignore;
            _overlay.style.opacity = from;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _overlay.style.opacity = Mathf.Lerp(from, to, t);
                yield return null;
            }

            _overlay.style.opacity = to;

            // Unblock input when fully transparent
            if (to <= 0f)
            {
                _overlay.pickingMode = PickingMode.Ignore;
            }

            _fadeCoroutine = null;
        }
    }
}
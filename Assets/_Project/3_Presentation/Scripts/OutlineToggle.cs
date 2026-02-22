using UnityEngine;

namespace Genesis.Presentation.Feedback
{
    /// <summary>
    /// Component to be added to interactable objects (Lootbags, Chests, etc.).
    /// Toggles the object's layer to match the Outline Renderer Feature's mask.
    /// Uses a reference count so multiple sources (proximity + hover) can each
    /// enable/disable the outline independently without conflicting.
    /// </summary>
    public class OutlineToggle : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private LayerMask _outlineLayer;
        [SerializeField] private bool _outlineOnStart = false;

        private int _originalLayer;
        private int _targetLayerIndex;

        // Reference count: outline stays ON as long as count > 0
        private int _activeCount;

        private void Awake()
        {
            _originalLayer = gameObject.layer;

            int mask = _outlineLayer.value;
            for (int i = 0; i < 32; i++)
            {
                if (((mask >> i) & 1) == 1)
                {
                    _targetLayerIndex = i;
                    break;
                }
            }

            if (_outlineOnStart) SetOutline(true);
        }

        /// <summary>
        /// Enable or disable the outline. Safe to call from multiple sources
        /// (mouse hover, proximity, quest markers, etc.) — outline stays on
        /// as long as at least one source has requested it.
        /// </summary>
        public void SetOutline(bool enable)
        {
            int prev = _activeCount;
            _activeCount = Mathf.Max(0, enable ? _activeCount + 1 : _activeCount - 1);

            bool wasOn = prev > 0;
            bool isOn  = _activeCount > 0;

            if (wasOn != isOn)
            {
                int targetLayer = isOn ? _targetLayerIndex : _originalLayer;
                Debug.Log($"<color=cyan>[OutlineToggle] {gameObject.name} -> Layer: {targetLayer} (refs: {_activeCount})</color>");
                SetLayerRecursive(transform, targetLayer);
            }
        }

        /// <summary>
        /// Force the outline off regardless of active sources (e.g. object destroyed).
        /// </summary>
        public void ForceOutlineOff()
        {
            _activeCount = 0;
            SetLayerRecursive(transform, _originalLayer);
        }

        private void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
                SetLayerRecursive(child, layer);
        }
    }
}

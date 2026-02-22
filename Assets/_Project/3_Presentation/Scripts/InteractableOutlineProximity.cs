using UnityEngine;
using Genesis.Core;

namespace Genesis.Presentation.Feedback
{
    /// <summary>
    /// Listens to the InteractionDetector's proximity scan and applies/removes
    /// the outline on the nearest interactable. Attach to the local player prefab
    /// alongside (or near) the InteractionDetector.
    /// </summary>
    public class InteractableOutlineProximity : MonoBehaviour
    {
        private OutlineToggle _currentOutline;

        private void OnEnable()
        {
            EventBus.Subscribe<GameObject>("OnNearestInteractableChanged", OnNearestChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameObject>("OnNearestInteractableChanged", OnNearestChanged);

            // Make sure we clean up any active outline when this component disables
            if (_currentOutline != null)
            {
                _currentOutline.SetOutline(false);
                _currentOutline = null;
            }
        }

        private void OnNearestChanged(GameObject nearestGO)
        {
            // Disable outline on the previous nearest
            if (_currentOutline != null)
            {
                _currentOutline.SetOutline(false);
                _currentOutline = null;
            }

            // Enable outline on the new nearest (null means player left range)
            if (nearestGO != null)
            {
                _currentOutline = nearestGO.GetComponentInParent<OutlineToggle>();
                if (_currentOutline == null)
                    _currentOutline = nearestGO.GetComponentInChildren<OutlineToggle>();

                if (_currentOutline != null)
                    _currentOutline.SetOutline(true);
            }
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

namespace Genesis.Presentation.Feedback
{
    [RequireComponent(typeof(OutlineToggle))]
    public class InteractableOutlineHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private OutlineToggle _outline;

        private void Awake()
        {
            _outline = GetComponent<OutlineToggle>();
        }

        private void Start()
        {
            Debug.Log($"[InteractableOutlineHover] Initialized on {gameObject.name}");
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log($"<color=green>[InteractableOutlineHover] Pointer Entered: {gameObject.name}</color>");
            if (_outline != null) _outline.SetOutline(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Debug.Log($"<color=orange>[InteractableOutlineHover] Pointer Exited: {gameObject.name}</color>");
            if (_outline != null) _outline.SetOutline(false);
        }
    }
}

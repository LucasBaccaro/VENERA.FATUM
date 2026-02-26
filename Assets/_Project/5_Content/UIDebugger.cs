using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class UIDebugger : MonoBehaviour
{
    private List<UIDocument> _uiDocuments;

    void Start()
    {
        _uiDocuments = new List<UIDocument>(FindObjectsByType<UIDocument>(FindObjectsSortMode.None));
        Debug.Log($"<color=cyan>[UIDebugger]</color> Initialized. Found {_uiDocuments.Count} UIDocuments.");
    }

    void Update()
    {
        if (Pointer.current == null || Keyboard.current == null) return;

        if (Pointer.current.press.wasPressedThisFrame && Keyboard.current.leftCtrlKey.isPressed)
        {
            Vector2 mousePos = Pointer.current.position.ReadValue();
            mousePos.y = Screen.height - mousePos.y; 

            bool found = false;
            foreach (var doc in _uiDocuments)
            {
                if (doc == null || doc.rootVisualElement == null) continue;

                VisualElement picked = doc.rootVisualElement.panel.Pick(mousePos);
                if (picked != null && picked != doc.rootVisualElement)
                {
                    bool isBlocking = picked.pickingMode != PickingMode.Ignore;
                    Debug.Log($"<color=cyan>[UIDebugger]</color> Element: <b>{picked.name}</b> | Document: <b>{doc.gameObject.name}</b> | Layer: <b>{doc.gameObject.layer} ({LayerMask.LayerToName(doc.gameObject.layer)})</b> | Blocking: <b>{isBlocking}</b>");
                    found = true;
                }
            }

            if (!found)
            {
                Debug.Log("<color=yellow>[UIDebugger]</color> No UI Toolkit element detected under mouse.");
            }

            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                eventData.position = Pointer.current.position.ReadValue();
                var results = new List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);

                if (results.Count > 0)
                {
                    Debug.Log($"<color=orange>[UIDebugger]</color> EventSystem hits: {results.Count} objects.");
                    foreach (var res in results)
                    {
                        Debug.Log($"   - Hit: <b>{res.gameObject.name}</b> (Layer: {LayerMask.LayerToName(res.gameObject.layer)})");
                    }
                }
                else
                {
                    Debug.Log("<color=orange>[UIDebugger]</color> EventSystem raycast hit nothing.");
                }
            }
        }
    }
}

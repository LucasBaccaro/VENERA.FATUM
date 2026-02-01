#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Genesis.Editor {
    public class UIDocumentHelper : EditorWindow {
        [MenuItem("Tools/Genesis/Fix UI Documents")]
        public static void FixUIDocuments() {
            var allDocs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            int count = 0;

            foreach (var doc in allDocs) {
                // Force a dirty state to trigger re-registration
                EditorUtility.SetDirty(doc);
                
                // Toggle enabled state to force internal Disable/Enable cycles
                bool wasEnabled = doc.enabled;
                doc.enabled = false;
                doc.enabled = wasEnabled;
                
                Debug.Log($"[UIDocumentHelper] Reset tracking for: {doc.gameObject.name}");
                count++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[UIDocumentHelper] Successfully processed {count} UI Documents. Please try live reloading now.");
        }
    }
}
#endif

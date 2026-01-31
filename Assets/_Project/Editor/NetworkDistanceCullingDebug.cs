using UnityEngine;
using UnityEditor;
using Genesis.Core.Networking;

namespace Genesis.Editor
{
    /// <summary>
    /// Debug visualization for NetworkDistanceCulling in Scene View.
    /// Shows distance spheres and connection lines.
    /// </summary>
    [CustomEditor(typeof(NetworkDistanceCulling))]
    public class NetworkDistanceCullingDebug : UnityEditor.Editor
    {
        private static bool _showDistanceSpheres = true;
        private static bool _showLabels = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Debug Visualization", EditorStyles.boldLabel);

            _showDistanceSpheres = EditorGUILayout.Toggle("Show Distance Sphere", _showDistanceSpheres);
            _showLabels = EditorGUILayout.Toggle("Show Distance Label", _showLabels);

            NetworkDistanceCulling culling = (NetworkDistanceCulling)target;

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "In Play Mode, this object will only replicate to clients within the configured distance.\n\n" +
                "The green sphere shows the visibility range in Scene View.",
                MessageType.Info
            );
        }

        private void OnSceneGUI()
        {
            if (!_showDistanceSpheres) return;

            NetworkDistanceCulling culling = (NetworkDistanceCulling)target;
            if (culling == null) return;

            // Get distance from profile or custom setting
            SerializedObject so = new SerializedObject(culling);
            SerializedProperty profileProp = so.FindProperty("profile");
            SerializedProperty useCustomProp = so.FindProperty("useCustomDistance");
            SerializedProperty customDistProp = so.FindProperty("customDistance");

            float distance = 100f; // default

            if (useCustomProp.boolValue)
            {
                distance = customDistProp.floatValue;
            }
            else if (profileProp.objectReferenceValue != null)
            {
                SerializedObject profileSO = new SerializedObject(profileProp.objectReferenceValue);
                SerializedProperty maxDistProp = profileSO.FindProperty("maxDistance");
                distance = maxDistProp.floatValue;
            }

            Vector3 position = culling.transform.position;

            // Draw distance sphere
            Handles.color = new Color(0, 1, 0, 0.1f);
            Handles.DrawSolidDisc(position, Vector3.up, distance);

            Handles.color = new Color(0, 1, 0, 0.5f);
            Handles.DrawWireDisc(position, Vector3.up, distance);

            // Draw vertical circle for 3D reference
            Handles.color = new Color(0, 1, 0, 0.3f);
            Handles.DrawWireDisc(position, Vector3.right, distance);
            Handles.DrawWireDisc(position, Vector3.forward, distance);

            // Draw label
            if (_showLabels)
            {
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.normal.textColor = Color.green;
                style.fontStyle = FontStyle.Bold;
                style.fontSize = 12;

                Vector3 labelPos = position + Vector3.up * 2f;
                Handles.Label(labelPos, $"Visibility Range: {distance}m", style);
            }

            // Draw distance to other NetworkDistanceCulling objects (in Play Mode)
            if (Application.isPlaying)
            {
                var allCulling = FindObjectsOfType<NetworkDistanceCulling>();
                foreach (var other in allCulling)
                {
                    if (other == culling) continue;

                    float dist = Vector3.Distance(position, other.transform.position);
                    bool isVisible = dist <= distance;

                    Handles.color = isVisible ? new Color(0, 1, 0, 0.3f) : new Color(1, 0, 0, 0.3f);
                    Handles.DrawDottedLine(position, other.transform.position, 3f);

                    // Draw distance text at midpoint
                    if (_showLabels)
                    {
                        Vector3 midpoint = (position + other.transform.position) / 2f;
                        GUIStyle distStyle = new GUIStyle(GUI.skin.label);
                        distStyle.normal.textColor = isVisible ? Color.green : Color.red;
                        distStyle.fontSize = 10;

                        Handles.Label(midpoint, $"{dist:F1}m {(isVisible ? "✓" : "✗")}", distStyle);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Scene view overlay showing all NetworkDistanceCulling objects
    /// </summary>
    public static class NetworkDistanceCullingSceneOverlay
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void DrawGizmo(NetworkDistanceCulling culling, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;

            // Small sphere at object position
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Gizmos.DrawSphere(culling.transform.position, 0.5f);
        }
    }
}

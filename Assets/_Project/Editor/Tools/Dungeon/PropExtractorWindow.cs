using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace Genesis.Editor.Dungeon
{
    /// <summary>
    /// Extracts each mesh inside an FBX as an individual prop prefab.
    /// Registers the FBX so that PropFBXPostprocessor auto-updates prefabs on reimport.
    /// </summary>
    public class PropExtractorWindow : EditorWindow
    {
        private GameObject _sourceFBX;
        private string _targetPath = "Assets/_Project/5_Content/Prefabs/Dungeon/Props";
        private string _propLayer = "Environment";
        private bool _addBoxCollider = false;
        private Material _overrideMaterial;

        [MenuItem("Genesis/Dungeon/Prop Extractor")]
        public static void ShowWindow()
        {
            GetWindow<PropExtractorWindow>("Prop Extractor");
        }

        private void OnGUI()
        {
            GUILayout.Label("Dungeon Prop Extractor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Each mesh inside the FBX will become its own prefab.\n" +
                "The FBX is registered so prefabs auto-update on reimport.",
                MessageType.Info);

            GUILayout.Space(6);

            _sourceFBX = (GameObject)EditorGUILayout.ObjectField("Source FBX", _sourceFBX, typeof(GameObject), false);
            _targetPath = EditorGUILayout.TextField("Output Path", _targetPath);
            _propLayer  = EditorGUILayout.TextField("Layer", _propLayer);
            _addBoxCollider = EditorGUILayout.Toggle("Add Box Collider", _addBoxCollider);

            GUILayout.Space(4);
            GUILayout.Label("Material", EditorStyles.boldLabel);
            _overrideMaterial = (Material)EditorGUILayout.ObjectField("Override Material", _overrideMaterial, typeof(Material), false);
            if (_overrideMaterial != null)
                EditorGUILayout.HelpBox("Este material reemplazará los del FBX en todos los prefabs extraídos.", MessageType.None);

            GUILayout.Space(8);

            using (new EditorGUI.DisabledScope(_sourceFBX == null))
            {
                if (GUILayout.Button("Extract Props"))
                {
                    string fbxPath = AssetDatabase.GetAssetPath(_sourceFBX);
                    if (string.IsNullOrEmpty(fbxPath))
                    {
                        EditorUtility.DisplayDialog("Error", "Could not find asset path for the selected FBX.", "OK");
                        return;
                    }
                    PropFBXPostprocessor.RegisterFBX(fbxPath, _targetPath, _propLayer, _addBoxCollider, _overrideMaterial);
                    PropExtractorCore.Extract(fbxPath, _targetPath, _propLayer, _addBoxCollider, _overrideMaterial);
                }

                GUILayout.Space(4);

                if (GUILayout.Button("Unregister FBX (stop auto-update)"))
                {
                    string fbxPath = AssetDatabase.GetAssetPath(_sourceFBX);
                    if (!string.IsNullOrEmpty(fbxPath))
                        PropFBXPostprocessor.UnregisterFBX(fbxPath);
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("Registered FBXs", EditorStyles.boldLabel);
            DrawRegisteredList();
        }

        private Vector2 _scroll;
        private void DrawRegisteredList()
        {
            var entries = PropFBXPostprocessor.GetRegisteredEntries();
            if (entries.Count == 0)
            {
                GUILayout.Label("  (none)", EditorStyles.miniLabel);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(120));
            foreach (var e in entries)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(Path.GetFileName(e.FbxPath), EditorStyles.miniLabel, GUILayout.Width(160));
                GUILayout.Label("→ " + e.OutputPath, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}

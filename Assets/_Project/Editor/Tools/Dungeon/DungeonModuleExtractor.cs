using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace Genesis.Editor.Dungeon
{
    public class DungeonModuleExtractor : EditorWindow
    {
        private GameObject _sourceFBX;
        private string _targetPath = "Assets/_Project/5_Content/Prefabs/Dungeon/Modules";

        [MenuItem("Genesis/Dungeon/Module Extractor")]
        public static void ShowWindow()
        {
            GetWindow<DungeonModuleExtractor>("Module Extractor");
        }

        private void OnGUI()
        {
            GUILayout.Label("Dungeon Module Extractor", EditorStyles.boldLabel);

            _sourceFBX = (GameObject)EditorGUILayout.ObjectField("Source FBX", _sourceFBX, typeof(GameObject), false);
            _targetPath = EditorGUILayout.TextField("Target Path", _targetPath);

            if (GUILayout.Button("Extract Modules"))
            {
                if (_sourceFBX == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please select a Source FBX.", "OK");
                    return;
                }
                ExtractModules();
            }
        }

        private void ExtractModules()
        {
            if (!Directory.Exists(_targetPath))
            {
                Directory.CreateDirectory(_targetPath);
                AssetDatabase.Refresh();
            }

            // Instantiate
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(_sourceFBX);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            try
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                int envLayer = LayerMask.NameToLayer("Environment");

                if (groundLayer == -1) Debug.LogWarning("Layer 'Ground' not found! Defaulting to 0.");
                if (envLayer == -1) Debug.LogWarning("Layer 'Environment' not found! Defaulting to 0.");

                // Find all renderers recursively
                MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                int processingCount = 0;

                // Process list to avoid hierarchy modification issues during iteration
                List<GameObject> modules = new List<GameObject>();
                foreach (var r in renderers) modules.Add(r.gameObject);

                foreach (GameObject moduleGO in modules)
                {
                    // 1. Detach and Normalize
                    moduleGO.transform.parent = null;
                    moduleGO.transform.position = Vector3.zero;
                    moduleGO.transform.rotation = Quaternion.identity;
                    // Retain original scale (e.g. 0.01) to match imported bounds
                    // moduleGO.transform.localScale = Vector3.one;

                    // 2. Add Collider
                    if (moduleGO.GetComponent<BoxCollider>() == null)
                    {
                        moduleGO.AddComponent<BoxCollider>();
                    }

                    // 3. Set Layer
                    bool isFloor = moduleGO.name.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (isFloor)
                    {
                        moduleGO.layer = groundLayer != -1 ? groundLayer : 0;
                    }
                    else
                    {
                        moduleGO.layer = envLayer != -1 ? envLayer : 0;
                    }

                    // 4. Save Logic
                    string localPath = $"{_targetPath}/{moduleGO.name}.prefab";

                    // Check if prefab exists
                    GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(localPath);
                    if (existingPrefab != null)
                    {
                         // Option A: Overwrite fully (Simple, destructive to scripts on prefab)
                         // But keeps the main mesh updated.
                         PrefabUtility.SaveAsPrefabAsset(moduleGO, localPath);
                         Debug.Log($"Updated existing prefab: {localPath}");
                    }
                    else
                    {
                        // Create new
                        // Ensure unique name only if we actually collide with something we don't want to overwrite? 
                        // No, we WANT to overwrite if name matches. 
                        // So we just save. 
                        // But what if the user has "Wall" and "Wall" in the FBX? Then we have a problem.
                        // The FBX meshes should be unique named.
                        
                        localPath = AssetDatabase.GenerateUniqueAssetPath(localPath);
                        PrefabUtility.SaveAsPrefabAsset(moduleGO, localPath);
                        processingCount++;
                    }
                }

                Debug.Log($"<color=green>Successfully extracted {processingCount} modules to {_targetPath}</color>");
            }
            finally
            {
                DestroyImmediate(instance);
            }
        }
    }
}

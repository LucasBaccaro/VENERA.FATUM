using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Genesis.Data.Dungeon;

namespace Genesis.Editor.Dungeon
{
    public class DungeonCreatorWindow : EditorWindow
    {
        // State
        private DungeonTheme _currentTheme;
        private ModuleType _selectedType = ModuleType.Floor;
        private ModuleCategory _selectedCategory;
        
        private bool _isPainting = false;
        private GameObject _ghostObject;
        private int _currentVariantIndex = 0;
        private float _rotationY = 0;

        // Settings
        private float _gridSize = 2.0f;
        private bool _autoColumns = true;
        private float _yOffset = 0f;
        
        // Debug & Preview
        private Material _customGhostMaterial;
        private bool _debugMode = false;

        // Modes
        private enum ToolMode { Structure, PropPainter }
        private ToolMode _currentMode = ToolMode.Structure;

        // Props settings
        private float _propDensity = 1.0f;
        private float _propRadius = 1.0f;
        private GameObject _selectedPropPrefab;

        [MenuItem("Genesis/Dungeon/Dungeon Creator")]
        public static void ShowWindow()
        {
            var window = GetWindow<DungeonCreatorWindow>("Dungeon Creator");
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            if (_ghostObject != null) DestroyImmediate(_ghostObject);
        }

        private void OnGUI()
        {
            GUILayout.Label("Dungeon Creator", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            _currentTheme = (DungeonTheme)EditorGUILayout.ObjectField("Theme", _currentTheme, typeof(DungeonTheme), false);
            if (EditorGUI.EndChangeCheck())
            {
                UpdateSelectedCategory();
            }

            _customGhostMaterial = (Material)EditorGUILayout.ObjectField("Preview Mat", _customGhostMaterial, typeof(Material), false);
            _debugMode = EditorGUILayout.Toggle("Debug Mode", _debugMode);

            if (_currentTheme == null) return;

            GUILayout.Space(10);
            _currentMode = (ToolMode)GUILayout.Toolbar((int)_currentMode, new string[] { "Structure", "Props" });

            GUILayout.Space(10);

            if (_currentMode == ToolMode.Structure)
            {
                DrawStructureGUI();
            }
            else
            {
                DrawPropsGUI();
            }

            GUILayout.Space(10);
            GUILayout.Label("Settings", EditorStyles.boldLabel);
            _gridSize = EditorGUILayout.FloatField("Grid Size", _gridSize);
            _yOffset = EditorGUILayout.FloatField("Y Offset", _yOffset);
            _autoColumns = EditorGUILayout.Toggle("Auto Columns", _autoColumns);

            GUILayout.FlexibleSpace();
            GUILayout.Label("Controls:", EditorStyles.miniBoldLabel);
            GUILayout.Label("Click: Place | Shift+Click: Delete", EditorStyles.miniLabel);
            GUILayout.Label("R: Rotate | V: Next Variant", EditorStyles.miniLabel);
        }

        private void DrawStructureGUI()
        {
            GUILayout.Label("Module Type", EditorStyles.label);
            
            EditorGUI.BeginChangeCheck();
            _selectedType = (ModuleType)EditorGUILayout.EnumPopup("Type", _selectedType);
            if (EditorGUI.EndChangeCheck())
            {
                UpdateSelectedCategory();
                _currentVariantIndex = 0;
                UpdateGhost(Vector3.zero); // Helper call to reset if needed
            }

            if (_selectedCategory == null)
            {
                EditorGUILayout.HelpBox($"No category found for {_selectedType} in current Theme.", MessageType.Warning);
            }
            else
            {
                GUILayout.Label($"Variants: {_selectedCategory.Variants.Length}");
            }
        }

        private void DrawPropsGUI()
        {
            GUILayout.Label("Prop Settings", EditorStyles.label);
            _propDensity = EditorGUILayout.Slider("Density", _propDensity, 0.1f, 5f);
            _propRadius = EditorGUILayout.Slider("Brush Radius", _propRadius, 0.5f, 5f);
            
            // Simple prop selection from theme (assuming props are a category)
            // Or allow manual prefab drop
             _selectedPropPrefab = (GameObject)EditorGUILayout.ObjectField("Prop Prefab", _selectedPropPrefab, typeof(GameObject), false);
        }

        private void UpdateSelectedCategory()
        {
            if (_currentTheme == null) return;
            _selectedCategory = _currentTheme.Categories.Find(c => c.Type == _selectedType);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_currentTheme == null) return;

            Event e = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            // Handle Inputs
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.R)
                {
                    _rotationY = (_rotationY + 90) % 360;
                    e.Use();
                }
                if (e.keyCode == KeyCode.V)
                {
                    _currentVariantIndex++;
                    e.Use();
                }
            }

            // Raycast against grid logic
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane plane = new Plane(Vector3.up, new Vector3(0, _yOffset, 0));

            if (plane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 snappedPos = GetSnappedPosition(hitPoint);

                // Draw Grid/Cursor
                Handles.color = new Color(0, 1, 1, 0.5f);
                Handles.DrawWireCube(snappedPos, new Vector3(_gridSize, 0.1f, _gridSize));

                // Update Ghost
                UpdateGhost(snappedPos);

                // Handle Mouse Click
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                {
                    if (e.modifiers == EventModifiers.None)
                    {
                        if (_currentMode == ToolMode.Structure)
                            PaintModule(snappedPos);
                    }
                    else if (e.modifiers == EventModifiers.Shift)
                    {
                         // Erase logic (simplified)
                    }

                    // Consume event to prevent selection box
                    GUIUtility.hotControl = controlID;
                    e.Use();
                }
            }

            if (e.type == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;
            }

            // Force repaint to make the ghost movement smooth
            sceneView.Repaint();
        }

        private Vector3 GetSnappedPosition(Vector3 worldPos)
        {
            float x = Mathf.Round(worldPos.x / _gridSize) * _gridSize;
            float z = Mathf.Round(worldPos.z / _gridSize) * _gridSize;
            return new Vector3(x, _yOffset, z);
        }

        // Cache for transparent material
        private Material _ghostMaterial;

        private void UpdateGhost(Vector3 pos)
        {
            if (_selectedCategory == null || _selectedCategory.Variants.Length == 0) return;

            GameObject prefab = _currentTheme.GetVariant(_selectedType, _currentVariantIndex);
            if (prefab == null) return;

            // Re-instantiate if needed
            if (_ghostObject == null || _ghostObject.name != prefab.name + "_Ghost")
            {
                if (_ghostObject != null) DestroyImmediate(_ghostObject);
                _ghostObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                _ghostObject.name = prefab.name + "_Ghost";
                _ghostObject.hideFlags = HideFlags.HideAndDontSave;
                
                // Strip colliders
                var colliders = _ghostObject.GetComponentsInChildren<Collider>();
                foreach (var c in colliders) DestroyImmediate(c);

                // Apply Transparent Material
                ApplyGhostMaterial(_ghostObject);
            }
            else
            {
                 // Update material if changed dynamically
                 if (_customGhostMaterial != null && _ghostMaterial != _customGhostMaterial)
                 {
                     ApplyGhostMaterial(_ghostObject);
                 }
            }

            // Only update if changed to avoid dirtying scene too much (though it's a ghost)
            if (_ghostObject.transform.position != pos || _ghostObject.transform.rotation != Quaternion.Euler(0, _rotationY, 0))
            {
                _ghostObject.transform.position = pos;
                _ghostObject.transform.rotation = Quaternion.Euler(0, _rotationY, 0);
            }
        }

        private void ApplyGhostMaterial(GameObject ghost)
        {
            if (_customGhostMaterial != null)
            {
                _ghostMaterial = _customGhostMaterial;
            }
            else if (_ghostMaterial == null)
            {
                // Use Sprites/Default for reliable potential transparency without magenta issues
                Shader shader = Shader.Find("Sprites/Default"); 
                if (shader == null) shader = Shader.Find("Standard"); // Fallback
                _ghostMaterial = new Material(shader);
                _ghostMaterial.color = new Color(0.5f, 1f, 1f, 0.4f); // Cyan transparent
            }

            var renderers = ghost.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                r.sharedMaterial = _ghostMaterial;
            }
        }

        private void PaintModule(Vector3 pos)
        {
             if (_selectedCategory == null) return;
             
             GameObject prefab = _currentTheme.GetVariant(_selectedType, _currentVariantIndex);
             if (prefab == null) return;

             // Check if something is already there? (Simple overlap check)
             // For now, just instantiate.

             GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
             instance.transform.position = pos;
             instance.transform.rotation = Quaternion.Euler(0, _rotationY, 0);
             
             Undo.RegisterCreatedObjectUndo(instance, "Place Module");

             // Parent to common container
             Transform container = GetContainer(_selectedType.ToString());
             instance.transform.parent = container;

             if (_autoColumns && (_selectedType == ModuleType.Wall2x2 || _selectedType == ModuleType.Wall4x4))
             {
                 TryPlaceAutocolumns(instance);
             }
        }

        private Transform GetContainer(string name)
        {
            GameObject container = GameObject.Find(name + "_Container");
            if (container == null)
            {
                container = new GameObject(name + "_Container");
                Undo.RegisterCreatedObjectUndo(container, "Create Container");
            }
            return container.transform;
        }

        private void TryPlaceAutocolumns(GameObject wallInstance)
        {
            // Calculate endpoints based on actual Mesh Bounds to be robust against GridSize mismatch
            var meshFilter = wallInstance.GetComponent<MeshFilter>();
            if (meshFilter == null) return;

            // Assume pivot is center. We need extents along local Z (Forward)
            // If the mesh is rotated in prefab, this might be X, but standard is Z.
            float halfLength = meshFilter.sharedMesh.bounds.extents.z;
            
            // Transform to world scaling (lossyScale.z)
            // If scale is 1, it matches mesh.
            halfLength *= wallInstance.transform.lossyScale.z;

            Vector3 center = wallInstance.transform.position;
            Vector3 forward = wallInstance.transform.forward;

            Vector3 p1 = center + forward * halfLength;
            Vector3 p2 = center - forward * halfLength;
            
            CheckAndSpawnColumn(p1, wallInstance);
            CheckAndSpawnColumn(p2, wallInstance);
        }

        private void CheckAndSpawnColumn(Vector3 pos, GameObject currentWallKey)
        {
            // 1. Check if column exists
            Collider[] hits = Physics.OverlapSphere(pos, 0.4f);
            foreach(var hit in hits) 
            {
                string name = hit.name.ToLower();
                if(name.Contains("column") || name.Contains("pillar")) return; 
            }

            // 2. Check for Perpendicular Neighbor Wall
            // We only spawn if we find a neighbor wall that is ~90 degrees rotated relative to us.
            bool foundPerpendicular = false;
            
            foreach(var hit in hits)
            {
                if (hit.gameObject == currentWallKey) continue; // Ignore self

                string name = hit.name.ToLower();
                if (name.Contains("wall"))
                {
                    // Check angle
                    float dot = Vector3.Dot(currentWallKey.transform.forward, hit.transform.forward);
                    // If dot is near 0, they are perpendicular.
                    if (Mathf.Abs(dot) < 0.1f)
                    {
                        foundPerpendicular = true;
                        if (_debugMode) Debug.Log($"[AutoColumn] Found Perpendicular neighbor {hit.name} at {pos}");
                        break;
                    }
                }
            }

            if (!foundPerpendicular)
            {
                if (_debugMode) Debug.Log($"[AutoColumn] Ignored at {pos}: No perpendicular neighbor found.");
                return;
            }

            GameObject columnPrefab = _currentTheme.GetRandomVariant(ModuleType.Column);
            if (columnPrefab == null) return;

            Transform colContainer = GetContainer("Column");
            
            GameObject col = (GameObject)PrefabUtility.InstantiatePrefab(columnPrefab);
            col.transform.position = pos;
            col.transform.rotation = Quaternion.identity; 
            col.transform.parent = colContainer;
            Undo.RegisterCreatedObjectUndo(col, "Auto Column");
            
            if (_debugMode) Debug.Log($"[AutoColumn] Spawned at {pos}");
        }
    }
}

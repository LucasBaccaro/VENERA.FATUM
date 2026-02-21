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
        private bool _autoNextVariant = false; // New setting
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
        private bool _isHanger = false; // Checkbox for wall-mounted items

        // World Anchor ([WORLD] GameObject from the chunk scene)
        private Transform _worldAnchor;
        private const string WorldAnchorName = "[WORLD]";

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

            // ── World Anchor Block ──────────────────────────────────────────────
            GUILayout.Space(6);
            GUILayout.Label("World Anchor", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (_worldAnchor != null)
                {
                    EditorGUILayout.HelpBox($"[WORLD] found: {_worldAnchor.gameObject.scene.name} @ {_worldAnchor.position}", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("No [WORLD] detected. Dungeon will be placed at grid origin.", MessageType.Warning);
                }

                if (GUILayout.Button("Detect", GUILayout.Width(60)))
                    DetectWorldAnchor();
            }

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
            _autoNextVariant = EditorGUILayout.Toggle("Auto Next Variant", _autoNextVariant);

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
             _isHanger = EditorGUILayout.Toggle("Hanger (Wall Mount)", _isHanger);
        }

        private void UpdateSelectedCategory()
        {
            if (_currentTheme == null) return;
            _selectedCategory = _currentTheme.Categories.Find(c => c.Type == _selectedType);
        }

        private void CycleVariant()
        {
            if (_selectedCategory == null || _selectedCategory.Variants.Length == 0) return;
            _currentVariantIndex = (_currentVariantIndex + 1) % _selectedCategory.Variants.Length;
            UpdateGhost(Vector3.zero); // Trigger ghost update (position will be fixed in OnSceneGUI)
        }

        // Debug lists
        private List<Vector3> _debugPoints = new List<Vector3>();

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
                    CycleVariant();
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
                Vector3 normal = Vector3.up;

                // Prop Placement Logic (Override with Physics Raycast)
                if (_currentMode == ToolMode.PropPainter)
                {
                    if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
                    {
                        hitPoint = hit.point;
                        normal = hit.normal;
                        
                        // For props, maybe we don't snap to grid? Or partial snap?
                        // Let's snap position but use surface normal.
                        // Actually props usually want free placement or smaller grid.
                        // Let's stick to _gridSize for now but allow offset? 
                        // User didn't specify grid for props, but "snap to wall".
                        // Let's just use raw hit point for now, maybe round to 0.1?
                        
                        snappedPos = hitPoint; // Free placement on surface
                    }
                    else
                    {
                        // If no physics hit (e.g. empty space), fallback to plane but reset normal
                        snappedPos = GetSnappedPosition(ray.GetPoint(enter));
                    }
                }

                // Draw Grid/Cursor
                Handles.color = new Color(0, 1, 1, 0.5f);
                if (_currentMode == ToolMode.Structure)
                {
                    Handles.DrawWireCube(snappedPos, new Vector3(_gridSize, 0.1f, _gridSize));
                }
                else
                {
                    Handles.DrawWireDisc(snappedPos, normal, 0.5f);
                    Handles.DrawLine(snappedPos, snappedPos + normal);
                }

                // Update Ghost
                if (_currentMode == ToolMode.Structure)
                    UpdateGhost(snappedPos);
                else
                    UpdatePropGhost(snappedPos, normal);

                // Draw Debug Points (Wall Endpoints)
                if (_debugPoints.Count > 0)
                {
                    Handles.color = Color.red;
                    foreach (var pt in _debugPoints)
                    {
                        Handles.SphereHandleCap(0, pt, Quaternion.identity, 0.3f, EventType.Repaint);
                    }
                }

                // Handle Mouse Click
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                {
                    if (e.modifiers == EventModifiers.None)
                    {
                        if (_currentMode == ToolMode.Structure)
                            PaintModule(snappedPos);
                        else if (_currentMode == ToolMode.PropPainter)
                            PaintProp(snappedPos, normal);
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

        private void UpdateGhost(Vector3 pos)
        {
            if (_selectedCategory == null || _selectedCategory.Variants.Length == 0) return;

            // Ensure index is valid (safety check)
            if (_currentVariantIndex >= _selectedCategory.Variants.Length) _currentVariantIndex = 0;

            GameObject prefab = _currentTheme.GetVariant(_selectedType, _currentVariantIndex);
            if (prefab == null) return;

            // Re-instantiate if needed
            if (_ghostObject == null || _ghostObject.name != prefab.name + "_Ghost")
            {
                if (_ghostObject != null) DestroyImmediate(_ghostObject);
                _ghostObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                _ghostObject.name = prefab.name + "_Ghost";
                _ghostObject.hideFlags = HideFlags.HideAndDontSave;
                
                // Strip colliders but keep them for calculation? 
                // Wait, if we strip them we can't calculate bounds on the ghost!
                // We should calculate bounds BEFORE stripping, or just disable them.
                var colliders = _ghostObject.GetComponentsInChildren<Collider>();
                foreach (var c in colliders) c.enabled = false; // Disable instead of destroy

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

            // Update transform
            if (_ghostObject.transform.position != pos || _ghostObject.transform.rotation != Quaternion.Euler(0, _rotationY, 0))
            {
                _ghostObject.transform.position = pos;
                _ghostObject.transform.rotation = Quaternion.Euler(0, _rotationY, 0);
                
                // Recalculate Debug Points based on Ghost
                if (_autoColumns && (_selectedType == ModuleType.Wall2x2 || _selectedType == ModuleType.Wall4x4))
                {
                    _debugPoints.Clear();
                    if (GetWallEndpoints(_ghostObject, out Vector3 p1, out Vector3 p2))
                    {
                        _debugPoints.Add(p1);
                        _debugPoints.Add(p2);
                    }
                }
                else
                {
                    _debugPoints.Clear();
                }
            }
        }

        private void TryPlaceAutocolumns(GameObject wallInstance)
        {
            if (GetWallEndpoints(wallInstance, out Vector3 p1, out Vector3 p2))
            {
                CheckAndSpawnColumn(p1, wallInstance);
                CheckAndSpawnColumn(p2, wallInstance);
            }
        }

        private bool GetWallEndpoints(GameObject wallObj, out Vector3 p1, out Vector3 p2)
        {
            p1 = wallObj.transform.position;
            p2 = wallObj.transform.position;

            // Use Colliders for consistency with user request
            var colliders = wallObj.GetComponentsInChildren<Collider>();
            if (colliders.Length == 0) return false;

            // Use Box/Mesh data directly as Collider.bounds needs enabled components
            Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;

            foreach (var col in colliders)
            {
                Bounds b = new Bounds(Vector3.zero, Vector3.zero);
                
                if (col is BoxCollider box)
                {
                    b = new Bounds(box.center, box.size);
                }
                else if (col is MeshCollider meshCol && meshCol.sharedMesh != null)
                {
                    b = meshCol.sharedMesh.bounds;
                }
                else 
                {
                    continue; // Sphere/Capsule not supported for walls yet
                }

                // Current b is in Collider Local Space.
                // Transform corners to Wall Local Space.
                
                Vector3[] localCorners = GetBoundsCorners(b);
                foreach(var pt in localCorners)
                {
                   // Collider -> World -> Wall Local
                   // But since we are already in ghost (which might be scaled),
                   // let's just transform from Col -> Wall directly if hierarchical.
                   // If Col is on Wall root, no transform needed.
                   
                   Vector3 wallLocalPt = pt;
                   if (col.transform != wallObj.transform)
                   {
                       // Transform point from child local to world, then world to root local
                       Vector3 worldPt = col.transform.TransformPoint(pt);
                       wallLocalPt = wallObj.transform.InverseTransformPoint(worldPt);
                   }

                   if (first)
                   {
                       localBounds = new Bounds(wallLocalPt, Vector3.zero);
                       first = false;
                   }
                   else
                   {
                       localBounds.Encapsulate(wallLocalPt);
                   }
                }
            }
            
            if (first) return false;

            Vector3 size = localBounds.size;
            Vector3 center = localBounds.center;

            // Determine Major Axis (Longest dimension on X or Z)
            if (size.x > size.z)
            {
                // Oriented along local X
                Vector3 localP1 = new Vector3(localBounds.min.x, 0, center.z);
                Vector3 localP2 = new Vector3(localBounds.max.x, 0, center.z);

                p1 = wallObj.transform.TransformPoint(localP1);
                p2 = wallObj.transform.TransformPoint(localP2);
            }
            else
            {
                // Oriented along local Z (Forward)
                Vector3 localP1 = new Vector3(center.x, 0, localBounds.min.z);
                Vector3 localP2 = new Vector3(center.x, 0, localBounds.max.z);

                p1 = wallObj.transform.TransformPoint(localP1);
                p2 = wallObj.transform.TransformPoint(localP2);
            }



            // Snap to nearest grid point for perfect column alignment (as requested)
            // We use the wall's Y (world) for the height, but snap X and Z to _gridSize
            
            float snapSize = _gridSize;
            if (_selectedType == ModuleType.Wall2x2) snapSize = _gridSize / 2f;

            p1 = new Vector3(
                Mathf.Round(p1.x / snapSize) * snapSize,
                p1.y,
                Mathf.Round(p1.z / snapSize) * snapSize
            );

            p2 = new Vector3(
                Mathf.Round(p2.x / snapSize) * snapSize,
                p2.y,
                Mathf.Round(p2.z / snapSize) * snapSize
            );

            return true;
        }

        private Vector3[] GetBoundsCorners(Bounds b)
        {
            Vector3 min = b.min;
            Vector3 max = b.max;
            return new Vector3[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };
        }

        private Vector3 GetSnappedPosition(Vector3 worldPos)
        {
            // Snap relative to the world anchor so the grid aligns to chunk origin
            Vector3 anchorPos = _worldAnchor != null ? _worldAnchor.position : Vector3.zero;
            float x = Mathf.Round((worldPos.x - anchorPos.x) / _gridSize) * _gridSize + anchorPos.x;
            float z = Mathf.Round((worldPos.z - anchorPos.z) / _gridSize) * _gridSize + anchorPos.z;
            return new Vector3(x, anchorPos.y + _yOffset, z);
        }

        // Cache for transparent material
        private Material _ghostMaterial;

        private void CheckAndSpawnColumn(Vector3 pos, GameObject currentWallKey)
        {
             // 1. Check if column exists
            Collider[] hits = Physics.OverlapSphere(pos, 0.5f); 
            foreach(var hit in hits) 
            {
                string name = hit.name.ToLower();
                if(name.Contains("column") || name.Contains("pillar")) return; 
            }

            // 2. Check for Perpendicular Neighbor Wall
            bool foundPerpendicular = false;
            
            foreach(var hit in hits)
            {
                // Ignore self and children
                if (hit.transform.IsChildOf(currentWallKey.transform)) continue;

                string name = hit.name.ToLower();
                bool isWall = name.Contains("wall") || (hit.transform.parent != null && hit.transform.parent.name.ToLower().Contains("wall"));
                
                if (isWall)
                {
                    // Find root to check alignment
                    // Find root to check alignment
                    Transform otherRoot = hit.transform;
                    
                    // Traverse up to find the Module Instance (child of a _Container)
                    // We want the object JUST BELOW the container
                    while (otherRoot.parent != null && !otherRoot.parent.name.Contains("_Container"))
                    {
                        otherRoot = otherRoot.parent;
                        // Safety break for scene root
                        if (otherRoot.parent == null) break; 
                    }
                    
                    // If we stopped because parent is null, it might be a root object or we missed the container
                    if (otherRoot.parent == null) continue;

                    // If we stopped because parent has "_Container", then otherRoot is the module. Correct.
                    // But if otherRoot ITSELF is the container (because hit was the container), we skip
                    if (otherRoot.name.Contains("_Container")) continue;

                    if (_debugMode) Debug.Log($"[AutoColumn] Checking alignment with {otherRoot.name}");


                    // Force normalize to be safe
                    Vector3 myForward = currentWallKey.transform.forward;
                    Vector3 otherForward = otherRoot.forward;
                    
                    float dot = Mathf.Abs(Vector3.Dot(myForward, otherForward));
                    
                    // If walls are parallel (dot near 1), we generally do NOT want a column 
                    // unless it's a specific T-junction case, but usually columns go in corners.
                    // The user complained about "consecutive walls" triggering columns.
                    if (dot > 0.9f) 
                    {
                        if (_debugMode) Debug.Log($"[AutoColumn] Ignoring parallel/consecutive wall {otherRoot.name} (Dot: {dot})");
                        continue; 
                    }

                    // Check for Perpendicular (dot near 0)
                    if (dot < 0.1f)
                    {
                        foundPerpendicular = true;
                        if (_debugMode) Debug.Log($"[AutoColumn] Found Perpendicular neighbor {otherRoot.name} at {pos}");
                        break;
                    }
                }
            }

            if (!foundPerpendicular) return;

            GameObject columnPrefab = _currentTheme.GetRandomVariant(ModuleType.Column);
            if (columnPrefab == null) return;

            Transform colContainer = GetContainer("Column");
            
            GameObject col = (GameObject)PrefabUtility.InstantiatePrefab(columnPrefab);
            col.transform.position = pos;
            col.transform.rotation = Quaternion.identity; 
            col.transform.parent = colContainer;
            Undo.RegisterCreatedObjectUndo(col, "Auto Column");
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
                 Physics.SyncTransforms(); // Update collider positions for OverlapSphere
                 TryPlaceAutocolumns(instance);
             }

             if (_autoNextVariant)
             {
                 CycleVariant();
             }
        }

        private void UpdatePropGhost(Vector3 pos, Vector3 normal)
        {
            if (_selectedPropPrefab == null) return;

            // Re-instantiate if needed
            if (_ghostObject == null || _ghostObject.name != _selectedPropPrefab.name + "_Ghost_Prop")
            {
                if (_ghostObject != null) DestroyImmediate(_ghostObject);
                _ghostObject = (GameObject)PrefabUtility.InstantiatePrefab(_selectedPropPrefab);
                _ghostObject.name = _selectedPropPrefab.name + "_Ghost_Prop";
                _ghostObject.hideFlags = HideFlags.HideAndDontSave;
                
                var colliders = _ghostObject.GetComponentsInChildren<Collider>();
                foreach (var c in colliders) c.enabled = false;

                ApplyGhostMaterial(_ghostObject);
            }
            else
            {
                 if (_customGhostMaterial != null && _ghostMaterial != _customGhostMaterial)
                 {
                     ApplyGhostMaterial(_ghostObject);
                 }
            }

            // Update transform
            _ghostObject.transform.position = pos;
            
            // Rotation Logic
            if (_isHanger)
            {
                // Hanger: Align Forward to Normal (stick out of wall)
                if (normal != Vector3.zero)
                    _ghostObject.transform.rotation = Quaternion.LookRotation(normal);
            }
            else
            {
                // Standard: Align Up to Normal (sit on floor/slope)
                _ghostObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
                // Maybe apply Y rotation on top? 
                _ghostObject.transform.Rotate(0, _rotationY, 0, Space.Self);
            }
        }

        private void PaintProp(Vector3 pos, Vector3 normal)
        {
             if (_selectedPropPrefab == null) return;
             
             GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(_selectedPropPrefab);
             instance.transform.position = pos;
             
             if (_isHanger)
             {
                 if (normal != Vector3.zero)
                    instance.transform.rotation = Quaternion.LookRotation(normal);
             }
             else
             {
                 instance.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
                 instance.transform.Rotate(0, _rotationY, 0, Space.Self);
             }
             
             Undo.RegisterCreatedObjectUndo(instance, "Place Prop");

             Transform container = GetContainer("Props");
             instance.transform.parent = container;
        }

        private Transform GetContainer(string name)
        {
            // If we have a [WORLD] anchor, nest containers inside it
            if (_worldAnchor != null)
            {
                Transform existing = _worldAnchor.Find(name + "_Container");
                if (existing != null) return existing;

                GameObject container = new GameObject(name + "_Container");
                Undo.RegisterCreatedObjectUndo(container, "Create Container");
                container.transform.SetParent(_worldAnchor, worldPositionStays: true);
                return container.transform;
            }

            // Fallback: root-level container (original behaviour)
            GameObject rootContainer = GameObject.Find(name + "_Container");
            if (rootContainer == null)
            {
                rootContainer = new GameObject(name + "_Container");
                Undo.RegisterCreatedObjectUndo(rootContainer, "Create Container");
            }
            return rootContainer.transform;
        }

        private void DetectWorldAnchor()
        {
            _worldAnchor = null;

            // Search all root GameObjects in all loaded scenes
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == WorldAnchorName)
                    {
                        _worldAnchor = root.transform;
                        Debug.Log($"[DungeonCreator] Found [WORLD] anchor in scene '{scene.name}' at {_worldAnchor.position}");
                        return;
                    }
                }
            }

            Debug.LogWarning("[DungeonCreator] No [WORLD] GameObject found in any loaded scene.");
        }
    }
}

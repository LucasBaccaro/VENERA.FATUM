using System.Collections.Generic;
using UnityEngine;

public class OccluderFader : MonoBehaviour
{
    public static OccluderFader Instance;

    [Header("Refs")]
    public Transform cameraTransform;
    public Transform target; 
    public Material occluderFadeMaterial;

    [Header("Detection Settings")]
    public LayerMask occluderMask;
    [Tooltip("Radius of the circular hole in the shader (relative to screen height).")]
    public float targetWindowRadius = 0.2f;
    [Tooltip("Vertical offset for the target position (e.g., to center the circle on the chest instead of feet).")]
    public float targetVerticalOffset = 1.0f;

    [Header("Grouping")]
    [Tooltip("If true, when an object is occluded, its entire hierarchy (building) will fade together.")]
    public bool useHierarchicalGrouping = true;

    [Header("Smoothing")]
    [Tooltip("How fast the transition happens (0 to 1 progress per second).")]
    public float fadeSpeed = 5f;

    [Header("Debug")]
    public bool showDebugRay = true;
    public bool showDetectionBox = false;

    private class FadeState
    {
        public Renderer renderer;
        public float currentProgress; 
        public bool isTargeted;
        public Material[] originalMaterials;
        public Texture originalTexture;
        public Color originalSpecColor;
        public Texture originalSpecTex;
        public bool hasSpecColor;
        public bool hasSpecTex;

        public FadeState(Renderer r, Material[] originals)
        {
            renderer = r;
            originalMaterials = originals;
            currentProgress = 0f;
            isTargeted = true;

            if (originals != null && originals.Length > 0 && originals[0] != null)
            {
                if (originals[0].HasProperty("_BaseMap"))
                    originalTexture = originals[0].GetTexture("_BaseMap");
                else if (originals[0].HasProperty("_MainTex"))
                    originalTexture = originals[0].GetTexture("_MainTex");

                // Búsqueda exhaustiva y segura de Specular/Metallic
                string[] specPropertyNames = { "_SpecGlossMap", "_MetallicGlossMap", "_Specular", "_SpecColor", "_Metallic" };
                foreach (string prop in specPropertyNames)
                {
                    if (originals[0].HasProperty(prop))
                    {
                        // Intentamos obtener textura (solo si no es un color/float conocido)
                        if (!prop.Contains("Color") && prop != "_Metallic")
                        {
                            try
                            {
                                Texture tex = originals[0].GetTexture(prop);
                                if (tex != null)
                                {
                                    originalSpecTex = tex;
                                    hasSpecTex = true;
                                    break; 
                                }
                            } catch { /* No es una textura, ignorar error */ }
                        }

                        // Si no es textura, intentamos Color o Float
                        if (!hasSpecTex)
                        {
                            try {
                                originalSpecColor = originals[0].GetColor(prop);
                                hasSpecColor = true;
                            } catch {
                                try {
                                    float val = originals[0].GetFloat(prop);
                                    originalSpecColor = new Color(val, val, val, 1);
                                    hasSpecColor = true;
                                } catch { /* No es color ni float, ignorar */ }
                            }
                        }
                    }
                }
            }
        }
    }

    private readonly Dictionary<Renderer, FadeState> activeFades = new();
    private readonly List<Renderer> environmentRenderers = new();
    private readonly Dictionary<Renderer, Transform> rendererToRoot = new();
    private readonly Vector3[] _cornerCache = new Vector3[8];
    private readonly Vector3[] _pointsCache = new Vector3[20];
    private int _pointsCount = 0;
    private readonly HashSet<Transform> _occludedRootsCache = new();
    private static readonly int[,] BoundingBoxEdges = new[,]
    {
        {0,1}, {1,3}, {3,2}, {2,0}, // Min Z face
        {4,5}, {5,7}, {7,6}, {6,4}, // Max Z face
        {0,4}, {1,5}, {2,6}, {3,7}  // Vertical edges
    };
    
    private Camera _cam;
    private MaterialPropertyBlock _propBlock;
    
    private static readonly int PlayerScreenPosID = Shader.PropertyToID("_PlayerScreenPos");
    private static readonly int PlayerYID = Shader.PropertyToID("_PlayerY");
    private static readonly int WindowRadiusID = Shader.PropertyToID("_WindowRadius");
    private static readonly int BaseMapID = Shader.PropertyToID("_BaseMap");
    private static readonly int SpecularID = Shader.PropertyToID("_Specular");
    private static readonly int ScreenAspectID = Shader.PropertyToID("_ScreenAspect");
    
    private float _nextRefreshTime = 0f;

    void Awake()
    {
        Instance = this;
        _propBlock = new MaterialPropertyBlock();
        InitCamera();
        RefreshRenderers();
    }

    [ContextMenu("Refresh Renderers Now")]
    public void RefreshRenderers()
    {
        environmentRenderers.Clear();
        rendererToRoot.Clear();
        Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        
        foreach (var r in allRenderers)
        {
            if (((1 << r.gameObject.layer) & occluderMask) != 0)
            {
                environmentRenderers.Add(r);
                if (useHierarchicalGrouping)
                    rendererToRoot[r] = GetLogicalRoot(r.transform);
            }
        }
    }

    private Transform GetLogicalRoot(Transform t)
    {
        Transform current = t;
        Transform lastValid = t;
        while (current.parent != null && ((1 << current.parent.gameObject.layer) & occluderMask) != 0)
        {
            current = current.parent;
            lastValid = current;
        }
        return lastValid;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    void LateUpdate()
    {
        if (target == null || occluderFadeMaterial == null) return;

        if (cameraTransform == null || _cam == null) {
            InitCamera();
            if (cameraTransform == null) return;
        }

        if (Time.time > _nextRefreshTime)
        {
            RefreshRenderers();
            _nextRefreshTime = Time.time + 3f; // Refresh every 3 seconds to catch dynamic objects
        }

        Vector3 camPos = cameraTransform.position;
        Vector3 playerPos = target.position + Vector3.up * targetVerticalOffset;

        if (showDebugRay) Debug.DrawLine(camPos, playerPos, Color.red);

        // 1) ACTUALIZAR GLOBALES DEL SHADER (Incluyendo Aspect Ratio)
        float aspect = (float)Screen.width / Screen.height;
        Vector3 playerViewportPos = _cam.WorldToViewportPoint(playerPos);
        
        Shader.SetGlobalVector(PlayerScreenPosID, new Vector4(playerViewportPos.x, playerViewportPos.y, 0, 0));
        Shader.SetGlobalFloat(PlayerYID, playerPos.y);
        Shader.SetGlobalFloat(ScreenAspectID, aspect);

        // 2) DETECCIÓN DE OCLUSIÓN
        _occludedRootsCache.Clear();
        float playerDepth = playerViewportPos.z;

        // Punto de referencia del jugador en espacio corregido (X escalado por aspect)
        Vector2 playerRef = new Vector2(playerViewportPos.x * aspect, playerViewportPos.y);

        foreach (var r in environmentRenderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;

            Bounds b = r.bounds;
            if (b.Contains(camPos))
            {
                _occludedRootsCache.Add(useHierarchicalGrouping && rendererToRoot.TryGetValue(r, out var rootInside) ? rootInside : r.transform);
                continue;
            }

            UpdateCornerCache(b);
            float minX = 2, maxX = -1, minY = 2, maxY = -1;
            float minZ = float.MaxValue;
            bool anyFront = false;

            // Pre-calculate world positions and check sides of camera near plane
            Plane nearPlane = new Plane(_cam.transform.forward, _cam.transform.position + _cam.transform.forward * 0.05f);
            
            _pointsCount = 0;

            for (int i = 0; i < 8; i++)
            {
                Vector3 worldPt = _cornerCache[i];
                if (nearPlane.GetSide(worldPt))
                {
                    _pointsCache[_pointsCount++] = _cam.WorldToViewportPoint(worldPt);
                    anyFront = true;
                }
            }

            // Edge clipping against the near plane in WORLD SPACE (Proper for Perspective)
            for (int i = 0; i < 12; i++)
            {
                Vector3 pA = _cornerCache[BoundingBoxEdges[i, 0]];
                Vector3 pB = _cornerCache[BoundingBoxEdges[i, 1]];

                if (nearPlane.GetSide(pA) != nearPlane.GetSide(pB))
                {
                    Ray ray = new Ray(pA, pB - pA);
                    if (nearPlane.Raycast(ray, out float enter))
                    {
                        Vector3 worldIntersect = ray.GetPoint(enter);
                        _pointsCache[_pointsCount++] = _cam.WorldToViewportPoint(worldIntersect);
                        anyFront = true;
                    }
                }
            }

            for (int i = 0; i < _pointsCount; i++)
            {
                Vector3 v = _pointsCache[i];
                float correctedX = v.x * aspect;
                minX = Mathf.Min(minX, correctedX); maxX = Mathf.Max(maxX, correctedX);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                minZ = Mathf.Min(minZ, v.z);
            }

            if (showDetectionBox && anyFront)
            {
                DrawDebugBox(minX, maxX, minY, maxY, aspect);
            }

            if (anyFront && minZ < playerDepth)
            {
                // Ahora comparamos en un espacio donde el círculo es realmente un círculo
                float margin = targetWindowRadius;
                if (playerRef.x >= (minX - margin) && playerRef.x <= (maxX + margin) &&
                    playerRef.y >= (minY - margin) && playerRef.y <= (maxY + margin))
                {
                    _occludedRootsCache.Add(useHierarchicalGrouping && rendererToRoot.TryGetValue(r, out var root) ? root : r.transform);
                }
            }
        }

        // 3) APLICAR FADES
        foreach (var state in activeFades.Values) state.isTargeted = false;

        foreach (var r in environmentRenderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            Transform root = useHierarchicalGrouping && rendererToRoot.TryGetValue(r, out var rRoot) ? rRoot : r.transform;

            if (_occludedRootsCache.Contains(root))
            {
                if (!activeFades.TryGetValue(r, out FadeState state))
                {
                    state = new FadeState(r, r.sharedMaterials);
                    activeFades.Add(r, state);
                    
                    Material[] replaced = new Material[state.originalMaterials.Length];
                    for (int m = 0; m < replaced.Length; m++) replaced[m] = occluderFadeMaterial;
                    r.sharedMaterials = replaced;
                }
                state.isTargeted = true;
            }
        }

        // 4) PROCESAR TRANSICIONES
        List<Renderer> toRemove = new List<Renderer>();
        foreach (var kvp in activeFades)
        {
            Renderer r = kvp.Key;
            FadeState state = kvp.Value;

            if (r == null) 
            {
                toRemove.Add(r);
                continue;
            }

            state.currentProgress = Mathf.MoveTowards(state.currentProgress, state.isTargeted ? 1f : 0f, fadeSpeed * Time.deltaTime);

            r.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat(WindowRadiusID, state.currentProgress * targetWindowRadius);
            _propBlock.SetFloat(ScreenAspectID, (float)Screen.width / Screen.height);
            
            if (state.originalTexture != null)
                _propBlock.SetTexture(BaseMapID, state.originalTexture);
            
            if (state.hasSpecTex)
                _propBlock.SetTexture(SpecularID, state.originalSpecTex);
            else if (state.hasSpecColor)
                _propBlock.SetColor(SpecularID, state.originalSpecColor);
                
            r.SetPropertyBlock(_propBlock);

            if (!state.isTargeted && state.currentProgress <= 0.001f)
            {
                r.sharedMaterials = state.originalMaterials;
                r.SetPropertyBlock(null);
                toRemove.Add(r);
            }
        }

        foreach (var r in toRemove) activeFades.Remove(r);
    }

    private void UpdateCornerCache(Bounds b)
    {
        Vector3 min = b.min;
        Vector3 max = b.max;
        _cornerCache[0] = new Vector3(min.x, min.y, min.z);
        _cornerCache[1] = new Vector3(min.x, min.y, max.z);
        _cornerCache[2] = new Vector3(min.x, max.y, min.z);
        _cornerCache[3] = new Vector3(min.x, max.y, max.z);
        _cornerCache[4] = new Vector3(max.x, min.y, min.z);
        _cornerCache[5] = new Vector3(max.x, min.y, max.z);
        _cornerCache[6] = new Vector3(max.x, max.y, min.z);
        _cornerCache[7] = new Vector3(max.x, max.y, max.z);
    }

    private void InitCamera()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (cameraTransform != null) _cam = cameraTransform.GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;
        if (_cam != null && cameraTransform == null) cameraTransform = _cam.transform;
    }

    private void DrawDebugBox(float minX, float maxX, float minY, float maxY, float aspect)
    {
        // Convert screen space back to world wires for visualization in scene view
        float z = 1.0f; // Near distance for debug
        Vector3 bl = _cam.ViewportToWorldPoint(new Vector3(minX / aspect, minY, z));
        Vector3 tl = _cam.ViewportToWorldPoint(new Vector3(minX / aspect, maxY, z));
        Vector3 tr = _cam.ViewportToWorldPoint(new Vector3(maxX / aspect, maxY, z));
        Vector3 br = _cam.ViewportToWorldPoint(new Vector3(maxX / aspect, minY, z));

        Debug.DrawLine(bl, tl, Color.green);
        Debug.DrawLine(tl, tr, Color.green);
        Debug.DrawLine(tr, br, Color.green);
        Debug.DrawLine(br, bl, Color.green);
    }
}
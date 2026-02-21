using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace Genesis.Editor.Dungeon
{
    /// <summary>
    /// Core extraction logic shared by PropExtractorWindow and PropFBXPostprocessor.
    /// For each mesh inside the FBX, instantiates it and saves/updates a prefab.
    /// The MeshFilter still references the FBX mesh, so updates to the FBX
    /// are automatically reflected in all dependent prefabs.
    /// </summary>
    public static class PropExtractorCore
    {
        public static void Extract(string fbxAssetPath, string outputPath, string layerName, bool addBoxCollider, Material overrideMaterial = null)
        {
            // Load all assets embedded in the FBX
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxAssetPath);
            if (allAssets == null || allAssets.Length == 0)
            {
                Debug.LogWarning($"[PropExtractor] No assets found at '{fbxAssetPath}'.");
                return;
            }

            // Ensure output directory exists
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                AssetDatabase.Refresh();
            }

            int layer = LayerMask.NameToLayer(layerName);
            if (layer == -1)
            {
                Debug.LogWarning($"[PropExtractor] Layer '{layerName}' not found. Defaulting to 0.");
                layer = 0;
            }

            int created  = 0;
            int updated  = 0;

            foreach (Object asset in allAssets)
            {
                // We only care about Mesh objects inside the FBX
                Mesh mesh = asset as Mesh;
                if (mesh == null) continue;

                string meshName = mesh.name;
                string prefabPath = $"{outputPath}/{meshName}.prefab";

                GameObject prefabGO = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefabGO != null)
                {
                    // ── UPDATE existing prefab ─────────────────────────────────────────
                    // Open the prefab for editing
                    using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
                    {
                        GameObject root = scope.prefabContentsRoot;
                        MeshFilter mf = root.GetComponent<MeshFilter>();
                        MeshRenderer mr = root.GetComponent<MeshRenderer>();

                        if (mf == null) mf = root.AddComponent<MeshFilter>();
                        if (mr == null) mr = root.AddComponent<MeshRenderer>();

                        // Re-assign the mesh reference (keeps it linked to FBX)
                        mf.sharedMesh = mesh;

                        ApplyMaterials(fbxAssetPath, meshName, mr, overrideMaterial);

                        root.layer = layer;

                        // Sync collider if the option is on
                        if (addBoxCollider && root.GetComponent<BoxCollider>() == null)
                            root.AddComponent<BoxCollider>();
                    }

                    Debug.Log($"[PropExtractor] <color=cyan>Updated</color> prefab: {prefabPath}");
                    updated++;
                }
                else
                {
                    // ── CREATE new prefab ──────────────────────────────────────────────
                    GameObject go = new GameObject(meshName);
                    go.layer = layer;

                    MeshFilter mf = go.AddComponent<MeshFilter>();
                    mf.sharedMesh = mesh;

                    MeshRenderer mr = go.AddComponent<MeshRenderer>();
                    ApplyMaterials(fbxAssetPath, meshName, mr, overrideMaterial);

                    if (addBoxCollider)
                        go.AddComponent<BoxCollider>();

                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                    Object.DestroyImmediate(go);

                    Debug.Log($"[PropExtractor] <color=green>Created</color> prefab: {prefabPath}");
                    created++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PropExtractor] <color=green>Done.</color> Created: {created}  Updated: {updated}  — Source: {fbxAssetPath}");
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static void ApplyMaterials(string fbxPath, string meshName, MeshRenderer targetRenderer, Material overrideMaterial)
        {
            if (overrideMaterial != null)
            {
                targetRenderer.sharedMaterial = overrideMaterial;
                return;
            }
            SyncMaterialsFromFBX(fbxPath, meshName, targetRenderer);
        }

        /// <summary>
        /// Tries to copy materials from the FBX's top-level GameObject for a given mesh name.
        /// Falls back to the default material if none found.
        /// </summary>
        private static void SyncMaterialsFromFBX(string fbxPath, string meshName, MeshRenderer targetRenderer)
        {
            // The FBX root GO often contains children matching mesh names
            GameObject fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxRoot == null) return;

            // Search for a child whose name matches the mesh
            MeshRenderer[] renderers = fbxRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer r in renderers)
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == meshName)
                {
                    targetRenderer.sharedMaterials = r.sharedMaterials;
                    return;
                }
            }

            // Fallback: leave existing materials or assign default
            if (targetRenderer.sharedMaterials == null || targetRenderer.sharedMaterials.Length == 0)
            {
                targetRenderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

public class OccluderFader : MonoBehaviour
{
    public static OccluderFader Instance;

    [Header("Refs")]
    public Transform cameraTransform;
    public Transform target; 
    public Material occluderFadeMaterial;

    [Header("Cast")]
    public LayerMask occluderMask;
    public float sphereRadius = 0.25f;
    public float extraDistance = 0.2f;

    private readonly Dictionary<Renderer, Material[]> originalMats = new();
    private readonly HashSet<Renderer> currentlyFaded = new();

    void Awake()
    {
        Instance = this;
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    void LateUpdate()
    {
        if (cameraTransform == null || target == null || occluderFadeMaterial == null) return;

        Vector3 from = cameraTransform.position;
        Vector3 to = target.position;
        Vector3 dir = (to - from);
        float dist = dir.magnitude;
        if (dist < 0.001f) return;
        dir /= dist;

        RaycastHit[] hits = Physics.SphereCastAll(from, sphereRadius, dir, dist + extraDistance, occluderMask, QueryTriggerInteraction.Ignore);

        HashSet<Renderer> shouldFade = new();

        for (int i = 0; i < hits.Length; i++)
        {
            Renderer r = hits[i].collider.GetComponentInParent<Renderer>();
            if (r == null) continue;

            shouldFade.Add(r);

            if (!currentlyFaded.Contains(r))
            {
                if (!originalMats.ContainsKey(r))
                    originalMats[r] = r.sharedMaterials;

                Material[] mats = r.sharedMaterials;
                Material[] replaced = new Material[mats.Length];
                for (int m = 0; m < replaced.Length; m++)
                    replaced[m] = occluderFadeMaterial;

                r.sharedMaterials = replaced;
                currentlyFaded.Add(r);
            }
        }

        var temp = new List<Renderer>(currentlyFaded);
        foreach (var r in temp)
        {
            if (!shouldFade.Contains(r))
            {
                if (originalMats.TryGetValue(r, out var mats))
                    r.sharedMaterials = mats;

                currentlyFaded.Remove(r);
            }
        }
    }
}
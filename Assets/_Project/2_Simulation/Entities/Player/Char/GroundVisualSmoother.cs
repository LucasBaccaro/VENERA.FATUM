using UnityEngine;

public class GroundVisualSmoother : MonoBehaviour
{
    [Header("References")]
    public Transform visualRoot;
    public LayerMask groundMask = ~0;

    [Header("Raycast")]
    public float rayHeight = 1.5f;
    public float rayDistance = 3f;

    [Header("Smoothing")]
    public float heightSmooth = 12f;
    public float maxOffset = 0.4f;

    float currentOffsetY;

    void LateUpdate()
    {
        if (visualRoot == null) return;

        Vector3 origin = transform.position + Vector3.up * rayHeight;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance, groundMask))
        {
            float targetOffset = hit.point.y - transform.position.y;

            targetOffset = Mathf.Clamp(targetOffset, -maxOffset, maxOffset);

            currentOffsetY = Mathf.Lerp(
                currentOffsetY,
                targetOffset,
                1f - Mathf.Exp(-heightSmooth * Time.deltaTime)
            );
        }
        else
        {
            currentOffsetY = Mathf.Lerp(
                currentOffsetY,
                0f,
                1f - Mathf.Exp(-heightSmooth * Time.deltaTime)
            );
        }

        Vector3 local = visualRoot.localPosition;
        local.y = currentOffsetY;
        visualRoot.localPosition = local;
    }
}

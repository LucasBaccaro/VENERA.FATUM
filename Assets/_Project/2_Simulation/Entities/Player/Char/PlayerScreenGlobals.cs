using UnityEngine;

public class PlayerScreenGlobals : MonoBehaviour
{
    public Transform player;
    public Camera cam;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (player == null || cam == null) return;

        Vector3 sp = cam.WorldToViewportPoint(player.position); // 0..1
        Shader.SetGlobalVector("_PlayerScreenPos", new Vector4(sp.x, sp.y, sp.z, 1f));
    }
}

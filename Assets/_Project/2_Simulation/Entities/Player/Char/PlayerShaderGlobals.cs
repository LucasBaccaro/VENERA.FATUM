using UnityEngine;

public class PlayerShaderGlobals : MonoBehaviour
{
    public Transform player;

    void Update()
    {
        if (player != null)
            Shader.SetGlobalFloat("_PlayerY", player.position.y);
    }
}

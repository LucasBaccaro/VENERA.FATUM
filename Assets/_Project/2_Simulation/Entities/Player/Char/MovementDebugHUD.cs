using UnityEngine;

public class MovementDebugHUD : MonoBehaviour
{
    public PlayerMotor motor;
    public CharacterController cc;
    public Transform player;

    public Vector2 screenOffset = new Vector2(10, 10);

    Vector3 lastPos;
    float speed;

    void Start()
    {
        if (player == null)
            player = transform;

        lastPos = player.position;
    }

    void Update()
    {
        Vector3 delta = player.position - lastPos;
        speed = delta.magnitude / Time.deltaTime;
        lastPos = player.position;
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(screenOffset.x, screenOffset.y, 300, 200));
        GUILayout.Label($"Speed (m/s): {speed:F2}");

        if (motor != null)
        {
            GUILayout.Label($"WalkSpeed: {motor.walkSpeed}");
            GUILayout.Label($"RunSpeed: {motor.runSpeed}");
        }

        if (cc != null)
        {
            GUILayout.Label($"Grounded: {cc.isGrounded}");
        }

        GUILayout.Label($"Rotation Y: {player.eulerAngles.y:F1}");
        GUILayout.EndArea();
    }
}

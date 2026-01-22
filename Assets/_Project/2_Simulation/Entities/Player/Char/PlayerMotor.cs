using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;   // Assign Main Camera (or CameraRig camera)
    public Animator animator;           // Assign your Animator

    [Header("Movement Speeds (m/s)")]
    public float walkSpeed = 2.2f;
    public float runSpeed = 4.0f;
    public bool holdShiftToRun = true;

    [Header("Acceleration / Deceleration")]
    public float accel = 14f;
    public float decel = 18f;

    [Header("Rotation")]
    public float turnSpeed = 14f;       // higher = snappier

    [Header("Gravity")]
    public float gravity = -20f;
    public float groundStick = -2f;     // keeps you grounded on slopes

    [Header("Animator")]
    public string speedParam = "Speed"; // your BlendTree param
    public float animSpeedScale = 1f;   // multiply to match thresholds

    CharacterController cc;
    Vector3 velocity;                  // includes vertical velocity in y
    Vector3 planarVel;                 // xz velocity we control

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        if (cameraTransform == null) return;

        // 1) Input
        float ix = Input.GetAxisRaw("Horizontal"); // A/D
        float iz = Input.GetAxisRaw("Vertical");   // W/S
        Vector2 input = new Vector2(ix, iz);
        input = Vector2.ClampMagnitude(input, 1f);

        // 2) Camera-relative direction on XZ
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 desiredDir = (camForward * input.y + camRight * input.x);
        desiredDir = desiredDir.sqrMagnitude > 0.0001f ? desiredDir.normalized : Vector3.zero;

        // 3) Target speed
        bool wantsRun = holdShiftToRun && Input.GetKey(KeyCode.LeftShift);
        float targetSpeed = wantsRun ? runSpeed : walkSpeed;

        Vector3 targetPlanarVel = desiredDir * targetSpeed;

        // 4) Accel / decel
        float a = (targetPlanarVel.sqrMagnitude > planarVel.sqrMagnitude) ? accel : decel;
        planarVel = Vector3.MoveTowards(planarVel, targetPlanarVel, a * Time.deltaTime);

        // 5) Rotate towards movement direction
        if (planarVel.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(planarVel.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        // 6) Gravity / ground stick
        if (cc.isGrounded)
        {
            if (velocity.y < 0f) velocity.y = groundStick;
        }
        velocity.y += gravity * Time.deltaTime;

        // 7) Move
        Vector3 move = planarVel + Vector3.up * velocity.y;
        cc.Move(move * Time.deltaTime);

        // 8) Animator speed
        if (animator != null)
        {
            float speed01 = planarVel.magnitude; // in m/s
            animator.SetFloat(speedParam, speed01 * animSpeedScale);
        }
    }
}

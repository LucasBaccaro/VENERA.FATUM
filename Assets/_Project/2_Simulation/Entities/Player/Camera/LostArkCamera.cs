using UnityEngine;
using UnityEngine.InputSystem; // Necesario para New Input System

public class LostArkCamera : MonoBehaviour
{
    public static LostArkCamera Instance; // Singleton para que el player la encuentre

    [Header("References")]
    public Transform target;          // El Jugador (se asignará solo)
    public Transform pivot;           // Pivot (hijo del rig)
    public Camera cam;                // Cámara principal

    [Header("Follow")]
    public Vector3 followOffset = Vector3.zero;
    public float followSmooth = 12f;

    [Header("Orbit (MMB Drag)")]
    public float yawSpeed = 20f; // Ajustado para New Input System (valores suelen ser diferentes)

    [Header("Dynamic Pitch (Zoom Based)")]
    public bool dynamicPitch = true;
    public float farPitch = 40f;      
    public float nearPitch = 15f;     

    [Header("Zoom (Mouse Wheel)")]
    public float minDistance = 6f;
    public float maxDistance = 18f;
    [Tooltip("Cuántos segundos tarda en ir de mínima a máxima distancia con la rueda")]
    public float zoomTransitionDuration = 2f;
    [Tooltip("Suavizado de la transición del zoom (más alto = más suave)")]
    public float zoomSmooth = 12f;

    float yaw;
    float zoomNormalized = 0.5f; // 0 = minDistance, 1 = maxDistance
    float targetZoomNormalized = 0.5f;
    float distance;

    void Awake()
    {
        Instance = this;
        if (cam == null) cam = Camera.main;
    }

    void Start()
    {
        // Inicializar zoom en el punto medio
        zoomNormalized = 0.5f;
        targetZoomNormalized = 0.5f;
        distance = Mathf.Lerp(minDistance, maxDistance, zoomNormalized);

        if (pivot != null)
        {
            var e = pivot.localRotation.eulerAngles;
            yaw = e.y;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    void LateUpdate()
    {
        if (target == null || pivot == null || cam == null)
            return;

        // 1) Seguimiento suave
        Vector3 desiredRigPos = target.position + followOffset;
        transform.position = Vector3.Lerp(
            transform.position,
            desiredRigPos,
            1f - Mathf.Exp(-followSmooth * Time.deltaTime)
        );

        // Validar que exista mouse conectado
        if (Mouse.current != null)
        {
            // 2) Rotación con click central (Middle Button)
            if (Mouse.current.middleButton.isPressed)
            {
                // En New Input System, delta.x es pixel delta frame a frame
                float mx = Mouse.current.delta.x.ReadValue(); 
                yaw += mx * yawSpeed * Time.deltaTime;
            }

            // 3) Zoom continuo
            // Scroll.y.ReadValue() suele devolver +-120 o valores normalizados dependiendo de configuración
            float wheel = Mouse.current.scroll.y.ReadValue();

            if (Mathf.Abs(wheel) > 0.1f)
            {
                // Scroll Up (+) -> Disminuye distancia (Acerca) -> Disminuye targetZoomNormalized
                // Scroll Down (-) -> Aumenta distancia (Aleja) -> Aumenta targetZoomNormalized

                // Velocidad de cambio: el rango completo (0 a 1) se recorre en zoomTransitionDuration segundos
                // Por lo tanto, la velocidad por segundo es 1.0 / zoomTransitionDuration
                float zoomSpeed = 1f / Mathf.Max(0.1f, zoomTransitionDuration); // Evitar división por 0

                float scrollDirection = wheel > 0 ? -1 : 1; // Scroll up acerca (disminuye)
                targetZoomNormalized += scrollDirection * zoomSpeed * Time.deltaTime * 60f; // *60 para compensar frame rate
                targetZoomNormalized = Mathf.Clamp01(targetZoomNormalized);
            }
        }

        // Interpolar suavemente el zoom normalizado
        zoomNormalized = Mathf.Lerp(zoomNormalized, targetZoomNormalized, 1f - Mathf.Exp(-zoomSmooth * Time.deltaTime));

        // Calcular distancia desde el valor normalizado
        distance = Mathf.Lerp(minDistance, maxDistance, zoomNormalized);

        // 4) Pitch dinámico
        float pitchToUse = farPitch;
        if (dynamicPitch)
        {
            float t = Mathf.InverseLerp(minDistance, maxDistance, distance);
            pitchToUse = Mathf.Lerp(nearPitch, farPitch, t);
        }

        // 5) Aplicar transformación
        pivot.localRotation = Quaternion.Euler(pitchToUse, yaw, 0f);
        cam.transform.position = pivot.position - pivot.forward * distance;
        cam.transform.LookAt(pivot.position);
    }
}
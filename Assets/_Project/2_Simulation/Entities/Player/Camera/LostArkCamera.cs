using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;


public class LostArkCamera : MonoBehaviour
{
    public static LostArkCamera Instance;

    [Header("References")]
    public Transform target;
    public Transform pivot;
    public Camera cam;

    [Header("Follow")]
    public Vector3 followOffset = Vector3.zero;
    public float followSmooth = 12f;

    [Header("Orbit (MMB Drag)")]
    public float yawSpeed = 20f;

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
    float zoomNormalized = 0.5f;
    float targetZoomNormalized = 0.5f;
    float distance;
    bool _firstTargetSet;

    void Awake()
    {
        Instance = this;
        if (cam == null) cam = Camera.main;
    }

    void Start()
    {
        yawSpeed = PlayerPrefs.GetFloat("gfx_sensitivity", yawSpeed);

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

        // Snap la cámara al target en el primer set para evitar lerp desde posición random
        if (newTarget != null && !_firstTargetSet)
        {
            _firstTargetSet = true;
            transform.position = newTarget.position + followOffset;
        }
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

        if (Mouse.current != null)
        {
            // 2) Rotación con click central (Middle Button)
            if (Mouse.current.middleButton.isPressed)
            {
                float mx = Mouse.current.delta.x.ReadValue();
                yaw += mx * yawSpeed * Time.deltaTime;
            }

            // 3) Zoom continuo
            float wheel = Mouse.current.scroll.y.ReadValue();

            if (Mathf.Abs(wheel) > 0.1f)
            {
                bool isOverUI = false;
                if (EventSystem.current != null)
                {
                    PointerEventData eventData = new PointerEventData(EventSystem.current);
                    eventData.position = Mouse.current.position.ReadValue();
                    List<RaycastResult> results = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(eventData, results);
                    
                    foreach (var res in results)
                    {
                        // 1. Si está en la capa UI, bloqueamos 100%
                        if (res.gameObject.layer == LayerMask.NameToLayer("UI"))
                        {
                            isOverUI = true; 
                            break;
                        }

                        // 2. Si el objeto tiene un PanelRaycaster (UI Toolkit), bloqueamos
                        // Aunque esté en capa Default, si el EventSystem lo detecta como hit de UI, bloqueamos.
                        // Usualmente PanelSettings tiene un PanelRaycaster.
                        if (res.gameObject.name.Contains("PanelSettings") || res.module is UnityEngine.UIElements.PanelRaycaster)
                        {
                            isOverUI = true;
                            break;
                        }

                        // Ignoramos explícitamente capas de juego como Ground, Default (si no es PanelSettings), etc.
                        // El resto de hits (como el Ground) no deberían bloquear el zoom.
                    }
                }

                if (!isOverUI)
                {
                    float zoomSpeed = 1f / Mathf.Max(0.1f, zoomTransitionDuration);
                    float scrollDirection = wheel > 0 ? -1 : 1;
                    targetZoomNormalized += scrollDirection * zoomSpeed * Time.deltaTime * 60f;
                    targetZoomNormalized = Mathf.Clamp01(targetZoomNormalized);
                }
            }

        }

        zoomNormalized = Mathf.Lerp(zoomNormalized, targetZoomNormalized, 1f - Mathf.Exp(-zoomSmooth * Time.deltaTime));
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

using UnityEngine;
using Genesis.Core;

namespace Genesis.Presentation.UI
{
    public class SafeZoneIndicatorUI : MonoBehaviour
    {
        [SerializeField] private GameObject safeZoneIcon; // UI icon (shield icon)
        [SerializeField] private TMPro.TextMeshProUGUI safeZoneText;

        void Start()
        {
            EventBus.Subscribe<bool>(WorldStreamingEvents.PLAYER_ZONE_CHANGED, OnZoneChanged);

            if (safeZoneIcon != null) safeZoneIcon.SetActive(false);
            if (safeZoneText != null) safeZoneText.gameObject.SetActive(false);
        }

        private void OnZoneChanged(bool isInSafeZone)
        {
            if (safeZoneIcon != null)
            {
                safeZoneIcon.SetActive(isInSafeZone);
            }

            if (safeZoneText != null)
            {
                safeZoneText.gameObject.SetActive(isInSafeZone);
                safeZoneText.text = "SAFE ZONE";
                safeZoneText.color = Color.green;
            }

            Debug.Log($"[SafeZoneUI] Zone changed: {isInSafeZone}");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<bool>(WorldStreamingEvents.PLAYER_ZONE_CHANGED, OnZoneChanged);
        }
    }
}

using UnityEngine;
using Genesis.Presentation.Audio;

namespace Genesis.Presentation.Audio
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class AmbientZoneTrigger : MonoBehaviour
    {
        [Header("Audio Settings")]
        [SerializeField] private AudioClip ambientClip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private float fadeTime = 2f;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;
            
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Set to Colliders layer (16) to ensure interaction with Player (3)
            gameObject.layer = 16; // Colliders layer
        }

        private void OnTriggerEnter(Collider other)
        {
            // Only trigger for the local player
            FishNet.Object.NetworkObject netObj = other.GetComponent<FishNet.Object.NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                if (ambientClip != null)
                {
                    Debug.Log($"[AmbientZoneTrigger] Player entered {gameObject.name}. Fading in: {ambientClip.name}");
                    AudioManager.Instance.PlayAmbient(ambientClip, fadeTime, volume);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            FishNet.Object.NetworkObject netObj = other.GetComponent<FishNet.Object.NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                Debug.Log($"[AmbientZoneTrigger] Player exited {gameObject.name}. Fading out ambient.");
                AudioManager.Instance.StopAmbient(fadeTime);
            }
        }

        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                
                Gizmos.color = new Color(0f, 1f, 1f, 1f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
    }
}

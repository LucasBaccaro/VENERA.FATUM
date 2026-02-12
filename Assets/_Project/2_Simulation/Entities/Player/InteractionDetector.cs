using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using Genesis.Core;

namespace Genesis.Simulation {

    [RequireComponent(typeof(NetworkObject))]
    public class InteractionDetector : NetworkBehaviour {

        [Header("Settings")]
        [SerializeField] private float _interactionRange = 3f;
        [SerializeField] private float _scanInterval = 0.1f;

        private IInteractable _nearestInteractable;
        private MonoBehaviour _nearestMono;
        private float _lastScanTime;

        private void Update() {
            if (!base.IsOwner) return;

            // Periodic scan for nearby interactables
            if (Time.time - _lastScanTime >= _scanInterval) {
                _lastScanTime = Time.time;
                ScanForInteractables();
            }

            // E key interaction
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) {
                TryInteract();
            }
        }

        private void ScanForInteractables() {
            IInteractable nearest = null;
            MonoBehaviour nearestMono = null;
            float minDist = float.MaxValue;

            Collider[] hits = Physics.OverlapSphere(transform.position, _interactionRange);
            foreach (var hit in hits) {
                if (hit.gameObject == gameObject) continue;

                // Check all MonoBehaviours on this collider for IInteractable
                var interactables = hit.GetComponents<IInteractable>();
                foreach (var interactable in interactables) {
                    // Skip ILootSource — handled by LootBagController
                    if (interactable is ILootSource) continue;

                    if (interactable.CanInteract(base.NetworkObject)) {
                        float dist = Vector3.Distance(transform.position, hit.transform.position);
                        if (dist < minDist) {
                            minDist = dist;
                            nearest = interactable;
                            nearestMono = interactable as MonoBehaviour;
                        }
                    }
                }
            }

            if (nearest != _nearestInteractable) {
                _nearestInteractable = nearest;
                _nearestMono = nearestMono;

                string prompt = nearest != null ? nearest.GetInteractionPrompt() : "";
                EventBus.Trigger("OnInteractionPromptChanged", prompt);
            }
        }

        private void TryInteract() {
            if (_nearestInteractable == null) return;

            // Send to server
            if (_nearestMono != null) {
                var nob = _nearestMono.GetComponent<NetworkObject>();
                if (nob != null) {
                    CmdInteract(nob);
                }
            }
        }

        [ServerRpc]
        private void CmdInteract(NetworkObject targetNob) {
            if (targetNob == null) return;

            // Validate range on server
            float dist = Vector3.Distance(transform.position, targetNob.transform.position);
            if (dist > _interactionRange * 1.5f) return;

            var interactable = targetNob.GetComponent<IInteractable>();
            if (interactable != null && interactable.CanInteract(base.NetworkObject)) {
                interactable.Interact(base.NetworkObject);
            }
        }

        private void OnDisable() {
            if (base.IsOwner && _nearestInteractable != null) {
                _nearestInteractable = null;
                _nearestMono = null;
                EventBus.Trigger("OnInteractionPromptChanged", "");
            }
        }
    }
}

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
        private PlayerStats _playerStats;
        private Camera _camera;

        private void Awake() {
            _playerStats = GetComponent<PlayerStats>();
            _camera = Camera.main;
        }

        private void Update() {
            if (!base.IsOwner) return;
            if (_playerStats != null && _playerStats.IsDead) return;

            // Periodic scan for nearby interactables
            if (Time.time - _lastScanTime >= _scanInterval) {
                _lastScanTime = Time.time;
                ScanForInteractables();
            }

            // E key: interact with nearest
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) {
                TryInteract();
            }

            // Right-click: interact with the object under the cursor
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) {
                TryInteractAtMouse();
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

            if (nearest != _nearestInteractable)
            {
                _nearestInteractable = nearest;
                _nearestMono = nearestMono;

                // Notify ALL systems (outline, UI, etc.) about the new nearest interactable
                // null = player walked out of range
                GameObject nearestGO = nearestMono != null ? nearestMono.gameObject : null;
                EventBus.Trigger<GameObject>("OnNearestInteractableChanged", nearestGO);

                string prompt = nearest != null ? nearest.GetInteractionPrompt() : "";
                EventBus.Trigger("OnInteractionPromptChanged", prompt);
            }
        }

        private void TryInteract() {
            if (_nearestInteractable == null) return;

            if (_nearestMono != null) {
                var nob = _nearestMono.GetComponent<NetworkObject>();
                if (nob != null) CmdInteract(nob);
            }
        }

        /// <summary>
        /// Casts a ray from the camera through the mouse position.
        /// Interacts with whatever IInteractable is under the cursor (if within range),
        /// or falls back to the nearest interactable if no raycast hit.
        /// </summary>
        private void TryInteractAtMouse() {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity)) return;

            // Must be directly pointing at a collider that belongs to an IInteractable
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null) return;

            var mono = interactable as MonoBehaviour;
            if (mono == null) return;

            float dist = Vector3.Distance(transform.position, mono.transform.position);
            if (dist > _interactionRange * 1.5f) return; // out of range

            var nob = mono.GetComponentInParent<NetworkObject>();
            if (nob != null) CmdInteract(nob);
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
                EventBus.Trigger<GameObject>("OnNearestInteractableChanged", null);
                EventBus.Trigger("OnInteractionPromptChanged", "");
            }
        }
    }
}

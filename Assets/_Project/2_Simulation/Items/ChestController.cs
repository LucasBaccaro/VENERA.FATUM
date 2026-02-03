using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Genesis.Core;
using Genesis.Data;
using Genesis.Items;
using System.Collections;
using System.Collections.Generic;

namespace Genesis.Simulation {
    
    public enum ChestState {
        Closed,
        Opened,
        Empty
    }

    public class ChestController : NetworkBehaviour, IInteractable, ILootSource {
        [Header("Configuration")]
        [SerializeField] private string _chestName = "Treasure Chest";
        [SerializeField] private LootTable _lootTable;
        [SerializeField] private float _interactRadius = 3f;
        
        [Header("Opening Settings")]
        [SerializeField] private float _openingTime = 2f;
        [SerializeField] private string _openingText = "Opening Chest";
        [SerializeField] private float _maxOpeningDistance = 5f;

        [Header("Visuals")]
        [SerializeField] private Animator _animator;
        [SerializeField] private GameObject _goldVisual; // The visual part representing the loot
        [SerializeField] private string _openTrigger = "Open";
        [SerializeField] private string _closeTrigger = "Close"; // Optional if we want re-closeable logic

        [Header("Network State")]
        private readonly SyncVar<ChestState> _state = new SyncVar<ChestState>(ChestState.Closed);
        private readonly SyncList<ItemSlot> _lootItems = new SyncList<ItemSlot>();
        private readonly SyncVar<string> _looterName = new SyncVar<string>("");

        // ILootSource Implementation
        public string LootName => _chestName;
        public IReadOnlyList<ItemSlot> LootItems => _lootItems;

        private void Awake() {
            _state.OnChange += OnStateChanged;
            _lootItems.OnChange += OnLootChanged;
        }

        public override void OnStartClient() {
            base.OnStartClient();
            Debug.Log($"[Chest] OnStartClient called. State: {_state.Value}, Position: {transform.position}");
            UpdateVisuals(_state.Value); // Force initial visual update
        }

        private void OnStateChanged(ChestState oldState, ChestState newState, bool asServer) {
            UpdateVisuals(newState);
        }

        private void UpdateVisuals(ChestState state) {
            if (_animator != null) {
                if (state == ChestState.Opened || state == ChestState.Empty) {
                    _animator.SetTrigger(_openTrigger);
                }
            }

            if (_goldVisual != null) {
                // Gold is visible if Closed or Opened, but hidden when Empty
                _goldVisual.SetActive(state != ChestState.Empty);
            }
        }

        private void OnLootChanged(SyncListOperation op, int index, ItemSlot oldItem, ItemSlot newItem, bool asServer) {
             if (asServer && _state.Value == ChestState.Opened && _lootItems.Count == 0) {
                 _state.Value = ChestState.Empty;
             }
        }

        #region IInteractable

        public void Interact(NetworkObject player) {
            if (!base.IsServer) return;

            if (_state.Value == ChestState.Closed) {
                // Start opening process with cast bar
                Debug.Log($"[Chest] Starting opening process for {player.name}");
                TargetStartOpening(player.Owner, _openingTime, _openingText);
            }
            
            if (_state.Value == ChestState.Opened) {
                // Open UI
                TargetOpenLootUI(player.Owner);
            }
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void CmdFinishOpening(NetworkObject player) {
            if (_state.Value != ChestState.Closed) return;
            
            // Check distance
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance > _maxOpeningDistance) {
                Debug.Log($"[Chest] Player {player.name} too far to open chest");
                TargetCancelOpening(player.Owner, "Too far from chest");
                return;
            }
            
            // Open and Generate Loot
            Debug.Log($"[Chest] Opening chest for {player.name}");
            GenerateLoot();
            _state.Value = ChestState.Opened;
            _looterName.Value = player.name;
            
            // Open UI immediately after opening
            TargetOpenLootUI(player.Owner);
        }

        public bool CanInteract(NetworkObject player) {
            return _state.Value != ChestState.Empty;
        }

        public string GetInteractionPrompt() {
            switch (_state.Value) {
                case ChestState.Closed: return "Open Chest";
                case ChestState.Opened: return "Loot Chest";
                default: return "";
            }
        }

        #endregion

        #region ILootSource

        public bool CanLoot(NetworkObject player) {
            return _state.Value == ChestState.Opened;
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdTakeItem(int lootIndex, NetworkObject player) {
            if (_state.Value != ChestState.Opened) return;

            TakeItem(lootIndex, player);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdTakeAll(NetworkObject player) {
            if (_state.Value != ChestState.Opened) return;
            
            TakeAll(player);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdTryInteract(NetworkObject player) {
            Interact(player);
        }

        #endregion

        #region Server Logic

        [Server]
        private void GenerateLoot() {
            _lootItems.Clear();

            if (_lootTable != null) {
                var generatedItems = _lootTable.GetLoot();
                foreach (var item in generatedItems) {
                    _lootItems.Add(item);
                }
                Debug.Log($"[Chest] Generated {_lootItems.Count} items.");
            } else {
                Debug.LogWarning("[Chest] No LootTable assigned!");
            }
        }

        [Server]
        private void TakeItem(int lootIndex, NetworkObject player) {
            if (lootIndex < 0 || lootIndex >= _lootItems.Count) return;
            ItemSlot item = _lootItems[lootIndex];
            
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.HasSpace(item.ItemID, item.Quantity)) {
                // Handle full inventory
                 EventBus.Trigger("OnCombatError", "Inventory full!");
                return;
            }

            if (inventory.AddItem(item.ItemID, item.Quantity, item.Tier, item.Rarity)) {
                _lootItems.RemoveAt(lootIndex);
            }
        }

        [Server]
        private void TakeAll(NetworkObject player) {
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
             if (inventory == null) return;

            for (int i = _lootItems.Count - 1; i >= 0; i--) {
                ItemSlot item = _lootItems[i];
                if (inventory.HasSpace(item.ItemID, item.Quantity)) {
                    if (inventory.AddItem(item.ItemID, item.Quantity, item.Tier, item.Rarity)) {
                        _lootItems.RemoveAt(i);
                    }
                }
            }
        }

        #endregion

        #region Client

        [TargetRpc]
        private void TargetStartOpening(FishNet.Connection.NetworkConnection conn, float duration, string text) {
            StartCoroutine(OpeningCoroutine(duration, text));
        }
        
        [TargetRpc]
        private void TargetCancelOpening(FishNet.Connection.NetworkConnection conn, string reason) {
            StopAllCoroutines();
            EventBus.Trigger<CastUpdateData>("OnCastUpdate", CastUpdateData.Empty);
            EventBus.Trigger("OnCombatError", reason);
        }
        
        private IEnumerator OpeningCoroutine(float duration, string text) {
            float elapsed = 0f;
            Vector3 startPosition = transform.position;
            
            // Get local player reference
            NetworkObject player = null;
            if (FishNet.InstanceFinder.ClientManager != null && FishNet.InstanceFinder.ClientManager.Connection != null) {
                player = FishNet.InstanceFinder.ClientManager.Connection.FirstObject;
            }
            
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float percent = Mathf.Clamp01(elapsed / duration);
                
                // Check distance
                if (player != null) {
                    float distance = Vector3.Distance(startPosition, player.transform.position);
                    if (distance > _maxOpeningDistance) {
                        EventBus.Trigger<CastUpdateData>("OnCastUpdate", CastUpdateData.Empty);
                        EventBus.Trigger("OnCombatError", "Moved too far from chest");
                        yield break;
                    }
                }
                
                // Update cast bar
                EventBus.Trigger<CastUpdateData>("OnCastUpdate", new CastUpdateData {
                    Percent = percent,
                    AbilityName = text,
                    RemainingTime = duration - elapsed,
                    Duration = duration,
                    IsChanneling = false,
                    TickRate = 0,
                    Category = AbilityCategory.Magical
                });
                
                yield return null;
            }
            
            // Clear cast bar
            EventBus.Trigger<CastUpdateData>("OnCastUpdate", CastUpdateData.Empty);
            
            // Tell server we finished
            CmdFinishOpening(player);
        }

        [TargetRpc]
        private void TargetOpenLootUI(FishNet.Connection.NetworkConnection conn) {
            EventBus.Trigger("OnLootOpened", (ILootSource)this);
        }

        #endregion
        
#if UNITY_EDITOR
        private void OnDrawGizmosSelected() {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
        }
#endif
    }
}

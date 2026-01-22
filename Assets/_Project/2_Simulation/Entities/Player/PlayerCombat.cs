using UnityEngine;
using UnityEngine.InputSystem; // Necesario para New Input System
using FishNet.Object;
using FishNet.Connection;
using Genesis.Data;
using Genesis.Simulation.Combat;
using System.Collections.Generic;

namespace Genesis.Simulation {

    public class PlayerCombat : NetworkBehaviour {

        [Header("References")]
        [SerializeField] private PlayerStats stats;
        [SerializeField] private TargetingSystem targeting;
        [SerializeField] private Animator animator;

        [Header("Loadout")]
        public List<AbilityData> abilitySlots = new List<AbilityData>();

        // State
        private Dictionary<int, float> _cooldowns = new Dictionary<int, float>();
        private float _gcdEndTime;
        private bool _isCasting;
        private AbilityData _currentCastAbility;

        // ═══════════════════════════════════════════════════════
        // INPUT LOOP (Client)
        // ═══════════════════════════════════════════════════════

        void Update() {
            if (!base.IsOwner) return;

            // Input 1-6 (New Input System)
            if (Keyboard.current.digit1Key.wasPressedThisFrame) TryCast(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) TryCast(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) TryCast(2);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) TryCast(3);
        }

        private void TryCast(int slotIndex) {
            if (slotIndex >= abilitySlots.Count) return;
            AbilityData ability = abilitySlots[slotIndex];
            if (ability == null) return;

            // 1. Validaciones Locales (Prediction)
            if (_isCasting) {
                Debug.LogWarning("Ya estás casteando.");
                return;
            }

            if (Time.time < _gcdEndTime) {
                Debug.LogWarning("En Global Cooldown.");
                return;
            }

            if (_cooldowns.TryGetValue(ability.ID, out float cdEnd)) {
                if (Time.time < cdEnd) {
                    Debug.LogWarning("Habilidad en Cooldown.");
                    return;
                }
            }

            if (stats.CurrentMana < ability.ManaCost) {
                Debug.LogWarning("Maná insuficiente.");
                return;
            }

            // Validar target
            int targetId = -1;
            if (ability.TargetingMode == TargetType.Enemy) {
                if (targeting.CurrentTarget == null) {
                    Debug.LogWarning("Necesitas un objetivo.");
                    return;
                }
                targetId = targeting.CurrentTarget.ObjectId;
            } else if (ability.TargetingMode == TargetType.Self) {
                targetId = base.ObjectId;
            }

            // Start Cast Visuals
            _isCasting = true;
            _currentCastAbility = ability;
            
            // Send to Server
            CmdCastAbility(ability.ID, targetId, targeting.GetGroundTargetPoint());
        }

        // ═══════════════════════════════════════════════════════
        // SERVER AUTHORITY
        // ═══════════════════════════════════════════════════════

        [ServerRpc]
        private void CmdCastAbility(int abilityId, int targetId, Vector3 groundPoint) {
            if (AbilityDatabase.Instance == null) {
                Debug.LogError("[PlayerCombat] AbilityDatabase.Instance is NULL! Check Resources folder.");
                return;
            }

            AbilityData ability = AbilityDatabase.Instance.GetAbility(abilityId);
            if (ability == null) {
                Debug.LogError($"[PlayerCombat] Ability ID {abilityId} not found in Database.");
                return;
            }

            // Validar recursos (Server Authority Real)
            if (stats.CurrentMana < ability.ManaCost) {
                RpcCastFailed(base.Owner, "No mana");
                return;
            }

            // Consumir recursos
            if (!stats.ConsumeMana(ability.ManaCost)) {
                RpcCastFailed(base.Owner, "No mana (Server Check)");
                return;
            }

            // Encontrar target
            NetworkObject target = null;
            if (targetId != -1) {
                if (FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(targetId, out NetworkObject found)) {
                    target = found;
                }
            }

            // EJECUTAR LÓGICA
            if (ability.Logic != null) {
                ability.Logic.Execute(base.NetworkObject, target, groundPoint, ability);
            } else {
                Debug.LogError($"Habilidad {ability.Name} no tiene script de Logic asignado!");
            }

            // Notificar éxito
            RpcCastSuccess(abilityId);
        }

        [ObserversRpc]
        private void RpcCastSuccess(int abilityId) {
            // Reset state
            if (base.IsOwner) {
                _isCasting = false;
                
                // Aplicar Cooldowns
                AbilityData ability = AbilityDatabase.Instance.GetAbility(abilityId);
                if (ability != null) {
                    _gcdEndTime = Time.time + ability.GCD;
                    _cooldowns[ability.ID] = Time.time + ability.Cooldown;
                }
            }
            
            // Play Animation trigger
            if (animator != null) animator.SetTrigger("Cast");
        }

        [TargetRpc]
        private void RpcCastFailed(NetworkConnection conn, string reason) {
            _isCasting = false;
            Debug.Log($"Cast falló: {reason}");
        }
    }
}

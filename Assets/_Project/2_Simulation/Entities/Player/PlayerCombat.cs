using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using FishNet.Connection;
using Genesis.Data;
using Genesis.Simulation.Combat;
using System.Collections.Generic;

namespace Genesis.Simulation {

    /// <summary>
    /// Sistema de combate del jugador - Sistema Híbrido (Targeted + Skillshots)
    /// Gestiona ejecución de habilidades, cooldowns, casting y aiming
    /// </summary>
    public class PlayerCombat : NetworkBehaviour {

        [Header("References")]
        [SerializeField] private PlayerStats stats;
        [SerializeField] private TargetingSystem targeting;
        [SerializeField] private Animator animator;
        [SerializeField] private AbilityIndicatorSystem indicatorSystem;

        [Header("Loadout")]
        public List<AbilityData> abilitySlots = new List<AbilityData>();

        // State
        private Dictionary<int, float> _cooldowns = new Dictionary<int, float>();
        private float _gcdEndTime;
        private CombatState _combatState = CombatState.Idle;
        private AbilityData _pendingAbility;
        private int _pendingSlotIndex;

        // ═══════════════════════════════════════════════════════
        // ENUMS
        // ═══════════════════════════════════════════════════════

        public enum CombatState {
            Idle,
            Aiming,       // Esperando confirmación del jugador (skillshot)
            Casting,      // Executing ability con cast time
            Channeling    // Manteniendo habilidad (ej: Rayo de Hielo)
        }

        // ═══════════════════════════════════════════════════════
        // INPUT LOOP (Client)
        // ═══════════════════════════════════════════════════════

        void Update() {
            if (!base.IsOwner) return;

            switch (_combatState) {
                case CombatState.Idle:
                    HandleIdleInput();
                    break;

                case CombatState.Aiming:
                    HandleAimingInput();
                    break;

                case CombatState.Casting:
                    // Existing casting logic (si implementamos cast bars)
                    break;

                case CombatState.Channeling:
                    HandleChannelingInput();
                    break;
            }
        }

        /// <summary>
        /// Input handling en estado Idle (escucha teclas 1-6)
        /// </summary>
        private void HandleIdleInput() {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) HandleAbilityInput(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) HandleAbilityInput(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) HandleAbilityInput(2);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) HandleAbilityInput(3);
            if (Keyboard.current.digit5Key.wasPressedThisFrame) HandleAbilityInput(4);
            if (Keyboard.current.digit6Key.wasPressedThisFrame) HandleAbilityInput(5);
        }

        /// <summary>
        /// Input handling en estado Aiming (espera confirmación o cancelación)
        /// </summary>
        private void HandleAimingInput() {
            // Actualizar posición del indicador con el mouse
            if (indicatorSystem != null) {
                indicatorSystem.UpdateIndicator(Mouse.current.position.ReadValue());
            }

            // Confirmar con Left Click
            if (Mouse.current.leftButton.wasPressedThisFrame) {
                ConfirmAbility();
            }

            // Cancelar con Right Click o Escape
            if (Mouse.current.rightButton.wasPressedThisFrame ||
                Keyboard.current.escapeKey.wasPressedThisFrame) {
                CancelAiming();
            }
        }

        /// <summary>
        /// Input handling para habilidades channeled (mantener para continuar)
        /// </summary>
        private void HandleChannelingInput() {
            // TODO: Implementar en Fase 7 (Rayo de Hielo)
            // Si Left Click se mantiene presionado → continuar channel
            // Si se suelta → terminar channel
        }

        // ═══════════════════════════════════════════════════════
        // ABILITY EXECUTION FLOW
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// PUNTO DE ENTRADA: Usuario presiona tecla de habilidad
        /// Decide si usar flujo Targeted (legacy) o Skillshot (nuevo)
        /// </summary>
        private void HandleAbilityInput(int slotIndex) {
            if (slotIndex >= abilitySlots.Count) return;
            AbilityData ability = abilitySlots[slotIndex];
            if (ability == null) return;

            // Validaciones básicas (comunes a ambos sistemas)
            if (!ValidateBasicRequirements(ability)) return;

            // DECISIÓN ARQUITECTÓNICA CLAVE: Targeted vs Skillshot
            if (ability.IndicatorType == IndicatorType.None) {
                // FLUJO LEGACY: Targeted ability (sistema actual - Fase 5)
                ExecuteTargetedAbility(ability);
            } else {
                // FLUJO NUEVO: Skillshot (requiere aiming)
                EnterAimingMode(ability);
            }
        }

        /// <summary>
        /// Validaciones básicas antes de ejecutar cualquier habilidad
        /// </summary>
        private bool ValidateBasicRequirements(AbilityData ability) {
            // Ya está casteando?
            if (_combatState == CombatState.Casting || _combatState == CombatState.Channeling) {
                Debug.LogWarning("[PlayerCombat] Already casting/channeling");
                return false;
            }

            // Global Cooldown activo?
            if (Time.time < _gcdEndTime) {
                Debug.LogWarning("[PlayerCombat] Global Cooldown active");
                return false;
            }

            // Cooldown específico de la habilidad?
            if (_cooldowns.TryGetValue(ability.ID, out float cdEnd)) {
                if (Time.time < cdEnd) {
                    float remaining = cdEnd - Time.time;
                    Debug.LogWarning($"[PlayerCombat] {ability.Name} on cooldown ({remaining:F1}s)");
                    return false;
                }
            }

            // Mana suficiente?
            if (stats.CurrentMana < ability.ManaCost) {
                Debug.LogWarning("[PlayerCombat] Insufficient mana");
                return false;
            }

            return true;
        }

        // ═══════════════════════════════════════════════════════
        // FLUJO LEGACY: TARGETED ABILITIES
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Ejecuta habilidad Targeted (sistema legacy - sin cambios)
        /// Requiere target seleccionado previamente
        /// </summary>
        private void ExecuteTargetedAbility(AbilityData ability) {
            int targetId = -1;
            bool isSelfCast = Keyboard.current.leftAltKey.isPressed;

            // Self-Cast Override (ALT key)
            if (isSelfCast && (ability.Category == AbilityCategory.Utility || ability.Category == AbilityCategory.Magical)) {
                // Asumimos que si presiona ALT quiere tirárselo a sí mismo (Heal/Buff)
                targetId = base.ObjectId;
            }
            else {
                // Validar target según modo normal
                if (ability.TargetingMode == TargetType.Enemy || ability.TargetingMode == TargetType.Ally) {
                    if (targeting.CurrentTarget == null) {
                        Debug.LogWarning("[PlayerCombat] Need a target");
                        return;
                    }
                    targetId = targeting.CurrentTarget.ObjectId;
                } else if (ability.TargetingMode == TargetType.Self) {
                    targetId = base.ObjectId;
                }
            }

            // Cambiar estado
            _combatState = CombatState.Casting;

            // Enviar al servidor (método legacy)
            CmdCastAbility(ability.ID, targetId, Vector3.zero);
        }

        /// <summary>
        /// LEGACY ServerRpc: Para habilidades targeted (mantener compatibilidad)
        /// </summary>
        [ServerRpc]
        private void CmdCastAbility(int abilityId, int targetId, Vector3 groundPoint) {
            if (AbilityDatabase.Instance == null) {
                Debug.LogError("[PlayerCombat] AbilityDatabase.Instance is NULL!");
                return;
            }

            AbilityData ability = AbilityDatabase.Instance.GetAbility(abilityId);
            if (ability == null) {
                Debug.LogError($"[PlayerCombat] Ability ID {abilityId} not found");
                return;
            }

            // Validaciones de servidor
            if (stats.CurrentMana < ability.ManaCost) {
                RpcCastFailed(base.Owner, "No mana");
                return;
            }

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

            // EJECUTAR LÓGICA (método legacy)
            if (ability.Logic != null) {
                ability.Logic.Execute(base.NetworkObject, target, groundPoint, ability);
            } else {
                Debug.LogError($"Habilidad {ability.Name} no tiene Logic asignado!");
            }

            // Notificar éxito
            RpcCastSuccess(abilityId);
        }

        // ═══════════════════════════════════════════════════════
        // FLUJO NUEVO: SKILLSHOT ABILITIES
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Entra en modo Aiming (muestra indicador y espera confirmación)
        /// </summary>
        private void EnterAimingMode(AbilityData ability) {
            _combatState = CombatState.Aiming;
            _pendingAbility = ability;

            // Mostrar indicador visual
            if (indicatorSystem != null) {
                indicatorSystem.ShowIndicator(ability, transform);
            } else {
                Debug.LogError("[PlayerCombat] AbilityIndicatorSystem is NULL! Assign in Inspector.");
                _combatState = CombatState.Idle;
                return;
            }

            Debug.Log($"[PlayerCombat] Aiming: {ability.Name}");
        }

        /// <summary>
        /// Confirma la habilidad (usuario hizo click)
        /// </summary>
        private void ConfirmAbility() {
            if (_pendingAbility == null || indicatorSystem == null) return;

            AbilityIndicator indicator = indicatorSystem.GetCurrentIndicator();
            if (indicator == null) return;

            // Validar que la posición sea válida
            if (!indicator.IsValid()) {
                Debug.LogWarning("[PlayerCombat] Invalid position!");
                return;
            }

            // Obtener datos del indicador
            Vector3 targetPoint = indicator.GetTargetPoint();
            Vector3 direction = indicator.GetDirection();

            // Ocultar indicador
            indicatorSystem.HideIndicator();

            // Cambiar estado
            _combatState = CombatState.Casting;

            // CLIENT PREDICTION PARA DASH
            // Si es un movimiento, debemos ejecutarlo localmente para que el Client Authoritative funcione
            if (_pendingAbility.IndicatorType == IndicatorType.Arrow) { // Arrow = Dash
                if (_pendingAbility.Logic != null) {
                    _pendingAbility.Logic.ExecuteDirectional(base.NetworkObject, targetPoint, direction, _pendingAbility);
                }
            }

            // Enviar al servidor (método nuevo direccional)
            CmdCastAbilityDirectional(_pendingAbility.ID, targetPoint, direction);

            _pendingAbility = null;
        }

        /// <summary>
        /// Cancela el aiming (usuario presionó Escape o click derecho)
        /// </summary>
        private void CancelAiming() {
            _combatState = CombatState.Idle;
            _pendingAbility = null;

            if (indicatorSystem != null) {
                indicatorSystem.HideIndicator();
            }

            Debug.Log("[PlayerCombat] Aiming canceled");
        }

        /// <summary>
        /// NEW ServerRpc: Para habilidades direccionales (skillshots)
        /// </summary>
        [ServerRpc]
        private void CmdCastAbilityDirectional(int abilityId, Vector3 targetPoint, Vector3 direction) {
            if (AbilityDatabase.Instance == null) return;

            AbilityData ability = AbilityDatabase.Instance.GetAbility(abilityId);
            if (ability == null) return;

            // Validaciones de servidor
            if (stats.CurrentMana < ability.ManaCost) {
                RpcCastFailed(base.Owner, "No mana");
                return;
            }

            if (!stats.ConsumeMana(ability.ManaCost)) {
                RpcCastFailed(base.Owner, "No mana (Server Check)");
                return;
            }

            // VALIDACIÓN DE DISTANCIA (Anti-cheat)
            float distanceToTarget = Vector3.Distance(transform.position, targetPoint);
            if (distanceToTarget > ability.Range * 1.2f) { // 20% tolerance for lag
                RpcCastFailed(base.Owner, "Too far");
                return;
            }

            // EJECUTAR LÓGICA DIRECCIONAL (nuevo método)
            if (ability.Logic != null) {
                ability.Logic.ExecuteDirectional(base.NetworkObject, targetPoint, direction, ability);
            } else {
                Debug.LogError($"Habilidad {ability.Name} no tiene Logic asignado!");
            }

            // Notificar éxito
            RpcCastSuccess(abilityId);
        }

        // ═══════════════════════════════════════════════════════
        // RPCs (COMUNES A AMBOS SISTEMAS)
        // ═══════════════════════════════════════════════════════

        [ObserversRpc]
        private void RpcCastSuccess(int abilityId) {
            // Reset state
            if (base.IsOwner) {
                _combatState = CombatState.Idle;

                // Aplicar Cooldowns
                AbilityData ability = AbilityDatabase.Instance.GetAbility(abilityId);
                if (ability != null) {
                    _gcdEndTime = Time.time + ability.GCD;
                    _cooldowns[ability.ID] = Time.time + ability.Cooldown;

                    Debug.Log($"[PlayerCombat] {ability.Name} cast successful. CD: {ability.Cooldown}s");
                }
            }

            // Play Animation trigger (en todos los clientes)
            if (animator != null) animator.SetTrigger("Cast");
        }

        [TargetRpc]
        private void RpcCastFailed(NetworkConnection conn, string reason) {
            _combatState = CombatState.Idle;
            Debug.LogWarning($"[PlayerCombat] Cast failed: {reason}");

            // Limpiar pending ability
            _pendingAbility = null;

            // Ocultar indicador si estaba activo
            if (indicatorSystem != null) {
                indicatorSystem.HideIndicator();
            }
        }

        // ═══════════════════════════════════════════════════════
        // DEBUG & UTILITIES
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Obtiene el cooldown restante de una habilidad (para UI)
        /// </summary>
        public float GetCooldownRemaining(int abilityId) {
            if (_cooldowns.TryGetValue(abilityId, out float cdEnd)) {
                float remaining = cdEnd - Time.time;
                return Mathf.Max(0, remaining);
            }
            return 0;
        }

        /// <summary>
        /// Verifica si una habilidad está en cooldown
        /// </summary>
        public bool IsAbilityOnCooldown(int abilityId) {
            return GetCooldownRemaining(abilityId) > 0;
        }

        /// <summary>
        /// Obtiene el GCD restante (para UI)
        /// </summary>
        public float GetGCDRemaining() {
            return Mathf.Max(0, _gcdEndTime - Time.time);
        }
    }
}

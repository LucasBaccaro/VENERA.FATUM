using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using FishNet.Connection;
using Genesis.Data;
using Genesis.Simulation.Combat;
using Genesis.Core;
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
        [SerializeField] private Transform castVFXSpawnPoint;

        [Header("Loadout")]
        public List<AbilityData> abilitySlots = new List<AbilityData>();

        // State
        private Dictionary<int, float> _cooldowns = new Dictionary<int, float>();
        private float _gcdEndTime;
        private CombatState _combatState = CombatState.Idle;
        private AbilityData _pendingAbility;
        private int _pendingSlotIndex;

        // Cast tracking
        private float _castStartTime;
        private float _castDuration;
        private string _castingAbilityName = "";
        private Coroutine _currentCastCoroutine;
        private NetworkObject _currentCastVFX;

        // Data para ejecutar después del cast
        private int _pendingTargetId = -1;
        private Vector3 _pendingTargetPoint = Vector3.zero;
        private Vector3 _pendingDirection = Vector3.zero;
        private bool _isDirectionalCast = false;

        // Channeling tracking
        private float _channelStartTime;
        private float _nextChannelTick;
        private float _channelTickRate;
        private float _channelMaxDuration;
        private float _movementGracePeriod = 0.3f; // Coyote time para evitar cancelación por movimiento accidental
        private bool _isInMovementGracePeriod = false;
        private Vector3 _channelStartPosition; // Posición cuando inició el channeling (para detectar movimiento)

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
                    HandleCastingUpdate();
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
        /// Input handling para habilidades channeled
        /// Actualiza la dirección del rayo con el mouse y ejecuta ticks de daño
        /// </summary>
        private void HandleChannelingInput() {
            if (_pendingAbility == null) {
                StopChanneling();
                return;
            }

            // Verificar duración máxima (si existe)
            if (_channelMaxDuration > 0 && Time.time - _channelStartTime >= _channelMaxDuration) {
                StopChanneling();
                return;
            }

            // Calcular dirección desde el jugador hacia el mouse
            Vector3 channelDirection = _pendingDirection; // Fallback a dirección inicial
            if (Mouse.current != null && Camera.main != null) {
                Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f, LayerMask.GetMask("Ground", "Environment"))) {
                    Vector3 directionToMouse = (hit.point - transform.position);
                    directionToMouse.y = 0; // Mantener horizontal
                    if (directionToMouse.sqrMagnitude > 0.001f) {
                        channelDirection = directionToMouse.normalized;
                        _pendingDirection = channelDirection; // Actualizar dirección guardada
                    }
                }
            }

            // ACTUALIZAR POSICIÓN Y ROTACIÓN DEL CAST VFX
            if (_currentCastVFX != null) {
                // Actualizar posición del VFX al spawn point (sigue al jugador aunque no esté attachado)
                if (castVFXSpawnPoint != null) {
                    _currentCastVFX.transform.position = castVFXSpawnPoint.position;
                }

                // Rotar hacia la dirección del mouse
                if (channelDirection != Vector3.zero) {
                    Vector3 horizontalDirection = channelDirection;
                    horizontalDirection.y = 0;
                    if (horizontalDirection.sqrMagnitude > 0.001f) {
                        Quaternion targetRotation = Quaternion.LookRotation(horizontalDirection) * Quaternion.Euler(-90f, 0f, 0f);
                        _currentCastVFX.transform.rotation = targetRotation;
                    }
                }
            }

            // DETECTAR MOVIMIENTO y cancelar si no puede moverse mientras canalea
            if (!_pendingAbility.CanMoveWhileCasting && !_isInMovementGracePeriod) {
                float distanceMoved = Vector3.Distance(transform.position, _channelStartPosition);
                if (distanceMoved > 0.1f) { // Threshold de 0.1 unidades para evitar micro-movimientos
                    Debug.Log($"[PlayerCombat] Channeling interrupted by movement (moved {distanceMoved:F2}m)");
                    StopChanneling();
                    return;
                }
            }

            // Ejecutar tick de daño si es momento
            if (Time.time >= _nextChannelTick) {
                ExecuteChannelTick();
                _nextChannelTick = Time.time + _channelTickRate;
            }

            // Cancelar con Right Click o Escape
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) {
                StopChanneling();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) {
                StopChanneling();
                return;
            }
        }

        /// <summary>
        /// Ejecuta un tick de daño/efecto del channeling
        /// </summary>
        private void ExecuteChannelTick() {
            if (_pendingAbility == null) return;

            // Calcular target point basado en la dirección actual
            Vector3 direction = _pendingDirection;
            Vector3 targetPoint = transform.position + direction * _pendingAbility.Range;

            // Enviar tick al servidor
            CmdChannelTick(_pendingAbility.ID, targetPoint, direction);
        }

        /// <summary>
        /// Update casting progress and trigger events for cast bar
        /// </summary>
        private void HandleCastingUpdate() {
            if (_castDuration <= 0) return; // Instant cast

            float elapsed = Time.time - _castStartTime;
            float progress = Mathf.Clamp01(elapsed / _castDuration);
            float progressPercent = progress * 100f;

            // Trigger event for UI
            EventBus.Trigger<float, string>("OnCastProgress", progressPercent, _castingAbilityName);

            // Check for movement interruption (if can't move while casting)
            if (_pendingAbility != null && !_pendingAbility.CanMoveWhileCasting) {
                // Si el jugador se mueve, interrumpir el cast                                                                
                // TODO: Implementar detección de movimiento si es necesario
            }

            // Check for ESC key to cancel
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) {
                InterruptCast();
            }
        }

        /// <summary>
        /// Coroutine que espera el cast time antes de ejecutar la habilidad
        /// </summary>
        private System.Collections.IEnumerator CastTimeCoroutine(AbilityData ability) {
            float elapsed = 0f;

            while (elapsed < ability.CastTime) {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Cast completado - ejecutar habilidad
            if (_isDirectionalCast) {
                CmdCastAbilityDirectional(_pendingAbility.ID, _pendingTargetPoint, _pendingDirection);
            } else {
                CmdCastAbility(_pendingAbility.ID, _pendingTargetId, _pendingTargetPoint);
            }
        }

        /// <summary>
        /// Interrumpe el cast actual
        /// </summary>
        private void InterruptCast() {
            if (_currentCastCoroutine != null) {
                StopCoroutine(_currentCastCoroutine);
                _currentCastCoroutine = null;
            }

            _combatState = CombatState.Idle;
            EventBus.Trigger<string>("OnCombatStateChanged", _combatState.ToString());

            // Clear cast tracking
            _castDuration = 0f;
            _castingAbilityName = "";
            EventBus.Trigger<float, string>("OnCastProgress", 0f, ""); // Clear cast bar

            // Destruir Cast VFX
            DestroyCastVFX();

            _pendingAbility = null;

            Debug.Log("[PlayerCombat] Cast interrupted");
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

            // DECISIÓN ARQUITECTÓNICA CLAVE: Targeted vs Skillshot/Channeling
            if (ability.IndicatorType == IndicatorType.None) {
                // FLUJO LEGACY: Targeted ability (sistema actual - Fase 5)
                ExecuteTargetedAbility(ability);
            } else {
                // FLUJO NUEVO: Skillshot y Channeling (ambos requieren aiming)
                // Channeling: presionar tecla → aiming → left click → channeling
                // Skillshot: presionar tecla → aiming → left click → cast
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
            EventBus.Trigger<string>("OnCombatStateChanged", _combatState.ToString());

            // Inicializar tracking de cast
            _castStartTime = Time.time;
            _castDuration = ability.CastTime;
            _castingAbilityName = ability.Name;

            // Guardar datos para ejecutar después del cast
            _pendingAbility = ability;
            _pendingTargetId = targetId;
            _pendingTargetPoint = Vector3.zero;
            _isDirectionalCast = false;

            // SPAWNER CAST VFX (durante el casting)
            SpawnCastVFX(ability);

            // Si tiene cast time, esperar; si no, ejecutar inmediatamente
            if (ability.CastTime > 0) {
                _currentCastCoroutine = StartCoroutine(CastTimeCoroutine(ability));
            } else {
                // Instant cast
                CmdCastAbility(ability.ID, targetId, Vector3.zero);
            }
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
                DestroyCastVFX(); // Limpiar VFX antes de notificar fallo
                RpcCastFailed(base.Owner, "No mana");
                return;
            }

            if (!stats.ConsumeMana(ability.ManaCost)) {
                DestroyCastVFX(); // Limpiar VFX antes de notificar fallo
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
        // FLUJO CHANNELING: CHANNELING ABILITIES
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Coroutine para el grace period de movimiento
        /// </summary>
        private System.Collections.IEnumerator MovementGracePeriodCoroutine() {
            yield return new UnityEngine.WaitForSeconds(_movementGracePeriod);
            _isInMovementGracePeriod = false;
        }

        /// <summary>
        /// Detiene el channeling (usuario presionó tecla nuevamente)
        /// </summary>
        private void StopChanneling() {
            if (_combatState != CombatState.Channeling) return;

            // Limpiar estado
            _combatState = CombatState.Idle;
            EventBus.Trigger<string>("OnCombatStateChanged", _combatState.ToString());

            // Ocultar LineIndicator y desactivar modo channel
            if (indicatorSystem != null) {
                AbilityIndicator indicator = indicatorSystem.GetCurrentIndicator();
                if (indicator is LineIndicator lineIndicator) {
                    lineIndicator.SetChannelMode(false);
                }
                indicatorSystem.HideIndicator();
            }

            // Destruir VFX
            DestroyCastVFX();

            // Notificar servidor
            CmdStopChanneling();

            // Aplicar cooldown
            if (_pendingAbility != null) {
                _gcdEndTime = Time.time + _pendingAbility.GCD;
                _cooldowns[_pendingAbility.ID] = Time.time + _pendingAbility.Cooldown;

                EventBus.Trigger<int, float>("OnAbilityCooldownStart", _pendingAbility.ID, _pendingAbility.Cooldown);
            }

            _pendingAbility = null;

            Debug.Log("[PlayerCombat] Stopped Channeling");
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
            EventBus.Trigger<string>("OnCombatStateChanged", _combatState.ToString());

            // Mostrar indicador visual
            if (indicatorSystem != null) {
                indicatorSystem.ShowIndicator(ability, transform);
            } else {
                Debug.LogError("[PlayerCombat] AbilityIndicatorSystem is NULL! Assign in Inspector.");
                _combatState = CombatState.Idle;
                EventBus.Trigger<string>("OnCombatStateChanged", _combatState.ToString());
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

            // DECISIÓN: Channeling vs Skillshot Normal
            if (_pendingAbility.CastType == CastingType.Channeling) {
                // CHANNELING: No ocultar indicador, activar modo channel
                StartChannelingFromAiming();
            } else {
                // SKILLSHOT NORMAL: Ejecutar cast normal
                ExecuteSkillshotCast();
            }
        }

        /// <summary>
        /// Inicia channeling desde el modo aiming (después de confirmar con Left Click)
        /// </summary>
        private void StartChannelingFromAiming() {
            if (_pendingAbility == null || indicatorSystem == null) return;

            // Obtener dirección inicial antes de ocultar el indicador
            AbilityIndicator indicator = indicatorSystem.GetCurrentIndicator();
            Vector3 initialDirection = Vector3.forward;
            if (indicator != null) {
                initialDirection = indicator.GetDirection();
            }

            // Ocultar el indicador
            if (indicator is LineIndicator lineIndicator) {
                lineIndicator.SetChannelMode(false);
            }
            indicatorSystem.HideIndicator();

            // Cambiar a modo Channeling
            _combatState = CombatState.Channeling;
            EventBus.Trigger<string>("OnCombatStateChanged", _combatState.ToString());

            // Setup channeling timing
            _channelStartTime = Time.time;
            _nextChannelTick = Time.time + _pendingAbility.ChannelTickRate;
            _channelTickRate = _pendingAbility.ChannelTickRate;
            _channelMaxDuration = _pendingAbility.ChannelMaxDuration;

            // Guardar posición inicial para detectar movimiento
            _channelStartPosition = transform.position;

            // Guardar dirección inicial del channeling
            _pendingDirection = initialDirection;

            // Activar grace period
            _isInMovementGracePeriod = true;
            StartCoroutine(MovementGracePeriodCoroutine());

            // SPAWNER CHANNEL VFX
            SpawnCastVFX(_pendingAbility);

            // Notificar servidor
            CmdStartChanneling(_pendingAbility.ID);

            Debug.Log($"[PlayerCombat] Started Channeling from Aiming: {_pendingAbility.Name}");
        }

        /// <summary>
        /// Ejecuta un skillshot normal (no channeling)
        /// </summary>
        private void ExecuteSkillshotCast() {
            if (_pendingAbility == null || indicatorSystem == null) return;

            AbilityIndicator indicator = indicatorSystem.GetCurrentIndicator();
            if (indicator == null) return;

            // Obtener datos del indicador
            Vector3 targetPoint = indicator.GetTargetPoint();
            Vector3 direction = indicator.GetDirection();

            // Ocultar indicador
            indicatorSystem.HideIndicator();

            // Cambiar estado
            _combatState = CombatState.Casting;
            EventBus.Trigger<string>("OnCombatStateChanged", _combatState.ToString());

            // Inicializar tracking de cast
            _castStartTime = Time.time;
            _castDuration = _pendingAbility.CastTime;
            _castingAbilityName = _pendingAbility.Name;

            // Guardar datos para ejecutar después del cast
            _pendingTargetPoint = targetPoint;
            _pendingDirection = direction;
            _isDirectionalCast = true;

            // SPAWNER CAST VFX (durante el casting)
            SpawnCastVFX(_pendingAbility);

            // CLIENT PREDICTION PARA DASH
            // Si es un movimiento, debemos ejecutarlo localmente para que el Client Authoritative funcione
            if (_pendingAbility.IndicatorType == IndicatorType.Arrow) { // Arrow = Dash
                if (_pendingAbility.Logic != null) {
                    _pendingAbility.Logic.ExecuteDirectional(base.NetworkObject, targetPoint, direction, _pendingAbility);
                }
            }

            // Si tiene cast time, esperar; si no, ejecutar inmediatamente
            if (_pendingAbility.CastTime > 0) {
                _currentCastCoroutine = StartCoroutine(CastTimeCoroutine(_pendingAbility));
            } else {
                // Instant cast
                CmdCastAbilityDirectional(_pendingAbility.ID, targetPoint, direction);
                _pendingAbility = null;
            }
        }

        /// <summary>
        /// Cancela el aiming (usuario presionó Escape o click derecho)
        /// </summary>
        private void CancelAiming() {
            _combatState = CombatState.Idle;
            EventBus.Trigger<string>("OnCombatStateChanged", _combatState.ToString());

            // Clear cast tracking
            _castDuration = 0f;
            _castingAbilityName = "";
            EventBus.Trigger<float, string>("OnCastProgress", 0f, ""); // Clear cast bar

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
                DestroyCastVFX(); // Limpiar VFX antes de notificar fallo
                RpcCastFailed(base.Owner, "No mana");
                return;
            }

            if (!stats.ConsumeMana(ability.ManaCost)) {
                DestroyCastVFX(); // Limpiar VFX antes de notificar fallo
                RpcCastFailed(base.Owner, "No mana (Server Check)");
                return;
            }

            // VALIDACIÓN DE DISTANCIA (Anti-cheat)
            float distanceToTarget = Vector3.Distance(transform.position, targetPoint);
            if (distanceToTarget > ability.Range * 1.2f) { // 20% tolerance for lag
                DestroyCastVFX(); // Limpiar VFX antes de notificar fallo
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
        // CHANNELING RPCs
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// ServerRpc: Inicia channeling en el servidor
        /// </summary>
        [ServerRpc]
        private void CmdStartChanneling(int abilityId) {
            if (AbilityDatabase.Instance == null) return;

            AbilityData ability = AbilityDatabase.Instance.GetAbility(abilityId);
            if (ability == null) return;

            // Validaciones de servidor
            if (stats.CurrentMana < ability.ManaCost) {
                DestroyCastVFX();
                RpcCastFailed(base.Owner, "No mana");
                return;
            }

            if (!stats.ConsumeMana(ability.ManaCost)) {
                DestroyCastVFX();
                RpcCastFailed(base.Owner, "No mana (Server Check)");
                return;
            }

            Debug.Log($"[PlayerCombat] Server: Channeling started for {ability.Name}");
        }

        /// <summary>
        /// ServerRpc: Ejecuta un tick de channeling
        /// </summary>
        [ServerRpc]
        private void CmdChannelTick(int abilityId, Vector3 targetPoint, Vector3 direction) {
            if (AbilityDatabase.Instance == null) return;

            AbilityData ability = AbilityDatabase.Instance.GetAbility(abilityId);
            if (ability == null) return;

            // VALIDACIÓN: Verificar que el targetPoint esté dentro del rango
            float distance = Vector3.Distance(transform.position, targetPoint);
            if (distance > ability.Range * 1.2f) { // 20% tolerance
                // Clampear al rango máximo
                targetPoint = transform.position + direction.normalized * ability.Range;
            }

            // EJECUTAR LÓGICA DEL CHANNEL
            if (ability.Logic != null) {
                ability.Logic.ExecuteDirectional(base.NetworkObject, targetPoint, direction, ability);
            } else {
                Debug.LogError($"Habilidad {ability.Name} no tiene Logic asignado!");
            }
        }

        /// <summary>
        /// ServerRpc: Detiene el channeling
        /// </summary>
        [ServerRpc]
        private void CmdStopChanneling() {
            Debug.Log("[PlayerCombat] Server: Channeling stopped");
            // El servidor puede hacer cleanup adicional aquí si es necesario
        }

        // ═══════════════════════════════════════════════════════
        // RPCs (COMUNES A AMBOS SISTEMAS)
        // ═══════════════════════════════════════════════════════

        [ObserversRpc]
        private void RpcCastSuccess(int abilityId) {
            // Destruir Cast VFX (en todos los clientes, pero solo el servidor ejecutará el Despawn)
            DestroyCastVFX();

            // Reset state (solo para el owner)
            if (base.IsOwner) {
                // Stop cast coroutine if still running
                if (_currentCastCoroutine != null) {
                    StopCoroutine(_currentCastCoroutine);
                    _currentCastCoroutine = null;
                }

                _combatState = CombatState.Idle;
                EventBus.Trigger<string>("OnCombatStateChanged", _combatState.ToString());

                // Clear cast tracking
                _castDuration = 0f;
                _castingAbilityName = "";
                _pendingAbility = null;
                EventBus.Trigger<float, string>("OnCastProgress", 0f, ""); // Clear cast bar

                // Aplicar Cooldowns
                AbilityData ability = AbilityDatabase.Instance.GetAbility(abilityId);
                if (ability != null) {
                    _gcdEndTime = Time.time + ability.GCD;
                    _cooldowns[ability.ID] = Time.time + ability.Cooldown;

                    Debug.Log($"[PlayerCombat] {ability.Name} cast successful. CD: {ability.Cooldown}s");

                    // Trigger EventBus events for UI
                    EventBus.Trigger<int, string>("OnAbilityCast", abilityId, ability.Name);
                    EventBus.Trigger<int, float>("OnAbilityCooldownStart", abilityId, ability.Cooldown);
                }
            }

            // Play Animation trigger (en todos los clientes)
            if (animator != null) animator.SetTrigger("Cast");
        }

        [TargetRpc]
        private void RpcCastFailed(NetworkConnection conn, string reason) {
            // Stop cast coroutine if still running
            if (_currentCastCoroutine != null) {
                StopCoroutine(_currentCastCoroutine);
                _currentCastCoroutine = null;
            }

            _combatState = CombatState.Idle;
            EventBus.Trigger<string>("OnCombatStateChanged", _combatState.ToString());
            Debug.LogWarning($"[PlayerCombat] Cast failed: {reason}");

            // Clear cast tracking
            _castDuration = 0f;
            _castingAbilityName = "";
            EventBus.Trigger<float, string>("OnCastProgress", 0f, ""); // Clear cast bar

            // Destruir Cast VFX
            DestroyCastVFX();

            // Trigger EventBus event for UI (if we have a pending ability)
            if (_pendingAbility != null) {
                EventBus.Trigger<int, string>("OnAbilityFailed", _pendingAbility.ID, reason);
            }

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

        // ═══════════════════════════════════════════════════════
        // CAST VFX HELPERS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Spawna el VFX de casting (se muestra DURANTE el cast)
        /// NETWORKED: Visible para todos los jugadores
        /// </summary>
        private void SpawnCastVFX(AbilityData ability) {
            if (ability.CastVFX == null) {
                Debug.LogWarning($"[PlayerCombat] {ability.Name} no tiene CastVFX asignado");
                return;
            }

            // Destruir VFX anterior si existe
            DestroyCastVFX();

            // Determinar posición y rotación de spawn
            Vector3 spawnPos;
            Quaternion spawnRot;

            if (castVFXSpawnPoint != null) {
                // Usar el spawn point asignado
                spawnPos = castVFXSpawnPoint.position;
                spawnRot = castVFXSpawnPoint.rotation * Quaternion.Euler(-90f, 0f, 0f); // X=-90 offset
            } else {
                // Fallback: 1 metro arriba del jugador
                spawnPos = transform.position + Vector3.up * 1f;
                spawnRot = Quaternion.identity * Quaternion.Euler(-90f, 0f, 0f); // X=-90 offset
                Debug.LogWarning("[PlayerCombat] castVFXSpawnPoint no asignado, usando posición default");
            }

            // Determinar si debe ser hijo del spawn point (para seguir al jugador)
            // IMPORTANTE: Para channeling, NO attachear porque rotaremos el VFX manualmente cada frame
            bool isChanneling = (ability.CastType == CastingType.Channeling);
            bool attachToPlayer = castVFXSpawnPoint != null && !isChanneling;

            // Solicitar spawn en red via ServerRpc
            CmdSpawnCastVFX(ability.CastVFX, spawnPos, spawnRot, ability.CastTime, attachToPlayer);

            Debug.Log($"[PlayerCombat] Requesting CastVFX spawn for {ability.Name} at {spawnPos} (CastTime: {ability.CastTime}s, Attached: {attachToPlayer}, Channeling: {isChanneling})");
        }

        /// <summary>
        /// ServerRpc: Spawna el VFX en red
        /// </summary>
        [ServerRpc]
        private void CmdSpawnCastVFX(GameObject vfxPrefab, Vector3 position, Quaternion rotation, float castTime, bool attachToPlayer) {
            if (vfxPrefab == null) return;

            // Instanciar el VFX con la posición y rotación especificada
            GameObject vfxInstance = Instantiate(vfxPrefab, position, rotation);

            // Si debe attachear al spawn point, hacerlo como hijo
            if (attachToPlayer && castVFXSpawnPoint != null) {
                vfxInstance.transform.SetParent(castVFXSpawnPoint);
            }

            // Si tiene NetworkObject, spawnearlo en red
            NetworkObject nob = vfxInstance.GetComponent<NetworkObject>();
            if (nob != null) {
                FishNet.InstanceFinder.ServerManager.Spawn(vfxInstance, base.Owner);

                // Notificar a todos los clientes (incluyendo owner) sobre el VFX spawneado
                RpcSetCastVFX(nob, castTime, attachToPlayer);
            } else {
                // Si no tiene NetworkObject, solo es local del servidor (no se verá en clientes)
                Debug.LogWarning("[PlayerCombat] CastVFX no tiene NetworkObject, no se sincronizará en red");
                Destroy(vfxInstance);
            }
        }

        /// <summary>
        /// ObserversRpc: Guarda referencia al VFX spawneado y programa destrucción
        /// </summary>
        [ObserversRpc]
        private void RpcSetCastVFX(NetworkObject vfx, float castTime, bool attachToPlayer) {
            _currentCastVFX = vfx;

            // Activar el VFX si está desactivado (el prefab tiene el hijo desactivado)
            if (vfx != null && vfx.gameObject != null) {
                // Activar todos los hijos
                foreach (Transform child in vfx.transform) {
                    child.gameObject.SetActive(true);
                }

                // Si debe attachear al spawn point, hacerlo en todos los clientes
                if (attachToPlayer && castVFXSpawnPoint != null) {
                    vfx.transform.SetParent(castVFXSpawnPoint);
                    // Resetear posición y rotación local (para que use el transform del spawn point)
                    vfx.transform.localPosition = Vector3.zero;
                    vfx.transform.localRotation = Quaternion.identity;
                }
            }

            Debug.Log($"[PlayerCombat] CastVFX set on client (CastTime: {castTime}s, Attached: {attachToPlayer})");

            // Para instant casts, programar destrucción rápida
            if (castTime == 0 && base.IsServer) {
                StartCoroutine(DestroyCastVFXDelayed(0.5f));
            }
        }

        /// <summary>
        /// Coroutine para destruir VFX después de un delay
        /// </summary>
        private System.Collections.IEnumerator DestroyCastVFXDelayed(float delay) {
            yield return new UnityEngine.WaitForSeconds(delay);
            DestroyCastVFX();
        }

        /// <summary>
        /// Destruye el VFX de casting actual (si existe)
        /// NETWORKED: Solo el servidor puede despawnear NetworkObjects
        /// </summary>
        private void DestroyCastVFX() {
            if (_currentCastVFX != null) {
                Debug.Log("[PlayerCombat] Destroying CastVFX");

                // Solo el servidor puede despawnear NetworkObjects
                if (base.IsServer) {
                    FishNet.InstanceFinder.ServerManager.Despawn(_currentCastVFX.gameObject);
                } else {
                    // Si somos cliente, pedir al servidor que lo despawnee
                    CmdDestroyCastVFX();
                }

                // Limpiar referencia en todos los clientes
                _currentCastVFX = null;
            }
        }

        /// <summary>
        /// ServerRpc: Despawnea el VFX desde el servidor
        /// </summary>
        [ServerRpc]
        private void CmdDestroyCastVFX() {
            if (_currentCastVFX != null) {
                FishNet.InstanceFinder.ServerManager.Despawn(_currentCastVFX.gameObject);
                _currentCastVFX = null;
            }
        }
    }
}

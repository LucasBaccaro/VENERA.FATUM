using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Genesis.Core;
using Genesis.Simulation.Combat;
using Genesis.Data;
using Genesis.Simulation.World;
using FishNet.Connection;

namespace Genesis.Simulation {

    /// <summary>
    /// Stats del jugador (HP, Mana, Shields) sincronizados por red.
    /// Implementa IDamageable para recibir daño.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerStats : NetworkBehaviour, IDamageable {

        [Header("Base Stats (Synced)")]
        private readonly SyncVar<float> _maxHealth = new SyncVar<float>(800f);
        private readonly SyncVar<float> _maxMana = new SyncVar<float>(800f);
        [SerializeField] private float manaRegenPerSecond = 5f;
        [SerializeField] private float healthRegenPerSecond = 2f;

        [Header("Current Stats (Synced - FishNet v4)")]
        // FishNet v4: Usar SyncVar<T> en lugar de [SyncVar]
        private readonly SyncVar<float> _currentHealth = new SyncVar<float>(800f);
        private readonly SyncVar<float> _currentMana = new SyncVar<float>(800f);
        private readonly SyncVar<float> _currentShield = new SyncVar<float>(0f);

        // Estado
        private bool _isDead;
        private float _lastDamageTime;
        private const float OOC_DELAY = 5f;

        // Referencias
        private StatusEffectSystem _statusSystem;
        private PlayerAttributes _attributes;

        void Awake() {
             _statusSystem = GetComponent<StatusEffectSystem>();
             _attributes = GetComponent<PlayerAttributes>();
        }

        // ═══════════════════════════════════════════════════════
        // PROPERTIES (Public Read-Only)
        // ═══════════════════════════════════════════════════════

        public float CurrentHealth => _currentHealth.Value;
        public float MaxHealth => _maxHealth.Value;
        public float CurrentMana => _currentMana.Value;
        public float MaxMana => _maxMana.Value;
        public float CurrentShield => _currentShield.Value;
        // public bool IsDead => _isDead; // REMOVIDO: Ya implementado abajo en la región de IDamageable

        // ═══════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════

        public override void OnStartNetwork() {
            base.OnStartNetwork();

            // Suscribirse a cambios de SyncType (FishNet v4)
            _currentHealth.OnChange += OnHealthChanged;
            _currentMana.OnChange += OnManaChanged;
            _currentShield.OnChange += OnShieldChanged;
            
            // También suscribirse a cambios de Max para actualizar UI
            _maxHealth.OnChange += (oldVal, newVal, asServer) => OnHealthChanged(_currentHealth.Value, _currentHealth.Value, asServer);
            _maxMana.OnChange += (oldVal, newVal, asServer) => OnManaChanged(_currentMana.Value, _currentMana.Value, asServer);
        }

        public override void OnStartServer() {
            base.OnStartServer();

            // Inicializar con stats completos (será sobrescrito por PlayerClassManager si existe)
            _currentHealth.Value = _maxHealth.Value;
            _currentMana.Value = _maxMana.Value;
            _currentShield.Value = 0f;
            _isDead = false;
        }

        [Server]
        public void InitializeFromClass(ClassData data) {
            _maxHealth.Value = data.MaxHealth;
            _maxMana.Value = data.MaxMana;
            manaRegenPerSecond = data.ManaRegenPerSecond;
            healthRegenPerSecond = data.HealthRegenPerSecond;

            // Al cambiar de clase, sanamos al jugador y le damos maná completo
            _currentHealth.Value = _maxHealth.Value;
            _currentMana.Value = _maxMana.Value;
            _isDead = false;

            // Notify attributes to recalculate derived stats
            if (_attributes != null) {
                _attributes.RecalculateDerivedStats();
            }

            Debug.Log($"[PlayerStats] Class updated: {data.ClassName}. Stats re-initialized.");
        }

        void Update() {
            if (!base.IsServer) return;

            // Regenerar maná pasivamente (base + WIS bonus)
            float totalManaRegen = manaRegenPerSecond + (_attributes != null ? _attributes.BonusManaRegen : 0f);
            if (_currentMana.Value < _maxMana.Value) {
                _currentMana.Value = Mathf.Min(_currentMana.Value + totalManaRegen * Time.deltaTime, _maxMana.Value);
            }

            // Health regen out of combat (base + CON bonus)
            float totalHealthRegen = healthRegenPerSecond + (_attributes != null ? _attributes.HealthRegenOOC : 0f);
            if (!_isDead
                && totalHealthRegen > 0f
                && Time.time - _lastDamageTime > OOC_DELAY
                && _currentHealth.Value < _maxHealth.Value) {
                _currentHealth.Value = Mathf.Min(
                    _currentHealth.Value + totalHealthRegen * Time.deltaTime,
                    _maxHealth.Value);
            }
        }

        // ═══════════════════════════════════════════════════════
        // IDAMAGEABLE IMPLEMENTATION
        // ═══════════════════════════════════════════════════════

        [Server]
        public void TakeDamage(float damage, NetworkObject attacker) {
            if (_isDead) return;

            // ═══ SAFE ZONE VALIDATION ═══
            if (!CombatValidator.CanApplyDamage(base.NetworkObject, attacker, out string reason)) {
                Debug.Log($"[PlayerStats] Damage blocked: {reason}");
                return;
            }

            // ═══ STATUS EFFECTS CHECK ═══
            if (_statusSystem != null) {
                // 1. Invulnerable: Ignorar todo el daño
                if (_statusSystem.HasEffect(EffectType.Invulnerable)) {
                    // Debug.Log($"[PlayerStats] Daño ignorado por Invulnerable");
                    return;
                }

                // 2. Reflect: Bloquear daño y devolverlo al atacante
                if (_statusSystem.HasEffect(EffectType.Reflect)) {
                    // Debug.Log($"[PlayerStats] Daño bloqueado por Reflect");
                    
                    if (attacker != null) {
                        PlayerStats attackerStats = attacker.GetComponent<PlayerStats>();
                        if (attackerStats != null) {
                            // PREVENCIÓN DE BUCLE INFINITO:
                            // Si el atacante TAMBIÉN tiene Reflect, no devolvemos el daño (empate técnico).
                            // Evita crash por StackOverflow si dos jugadores con Reflect se atacan.
                            StatusEffectSystem attackerSem = attacker.GetComponent<StatusEffectSystem>();
                            if (attackerSem != null && attackerSem.HasEffect(EffectType.Reflect)) {
                                Debug.Log("[PlayerStats] Reflect vs Reflect: Daño anulado.");
                            } else {
                                // Reflejar daño
                                attackerStats.TakeDamage(damage, base.NetworkObject);
                                Debug.Log($"[PlayerStats] Daño reflejado a {attacker.name}");
                            }
                        }
                    }
                    return; // No recibir daño
                }
            }

            // Track last damage time for OOC regen
            _lastDamageTime = Time.time;

            // ═══ PASO 1: Absorber con Shield ═══
            if (_currentShield.Value > 0) {
                float shieldAbsorbed = Mathf.Min(damage, _currentShield.Value);
                _currentShield.Value -= shieldAbsorbed;
                damage -= shieldAbsorbed;

                if (attacker != null && attacker.Owner.IsValid) {
                    TargetShowDamageText(attacker.Owner, $"{shieldAbsorbed:F0}", "shield");
                }
            }

            // LIMPIEZA DE ESCUDO (Chequeo robusto fuera del if anterior)
            // Si el valor del escudo es 0 y tenemos un StatusEffectSystem
            if (_currentShield.Value <= 0.01f && _statusSystem != null) {
                // Verificar si tiene el efecto antes de intentar removerlo (para no spammear logs/RPCs)
                if (_statusSystem.HasEffect(EffectType.Shield)) {
                    Debug.Log($"[PlayerStats] Shield depleted! Cleaning up visual effects.");
                    _statusSystem.RemoveEffectsOfType(EffectType.Shield);
                }
            }

            if (damage <= 0) return; // Shield absorbió todo y no sobra daño

            // ═══ PASO 2: Daño a HP ═══
            _currentHealth.Value = Mathf.Max(0, _currentHealth.Value - damage);

            if (attacker != null && attacker.Owner.IsValid) {
                TargetShowDamageText(attacker.Owner, $"{damage:F0}", "damage");
            }

            // ═══ PASO 3: Check Death ═══
            if (_currentHealth.Value <= 0) {
                Die(attacker);
            }
        }

        /// <summary>
        /// Overload: TakeDamage with DamageResultType for differentiated floating text.
        /// </summary>
        [Server]
        public void TakeDamage(float damage, NetworkObject attacker, DamageResultType resultType) {
            if (_isDead) return;

            // Safe zone validation
            if (!CombatValidator.CanApplyDamage(base.NetworkObject, attacker, out string reason)) {
                Debug.Log($"[PlayerStats] Damage blocked: {reason}");
                return;
            }

            // Status effects check
            if (_statusSystem != null) {
                if (_statusSystem.HasEffect(EffectType.Invulnerable)) return;
                if (_statusSystem.HasEffect(EffectType.Reflect)) {
                    if (attacker != null) {
                        PlayerStats attackerStats = attacker.GetComponent<PlayerStats>();
                        if (attackerStats != null) {
                            StatusEffectSystem attackerSem = attacker.GetComponent<StatusEffectSystem>();
                            if (attackerSem != null && attackerSem.HasEffect(EffectType.Reflect)) {
                                Debug.Log("[PlayerStats] Reflect vs Reflect: Damage cancelled.");
                            } else {
                                attackerStats.TakeDamage(damage, base.NetworkObject, resultType);
                            }
                        }
                    }
                    return;
                }
            }

            // Track last damage time for OOC regen
            _lastDamageTime = Time.time;

            // Shield absorption
            if (_currentShield.Value > 0) {
                float shieldAbsorbed = Mathf.Min(damage, _currentShield.Value);
                _currentShield.Value -= shieldAbsorbed;
                damage -= shieldAbsorbed;

                if (attacker != null && attacker.Owner.IsValid) {
                    TargetShowDamageText(attacker.Owner, $"{shieldAbsorbed:F0}", "shield");
                }
            }

            if (_currentShield.Value <= 0.01f && _statusSystem != null) {
                if (_statusSystem.HasEffect(EffectType.Shield)) {
                    _statusSystem.RemoveEffectsOfType(EffectType.Shield);
                }
            }

            if (damage <= 0) return;

            _currentHealth.Value = Mathf.Max(0, _currentHealth.Value - damage);

            // Determine text type based on result
            string textType = "damage";
            bool isCritical = false;
            switch (resultType) {
                case DamageResultType.Critical:
                    textType = "critical";
                    isCritical = true;
                    break;
                case DamageResultType.Overpower:
                    textType = "overpower";
                    isCritical = true;
                    break;
            }

            if (attacker != null && attacker.Owner.IsValid) {
                TargetShowDamageText(attacker.Owner, $"{damage:F0}", textType, isCritical);
            }

            if (_currentHealth.Value <= 0) {
                Die(attacker);
            }
        }

        public NetworkObject GetNetworkObject() => base.NetworkObject;
        public bool IsDead => _isDead;
        
        // IDamageable Legacy Support
        public bool IsAlive() => !_isDead;

        public float GetCurrentHealth() => _currentHealth.Value;
        public float GetMaxHealth() => _maxHealth.Value;

        // ═══════════════════════════════════════════════════════
        // HEALING
        // ═══════════════════════════════════════════════════════

        [Server]
        public void Heal(float amount, NetworkObject healer = null) {
            if (_isDead) return;

            float healAmount = Mathf.Min(amount, _maxHealth.Value - _currentHealth.Value);
            _currentHealth.Value += healAmount;

            if (base.Owner.IsValid) {
                TargetShowDamageText(base.Owner, $"+{healAmount:F0}", "heal");
            }

            EventBus.Trigger("OnPlayerHealed", healAmount);
        }

        /// <summary>
        /// Alias para Heal (usado por algunas habilidades)
        /// </summary>
        [Server]
        public void RestoreHealth(float amount) => Heal(amount);

        // ═══════════════════════════════════════════════════════
        // MANA MANAGEMENT
        // ═══════════════════════════════════════════════════════

        [Server]
        public bool ConsumeMana(float amount) {
            if (_currentMana.Value < amount) {
                return false; // No hay suficiente maná
            }

            _currentMana.Value -= amount;
            return true;
        }

        [Server]
        public void RestoreMana(float amount) {
            if (_isDead) return;

            float restoreAmount = Mathf.Min(amount, _maxMana.Value - _currentMana.Value);
            _currentMana.Value += restoreAmount;

            // Show floating text for mana restoration
            if (base.Owner.IsValid) {
                TargetShowDamageText(base.Owner, $"+{restoreAmount:F0}", "mana");
            }

            EventBus.Trigger("OnPlayerManaRestored", restoreAmount);
        }

        // ═══════════════════════════════════════════════════════
        // MAX STATS SETTERS (For Equipment System - Phase 9)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Set max health (called by EquipmentManager)
        /// </summary>
        [Server]
        public void SetMaxHealth(float value) {
            float oldMax = _maxHealth.Value;
            _maxHealth.Value = value;

            // Scale current HP proportionally when max increases
            if (value > oldMax && oldMax > 0f) {
                _currentHealth.Value = (_currentHealth.Value / oldMax) * value;
            }

            // Clamp current health if it exceeds new max
            if (_currentHealth.Value > _maxHealth.Value) {
                _currentHealth.Value = _maxHealth.Value;
            }
        }

        /// <summary>
        /// Set max mana (called by EquipmentManager)
        /// </summary>
        [Server]
        public void SetMaxMana(float value) {
            float oldMax = _maxMana.Value;
            _maxMana.Value = value;

            // Scale current mana proportionally when max increases
            if (value > oldMax && oldMax > 0f) {
                _currentMana.Value = (_currentMana.Value / oldMax) * value;
            }

            // Clamp current mana if it exceeds new max
            if (_currentMana.Value > _maxMana.Value) {
                _currentMana.Value = _maxMana.Value;
            }
        }

        // ═══════════════════════════════════════════════════════
        // SHIELD MANAGEMENT
        // ═══════════════════════════════════════════════════════

        [Server]
        public void AddShield(float amount) {
            _currentShield.Value += amount;
        }

        [Server]
        public void RemoveShield(float amount) {
            _currentShield.Value = Mathf.Max(0, _currentShield.Value - amount);
        }

        // ═══════════════════════════════════════════════════════
        // DEATH
        // ═══════════════════════════════════════════════════════

        [Server]
        private void Die(NetworkObject killer) {
            if (_isDead) return;

            _isDead = true;

            Debug.Log($"[PlayerStats] {gameObject.name} ha muerto. Killer: {(killer != null ? killer.name : "Unknown")}");

            // Trigger eventos
            EventBus.Trigger("OnPlayerDied", base.NetworkObject, killer);

            // Notificar clientes
            RpcOnDeath();

            // TODO: Lógica de respawn (Fase 9)
        }

        [ObserversRpc]
        private void RpcOnDeath() {
            // Animación de muerte
            var animator = GetComponent<Animator>();
            if (animator != null) {
                animator.SetTrigger("Die");
            }

            // Efecto visual
            Debug.Log($"[PlayerStats] Cliente: {gameObject.name} murió");
        }

        // ═══════════════════════════════════════════════════════
        // SYNCTYPE CALLBACKS (Para UI - FishNet v4)
        // ═══════════════════════════════════════════════════════

        private void OnHealthChanged(float oldValue, float newValue, bool asServer) {
            // Trigger evento para UI (solo en clientes)
            if (base.IsOwner) {
                EventBus.Trigger("OnHealthChanged", newValue, _maxHealth.Value);
            }
        }

        private void OnManaChanged(float oldValue, float newValue, bool asServer) {
            // Trigger evento para UI (solo en clientes)
            if (base.IsOwner) {
                EventBus.Trigger("OnManaChanged", newValue, _maxMana.Value);
            }
        }

        private void OnShieldChanged(float oldValue, float newValue, bool asServer) {
            // Trigger evento para UI (solo en clientes)
            if (base.IsOwner) {
                EventBus.Trigger("OnShieldChanged", newValue);
            }
        }

        // ═══════════════════════════════════════════════════════
        // RPC - FLOATING TEXT
        // ═══════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════
        // RPC - FLOATING TEXT (Visibilidad para el atacante)
        // ═══════════════════════════════════════════════════════

        [TargetRpc]
        private void TargetShowDamageText(FishNet.Connection.NetworkConnection conn, string text, string type, bool isCritical = false) {
            Debug.Log($"[PlayerStats] TargetShowDamageText received on client: {text} ({type})");
            // Desacoplado: Usar EventBus con un struct de datos
            var data = new Genesis.Data.FloatingTextData(transform.position + Vector3.up * 1.5f, text, type, isCritical);
            EventBus.Trigger("OnShowFloatingText", data);
        }

        [ObserversRpc]
        private void RpcShowDamageText(string text, Color color) {
            // Obsoleto, migrado a TargetShowDamageText
        }

        // ═══════════════════════════════════════════════════════
        // DEBUG
        // ═══════════════════════════════════════════════════════

#if UNITY_EDITOR
        [ContextMenu("Take 20 Damage")]
        private void DebugTakeDamage() {
            if (base.IsServer) {
                TakeDamage(20f, null);
            }
        }

        [ContextMenu("Heal 30")]
        private void DebugHeal() {
            if (base.IsServer) {
                Heal(30f);
            }
        }

        [ContextMenu("Add 50 Shield")]
        private void DebugAddShield() {
            if (base.IsServer) {
                AddShield(50f);
            }
        }
#endif
    }
}

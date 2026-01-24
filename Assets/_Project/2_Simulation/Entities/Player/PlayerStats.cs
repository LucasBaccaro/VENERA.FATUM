using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Genesis.Core;
using Genesis.Simulation.Combat; // Necesario para IDamageable
using Genesis.Data;

namespace Genesis.Simulation {

    /// <summary>
    /// Stats del jugador (HP, Mana, Shields) sincronizados por red.
    /// Implementa IDamageable para recibir daño.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerStats : NetworkBehaviour, IDamageable {

        [Header("Base Stats")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float maxMana = 100f;
        [SerializeField] private float manaRegenPerSecond = 5f;

        [Header("Current Stats (Synced - FishNet v4)")]
        // FishNet v4: Usar SyncVar<T> en lugar de [SyncVar]
        private readonly SyncVar<float> _currentHealth = new SyncVar<float>(100f);
        private readonly SyncVar<float> _currentMana = new SyncVar<float>(100f);
        private readonly SyncVar<float> _currentShield = new SyncVar<float>(0f);

        // Estado
        private bool _isDead;

        // Referencias
        private StatusEffectSystem _statusSystem;

        void Awake() {
             _statusSystem = GetComponent<StatusEffectSystem>();
        }

        // ═══════════════════════════════════════════════════════
        // PROPERTIES (Public Read-Only)
        // ═══════════════════════════════════════════════════════

        public float CurrentHealth => _currentHealth.Value;
        public float MaxHealth => maxHealth;
        public float CurrentMana => _currentMana.Value;
        public float MaxMana => maxMana;
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
        }

        public override void OnStartServer() {
            base.OnStartServer();

            // Inicializar con stats completos
            _currentHealth.Value = maxHealth;
            _currentMana.Value = maxMana;
            _currentShield.Value = 0f;
            _isDead = false;
        }

        void Update() {
            if (!base.IsServer) return;

            // Regenerar maná pasivamente
            if (_currentMana.Value < maxMana) {
                _currentMana.Value = Mathf.Min(_currentMana.Value + manaRegenPerSecond * Time.deltaTime, maxMana);
            }
        }

        // ═══════════════════════════════════════════════════════
        // IDAMAGEABLE IMPLEMENTATION
        // ═══════════════════════════════════════════════════════

        [Server]
        public void TakeDamage(float damage, NetworkObject attacker) {
            if (_isDead) return;

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

            // ═══ PASO 1: Absorber con Shield ═══
            if (_currentShield.Value > 0) {
                float shieldAbsorbed = Mathf.Min(damage, _currentShield.Value);
                _currentShield.Value -= shieldAbsorbed;
                damage -= shieldAbsorbed;

                RpcShowDamageText($"{shieldAbsorbed:F0} (SHIELD)", new Color(0.5f, 0.8f, 1f));
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

            RpcShowDamageText($"-{damage:F0}", Color.red);

            // ═══ PASO 3: Check Death ═══
            if (_currentHealth.Value <= 0) {
                Die(attacker);
            }
        }

        public NetworkObject GetNetworkObject() => base.NetworkObject;
        public bool IsDead => _isDead;
        
        // IDamageable Legacy Support
        public bool IsAlive() => !_isDead;

        public float GetCurrentHealth() => _currentHealth.Value;
        public float GetMaxHealth() => maxHealth;

        // ═══════════════════════════════════════════════════════
        // HEALING
        // ═══════════════════════════════════════════════════════

        [Server]
        public void Heal(float amount) {
            if (_isDead) return;

            float healAmount = Mathf.Min(amount, maxHealth - _currentHealth.Value);
            _currentHealth.Value += healAmount;

            RpcShowDamageText($"+{healAmount:F0}", Color.green);

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
            _currentMana.Value = Mathf.Min(_currentMana.Value + amount, maxMana);
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
                EventBus.Trigger("OnHealthChanged", newValue, maxHealth);
            }
        }

        private void OnManaChanged(float oldValue, float newValue, bool asServer) {
            // Trigger evento para UI (solo en clientes)
            if (base.IsOwner) {
                EventBus.Trigger("OnManaChanged", newValue, maxMana);
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

        [ObserversRpc]
        private void RpcShowDamageText(string text, Color color) {
            // TODO: Implementar FloatingText en FASE 11
            // Por ahora solo log
            Debug.Log($"[Damage] {gameObject.name}: {text}");
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

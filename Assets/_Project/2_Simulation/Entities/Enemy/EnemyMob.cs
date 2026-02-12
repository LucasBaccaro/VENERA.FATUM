using UnityEngine;
using UnityEngine.AI;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Genesis.Core;
using Genesis.Data;
using Genesis.Simulation.Combat;
using FishNet.Connection;

namespace Genesis.Simulation {

    [RequireComponent(typeof(NetworkObject))]
    public class EnemyMob : NetworkBehaviour, IDamageable {

        [Header("Data")]
        [SerializeField] private EnemyData _data;

        // Synced state
        private readonly SyncVar<float> _currentHealth = new SyncVar<float>(100f);
        private readonly SyncVar<bool> _isDead = new SyncVar<bool>(false);

        // AI state (server only)
        private enum AIState { Idle, Aggro, Attack, Return }
        private AIState _aiState = AIState.Idle;
        private Transform _target;
        private NetworkObject _targetNob;
        private Vector3 _spawnPosition;
        private float _lastAttackTime;
        private NavMeshAgent _agent;

        // Return leash
        private const float LEASH_RANGE = 20f;
        private const float RETURN_THRESHOLD = 1f;

        public string EnemyTag => _data != null ? _data.EnemyTag : "";
        public string DisplayName => _data != null ? _data.DisplayName : gameObject.name;
        public float CurrentHealthValue => _currentHealth.Value;
        public float MaxHealthValue => _data != null ? _data.MaxHealth : 100f;

        public override void OnStartNetwork() {
            base.OnStartNetwork();
            _currentHealth.OnChange += OnHealthSyncChanged;
            _isDead.OnChange += OnDeadSyncChanged;
        }

        public override void OnStartServer() {
            base.OnStartServer();

            _agent = GetComponent<NavMeshAgent>();
            if (_agent != null) {
                _agent.speed = _data != null ? _data.MoveSpeed : 3f;
                _agent.stoppingDistance = _data != null ? _data.AttackRange * 0.8f : 1.5f;
            }

            _spawnPosition = transform.position;

            if (_data != null) {
                _currentHealth.Value = _data.MaxHealth;
                gameObject.name = _data.DisplayName;
                RpcSetDisplayName(_data.DisplayName);
            }
        }

        [ObserversRpc(BufferLast = true)]
        private void RpcSetDisplayName(string displayName) {
            gameObject.name = displayName;
        }

        private void Update() {
            if (!base.IsServerInitialized || _isDead.Value) return;
            ServerAIUpdate();
        }

        // ═══════════════════════════════════════════════════════
        // AI (Server Only)
        // ═══════════════════════════════════════════════════════

        private void ServerAIUpdate() {
            switch (_aiState) {
                case AIState.Idle:
                    ScanForTargets();
                    break;
                case AIState.Aggro:
                    ChaseTarget();
                    break;
                case AIState.Attack:
                    AttackTarget();
                    break;
                case AIState.Return:
                    ReturnToSpawn();
                    break;
            }
        }

        private void ScanForTargets() {
            float detectionRange = _data != null ? _data.DetectionRange : 8f;
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange);

            float closestDist = float.MaxValue;
            Transform closest = null;
            NetworkObject closestNob = null;

            foreach (var hit in hits) {
                var playerStats = hit.GetComponent<PlayerStats>();
                if (playerStats != null && playerStats.IsAlive()) {
                    float dist = Vector3.Distance(transform.position, hit.transform.position);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = hit.transform;
                        closestNob = hit.GetComponent<NetworkObject>();
                    }
                }
            }

            if (closest != null) {
                _target = closest;
                _targetNob = closestNob;
                _aiState = AIState.Aggro;
            }
        }

        private void ChaseTarget() {
            if (_target == null || !IsTargetValid()) {
                LoseTarget();
                return;
            }

            // Leash check
            float distFromSpawn = Vector3.Distance(transform.position, _spawnPosition);
            if (distFromSpawn > LEASH_RANGE) {
                LoseTarget();
                _aiState = AIState.Return;
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _target.position);
            float attackRange = _data != null ? _data.AttackRange : 2f;

            if (distToTarget <= attackRange) {
                _aiState = AIState.Attack;
                if (_agent != null) _agent.ResetPath();
                return;
            }

            // Move toward target
            if (_agent != null && _agent.isOnNavMesh) {
                _agent.SetDestination(_target.position);
            } else {
                // Fallback: direct movement
                Vector3 direction = (_target.position - transform.position).normalized;
                float speed = _data != null ? _data.MoveSpeed : 3f;
                transform.position += direction * speed * Time.deltaTime;
                if (direction != Vector3.zero) {
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }
        }

        private void AttackTarget() {
            if (_target == null || !IsTargetValid()) {
                LoseTarget();
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _target.position);
            float attackRange = _data != null ? _data.AttackRange : 2f;

            if (distToTarget > attackRange * 1.2f) {
                _aiState = AIState.Aggro;
                return;
            }

            // Face target
            Vector3 lookDir = (_target.position - transform.position);
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.001f) {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }

            float cooldown = _data != null ? _data.AttackCooldown : 2f;
            if (Time.time - _lastAttackTime >= cooldown) {
                _lastAttackTime = Time.time;
                float damage = _data != null ? Random.Range(_data.MinDamage, _data.MaxDamage) : 10f;

                var damageable = _target.GetComponent<IDamageable>();
                if (damageable != null) {
                    damageable.TakeDamage(damage, base.NetworkObject);
                }

                RpcPlayAttackAnimation();
            }
        }

        private void ReturnToSpawn() {
            float dist = Vector3.Distance(transform.position, _spawnPosition);
            if (dist <= RETURN_THRESHOLD) {
                _aiState = AIState.Idle;
                // Heal to full on return
                if (_data != null) _currentHealth.Value = _data.MaxHealth;
                if (_agent != null) _agent.ResetPath();
                return;
            }

            if (_agent != null && _agent.isOnNavMesh) {
                _agent.SetDestination(_spawnPosition);
            } else {
                Vector3 direction = (_spawnPosition - transform.position).normalized;
                float speed = _data != null ? _data.MoveSpeed : 3f;
                transform.position += direction * speed * Time.deltaTime;
            }

            // Check if a player attacks during return
            ScanForTargets();
        }

        private bool IsTargetValid() {
            if (_target == null) return false;
            var stats = _target.GetComponent<PlayerStats>();
            return stats != null && stats.IsAlive();
        }

        private void LoseTarget() {
            _target = null;
            _targetNob = null;
            _aiState = AIState.Return;
        }

        // ═══════════════════════════════════════════════════════
        // IDamageable
        // ═══════════════════════════════════════════════════════

        public void TakeDamage(float damage, NetworkObject attacker) {
            if (!base.IsServerInitialized || _isDead.Value) return;

            _currentHealth.Value = Mathf.Max(0, _currentHealth.Value - damage);

            // Mostrar floating text al atacante (si es un jugador)
            if (attacker != null && attacker.Owner.IsValid) {
                TargetShowDamageText(attacker.Owner, $"{damage:F0}", "damage");
            }

            // Aggro on the attacker
            if (attacker != null && _aiState == AIState.Idle) {
                _target = attacker.transform;
                _targetNob = attacker;
                _aiState = AIState.Aggro;
            }

            if (_currentHealth.Value <= 0) {
                Die(attacker);
            }
        }

        public bool IsAlive() => !_isDead.Value;
        public float GetCurrentHealth() => _currentHealth.Value;
        public float GetMaxHealth() => _data != null ? _data.MaxHealth : 100f;

        [Server]
        private void Die(NetworkObject killer) {
            if (_isDead.Value) return;
            _isDead.Value = true;

            if (_agent != null) _agent.ResetPath();

            // Notify systems
            EventBus.Trigger("OnEnemyKilled", killer, base.NetworkObject);

            string tag = _data != null ? _data.EnemyTag : "";
            EventBus.Trigger("OnEnemyMobKilled", killer, tag);

            Debug.Log($"[EnemyMob] {gameObject.name} killed by {(killer != null ? killer.name : "unknown")}");

            // Despawn after delay
            StartCoroutine(DespawnAfterDelay(3f));
        }

        private System.Collections.IEnumerator DespawnAfterDelay(float delay) {
            yield return new WaitForSeconds(delay);
            if (base.IsServerInitialized) {
                FishNet.InstanceFinder.ServerManager.Despawn(gameObject);
            }
        }

        // ═══════════════════════════════════════════════════════
        // RPCs
        // ═══════════════════════════════════════════════════════

        [TargetRpc]
        private void TargetShowDamageText(NetworkConnection conn, string text, string type, bool isCritical = false) {
            var data = new Genesis.Data.FloatingTextData(
                transform.position + Vector3.up * 1.5f,
                text,
                type,
                isCritical);
            EventBus.Trigger("OnShowFloatingText", data);
        }

        [ObserversRpc]
        private void RpcPlayAttackAnimation() {
            var animator = GetComponentInChildren<Animator>();
            if (animator != null) {
                animator.SetTrigger("Attack");
            }
        }

        // ═══════════════════════════════════════════════════════
        // SyncVar Callbacks
        // ═══════════════════════════════════════════════════════

        private void OnHealthSyncChanged(float oldVal, float newVal, bool asServer) {
            EventBus.Trigger("OnEnemyHealthChanged", base.NetworkObject, newVal, GetMaxHealth());
        }

        private void OnDeadSyncChanged(bool oldVal, bool newVal, bool asServer) {
            if (newVal) {
                // Disable collider on death
                var col = GetComponent<Collider>();
                if (col != null) col.enabled = false;

                var animator = GetComponentInChildren<Animator>();
                if (animator != null) {
                    animator.SetTrigger("Die");
                }
            }
        }
    }
}

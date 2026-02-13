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
        private enum AIState { Idle, Patrol, Aggro, Attack, Kiting, Return }
        private AIState _aiState = AIState.Idle;
        private Transform _target;
        private NetworkObject _targetNob;
        private Vector3 _spawnPosition;
        private float _lastAttackTime;
        private NavMeshAgent _agent;

        // Patrol
        private Vector3 _patrolTarget;
        private float _patrolWaitTimer;
        private bool _isWaitingAtPatrol;

        // Telegraph
        private bool _isAnticipating;
        private float _anticipationTimer;
        private bool _isRecovering;
        private float _recoveryTimer;

        // Support
        private float _lastHealTime;
        private EnemyMob _healTarget;

        // StatusEffects
        private StatusEffectSystem _statusSystem;

        // Return leash
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
            _statusSystem = GetComponent<StatusEffectSystem>();

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
            UpdateAgentSpeed();
            ServerAIUpdate();
        }

        private void UpdateAgentSpeed() {
            if (_agent == null || _data == null) return;
            float speed = _data.MoveSpeed;
            if (_statusSystem != null) speed *= _statusSystem.GetMovementSpeedMultiplier();
            _agent.speed = speed;
        }

        // ═══════════════════════════════════════════════════════
        // AI (Server Only)
        // ═══════════════════════════════════════════════════════

        private void ServerAIUpdate() {
            // Telegraph blocks all other actions
            if (UpdateTelegraph()) return;

            switch (_aiState) {
                case AIState.Idle:
                    HandleIdle();
                    break;
                case AIState.Patrol:
                    HandlePatrol();
                    break;
                case AIState.Aggro:
                    HandleAggro();
                    break;
                case AIState.Attack:
                    HandleAttack();
                    break;
                case AIState.Kiting:
                    HandleKiting();
                    break;
                case AIState.Return:
                    ReturnToSpawn();
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════
        // TELEGRAPH (Anticipation / Recovery)
        // ═══════════════════════════════════════════════════════

        private bool UpdateTelegraph() {
            if (_isAnticipating) {
                _anticipationTimer -= Time.deltaTime;
                if (_anticipationTimer <= 0f) {
                    _isAnticipating = false;
                    ExecuteAttack();
                    // Start recovery
                    if (_data != null && _data.RecoveryDuration > 0f) {
                        _isRecovering = true;
                        _recoveryTimer = _data.RecoveryDuration;
                    }
                }
                return true;
            }

            if (_isRecovering) {
                _recoveryTimer -= Time.deltaTime;
                if (_recoveryTimer <= 0f) {
                    _isRecovering = false;
                }
                return true;
            }

            return false;
        }

        // ═══════════════════════════════════════════════════════
        // IDLE
        // ═══════════════════════════════════════════════════════

        private void HandleIdle() {
            if (_data != null && _data.PatrolRadius > 0f) {
                PickNewPatrolTarget();
                _aiState = AIState.Patrol;
                return;
            }
            ScanForTargets();
        }

        // ═══════════════════════════════════════════════════════
        // PATROL
        // ═══════════════════════════════════════════════════════

        private void HandlePatrol() {
            // Always scan while patrolling
            if (ScanForTargets()) return;

            if (_isWaitingAtPatrol) {
                _patrolWaitTimer -= Time.deltaTime;
                if (_patrolWaitTimer <= 0f) {
                    _isWaitingAtPatrol = false;
                    PickNewPatrolTarget();
                }
                return;
            }

            float dist = Vector3.Distance(transform.position, _patrolTarget);
            if (dist <= 1.5f) {
                _isWaitingAtPatrol = true;
                _patrolWaitTimer = _data != null ? _data.PatrolWaitTime : 3f;
                if (_agent != null) _agent.ResetPath();
                return;
            }

            if (_agent != null && _agent.isOnNavMesh) {
                _agent.SetDestination(_patrolTarget);
            }
        }

        private void PickNewPatrolTarget() {
            float radius = _data != null ? _data.PatrolRadius : 5f;
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 candidate = _spawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius, NavMesh.AllAreas)) {
                _patrolTarget = hit.position;
            } else {
                _patrolTarget = _spawnPosition;
            }
        }

        // ═══════════════════════════════════════════════════════
        // AGGRO (Chase)
        // ═══════════════════════════════════════════════════════

        private void HandleAggro() {
            if (_target == null || !IsTargetValid()) {
                LoseTarget();
                return;
            }

            // Leash check
            float leashRange = _data != null ? _data.LeashRange : 20f;
            float distFromSpawn = Vector3.Distance(transform.position, _spawnPosition);
            if (distFromSpawn > leashRange) {
                LoseTarget();
                _aiState = AIState.Return;
                return;
            }

            // Support: chase wounded ally instead of player
            if (_data != null && _data.Archetype == EnemyArchetype.Support) {
                AggroAsSupport();
                return;
            }

            // Tank: try to interpose between player and ranged/support allies
            if (_data != null && _data.Role == EnemyRole.Tank) {
                AggroAsTank();
                return;
            }

            // Default chase logic
            ChaseTarget();
        }

        private void ChaseTarget() {
            float distToTarget = Vector3.Distance(transform.position, _target.position);
            float attackRange = _data != null ? _data.AttackRange : 2f;
            bool isRanged = _data != null && _data.IsRanged;

            if (distToTarget <= attackRange) {
                _aiState = AIState.Attack;
                if (_agent != null) _agent.ResetPath();
                return;
            }

            // Ranged enemies: if within preferred distance, stop and attack
            if (isRanged && _data.PreferredDistance > 0f && distToTarget <= _data.PreferredDistance) {
                _aiState = AIState.Attack;
                if (_agent != null) _agent.ResetPath();
                return;
            }

            MoveToward(_target.position, isRanged);
        }

        private void MoveToward(Vector3 destination, bool isRanged = false) {
            if (_agent != null && _agent.isOnNavMesh) {
                if (isRanged && _data != null && _data.PreferredDistance > 0f) {
                    Vector3 dirToTarget = (destination - transform.position).normalized;
                    Vector3 desiredPos = destination - dirToTarget * _data.PreferredDistance * 0.8f;
                    _agent.SetDestination(desiredPos);
                } else {
                    _agent.SetDestination(destination);
                }
            } else {
                Vector3 direction = (destination - transform.position).normalized;
                float speed = _data != null ? _data.MoveSpeed : 3f;
                transform.position += direction * speed * Time.deltaTime;
                if (direction != Vector3.zero) {
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // SUPPORT BEHAVIOR
        // ═══════════════════════════════════════════════════════

        private void AggroAsSupport() {
            // Find wounded ally
            _healTarget = FindLowestHPAlly();

            if (_healTarget != null) {
                float distToAlly = Vector3.Distance(transform.position, _healTarget.transform.position);
                float healRange = _data != null ? _data.AttackRange : 3f;

                if (distToAlly <= healRange) {
                    _aiState = AIState.Attack;
                    if (_agent != null) _agent.ResetPath();
                    return;
                }

                // Move toward wounded ally
                MoveToward(_healTarget.transform.position);
            } else {
                // No wounded allies - check if player is too close, kite
                if (_target != null) {
                    float distToPlayer = Vector3.Distance(transform.position, _target.position);
                    float kiteThreshold = _data != null ? _data.KiteThreshold : 3f;
                    if (distToPlayer < kiteThreshold) {
                        _aiState = AIState.Kiting;
                        return;
                    }
                }
                // Stay near spawn area
                float distFromSpawn = Vector3.Distance(transform.position, _spawnPosition);
                if (distFromSpawn > 5f) {
                    MoveToward(_spawnPosition);
                }
            }
        }

        private void AttackAsSupport() {
            if (_healTarget == null || _healTarget._isDead.Value) {
                _healTarget = FindLowestHPAlly();
                if (_healTarget == null) {
                    _aiState = AIState.Aggro;
                    return;
                }
            }

            float distToAlly = Vector3.Distance(transform.position, _healTarget.transform.position);
            float healRange = _data != null ? _data.AttackRange : 3f;

            if (distToAlly > healRange * 1.2f) {
                _aiState = AIState.Aggro;
                return;
            }

            // Face heal target
            FaceTarget(_healTarget.transform.position);

            float healCooldown = _data != null ? _data.HealCooldown : 4f;
            if (Time.time - _lastHealTime >= healCooldown) {
                StartAttackOrTelegraph();
            }
        }

        private EnemyMob FindLowestHPAlly() {
            float scanRange = _data != null ? _data.SupportScanRange : 12f;
            Collider[] hits = Physics.OverlapSphere(transform.position, scanRange);

            EnemyMob lowestAlly = null;
            float lowestPercent = 0.9f; // Only heal below 90% HP

            foreach (var hit in hits) {
                var ally = hit.GetComponent<EnemyMob>();
                if (ally == null || ally == this || ally._isDead.Value) continue;
                if (ally._data == null) continue;

                float hpPercent = ally._currentHealth.Value / ally._data.MaxHealth;
                if (hpPercent < lowestPercent) {
                    lowestPercent = hpPercent;
                    lowestAlly = ally;
                }
            }

            return lowestAlly;
        }

        [Server]
        public void HealFromSupport(float amount) {
            if (_isDead.Value) return;
            _currentHealth.Value = Mathf.Min(_currentHealth.Value + amount, _data != null ? _data.MaxHealth : 100f);
        }

        // ═══════════════════════════════════════════════════════
        // TANK BEHAVIOR
        // ═══════════════════════════════════════════════════════

        private void AggroAsTank() {
            // Try to interpose between player and ranged/support allies
            Vector3 interposeTarget = GetInterposePosition();

            float distToTarget = Vector3.Distance(transform.position, _target.position);
            float attackRange = _data != null ? _data.AttackRange : 3f;

            if (distToTarget <= attackRange) {
                _aiState = AIState.Attack;
                if (_agent != null) _agent.ResetPath();
                return;
            }

            // Move toward interpose position or directly toward player
            MoveToward(interposeTarget);
        }

        private Vector3 GetInterposePosition() {
            if (_target == null) return transform.position;

            float scanRange = _data != null ? _data.DetectionRange : 8f;
            Collider[] hits = Physics.OverlapSphere(transform.position, scanRange);

            Vector3 allyCenter = Vector3.zero;
            int allyCount = 0;

            foreach (var hit in hits) {
                var ally = hit.GetComponent<EnemyMob>();
                if (ally == null || ally == this || ally._isDead.Value) continue;
                if (ally._data == null) continue;
                if (ally._data.Archetype == EnemyArchetype.Ranged || ally._data.Archetype == EnemyArchetype.Support) {
                    allyCenter += ally.transform.position;
                    allyCount++;
                }
            }

            if (allyCount == 0) return _target.position;

            allyCenter /= allyCount;
            return Vector3.Lerp(_target.position, allyCenter, 0.4f);
        }

        private void ApplyKnockback(Transform playerTransform) {
            if (_data == null || _data.Role != EnemyRole.Tank) return;

            var motor = playerTransform.GetComponent<PlayerMotorMultiplayer>();
            if (motor == null) return;

            var nob = playerTransform.GetComponent<NetworkObject>();
            if (nob == null || !nob.Owner.IsValid) return;

            Vector3 knockDir = (playerTransform.position - transform.position).normalized;
            Vector3 knockTarget = playerTransform.position + knockDir * _data.KnockbackForce;

            // Validate with NavMesh
            if (NavMesh.SamplePosition(knockTarget, out NavMeshHit hit, _data.KnockbackForce, NavMesh.AllAreas)) {
                knockTarget = hit.position;
            }

            motor.RpcKnockback(nob.Owner, knockTarget, _data.KnockbackDuration);
        }

        // ═══════════════════════════════════════════════════════
        // ATTACK
        // ═══════════════════════════════════════════════════════

        private void HandleAttack() {
            // Support attacks (heals) allies, not the player
            if (_data != null && _data.Archetype == EnemyArchetype.Support) {
                AttackAsSupport();
                return;
            }

            if (_target == null || !IsTargetValid()) {
                LoseTarget();
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _target.position);
            float attackRange = _data != null ? _data.AttackRange : 2f;
            bool isRanged = _data != null && _data.IsRanged;

            // Out of range -> chase again
            float maxRange = isRanged ? _data.PreferredDistance * 1.3f : attackRange * 1.2f;
            if (distToTarget > maxRange) {
                _aiState = AIState.Aggro;
                return;
            }

            // Kiting: ranged enemies retreat if player gets too close
            if (isRanged && _data != null) {
                float kiteThreshold = _data.KiteThreshold;
                if (distToTarget < kiteThreshold) {
                    _aiState = AIState.Kiting;
                    return;
                }
            }

            // Face target
            FaceTarget(_target.position);

            float cooldown = _data != null ? _data.AttackCooldown : 2f;
            if (Time.time - _lastAttackTime >= cooldown) {
                StartAttackOrTelegraph();
            }
        }

        private void StartAttackOrTelegraph() {
            _lastAttackTime = Time.time;

            // Telegraph: if anticipation > 0, delay the attack
            if (_data != null && _data.AnticipationDuration > 0f) {
                _isAnticipating = true;
                _anticipationTimer = _data.AnticipationDuration;
                RpcPlayAnticipation();
                return;
            }

            // Instant attack (legacy behavior)
            ExecuteAttack();
        }

        private void ExecuteAttack() {
            // Support: heal ally
            if (_data != null && _data.Archetype == EnemyArchetype.Support) {
                if (_healTarget != null && !_healTarget._isDead.Value) {
                    float healAmount = _data.HealAmount;
                    _healTarget.HealFromSupport(healAmount);
                    _lastHealTime = Time.time;
                    RpcPlayAttackAnimation();
                }
                return;
            }

            // Offensive attack
            if (_target == null || !IsTargetValid()) return;

            float damage = _data != null ? Random.Range(_data.MinDamage, _data.MaxDamage) : 10f;

            if (_data != null && _data.IsRanged && _data.ProjectilePrefab != null) {
                FireProjectile(damage);
            } else {
                var damageable = _target.GetComponent<IDamageable>();
                if (damageable != null) {
                    damageable.TakeDamage(damage, base.NetworkObject);
                }

                // Tank: apply knockback on melee hit
                if (_data != null && _data.Role == EnemyRole.Tank) {
                    ApplyKnockback(_target);
                }
            }

            RpcPlayAttackAnimation();
        }

        [Server]
        private void FireProjectile(float damage) {
            Vector3 spawnPos = transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;
            Vector3 direction = (_target.position + Vector3.up * 1f - spawnPos).normalized;

            GameObject projectile = Instantiate(_data.ProjectilePrefab, spawnPos, Quaternion.LookRotation(direction));

            if (projectile.TryGetComponent(out Combat.ProjectileController controller)) {
                float speed = _data.ProjectileSpeed;
                controller.Initialize(base.NetworkObject, damage, direction * speed, 0.3f);
            }

            FishNet.InstanceFinder.ServerManager.Spawn(projectile);
        }

        // ═══════════════════════════════════════════════════════
        // KITING
        // ═══════════════════════════════════════════════════════

        private void HandleKiting() {
            if (_target == null || !IsTargetValid()) {
                LoseTarget();
                return;
            }

            // Leash check
            float leashRange = _data != null ? _data.LeashRange : 20f;
            float distFromSpawn = Vector3.Distance(transform.position, _spawnPosition);
            if (distFromSpawn > leashRange) {
                LoseTarget();
                _aiState = AIState.Return;
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _target.position);
            float kiteDistance = _data != null ? _data.KiteDistance : 6f;

            // Reached safe distance -> go back to attack
            if (distToTarget >= kiteDistance) {
                _aiState = AIState.Attack;
                return;
            }

            // Move away from player
            Vector3 retreatDir = (transform.position - _target.position).normalized;
            Vector3 retreatPos = transform.position + retreatDir * kiteDistance;

            // Clamp to leash range
            Vector3 fromSpawn = retreatPos - _spawnPosition;
            if (fromSpawn.magnitude > leashRange) {
                retreatPos = _spawnPosition + fromSpawn.normalized * leashRange;
            }

            if (_agent != null && _agent.isOnNavMesh) {
                _agent.SetDestination(retreatPos);
            } else {
                float speed = _data != null ? _data.MoveSpeed : 3f;
                transform.position += retreatDir * speed * Time.deltaTime;
            }

            // Face target while retreating
            FaceTarget(_target.position);

            // Attack during kiting if cooldown ready
            float cooldown = _data != null ? _data.AttackCooldown : 2f;
            float attackRange = _data != null ? _data.AttackRange : 2f;
            bool isRanged = _data != null && _data.IsRanged;
            float effectiveRange = isRanged ? (_data != null ? _data.PreferredDistance : attackRange) : attackRange;

            if (distToTarget <= effectiveRange && Time.time - _lastAttackTime >= cooldown) {
                StartAttackOrTelegraph();
            }
        }

        // ═══════════════════════════════════════════════════════
        // RETURN
        // ═══════════════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════

        private bool ScanForTargets() {
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
                return true;
            }
            return false;
        }

        private void FaceTarget(Vector3 targetPos) {
            Vector3 lookDir = (targetPos - transform.position);
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.001f) {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        private bool IsTargetValid() {
            if (_target == null) return false;
            var stats = _target.GetComponent<PlayerStats>();
            return stats != null && stats.IsAlive();
        }

        private void LoseTarget() {
            _target = null;
            _targetNob = null;
            _healTarget = null;
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
            if (attacker != null && (_aiState == AIState.Idle || _aiState == AIState.Patrol)) {
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

        [ObserversRpc]
        private void RpcPlayAnticipation() {
            var animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.SetTrigger("Anticipation");
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

using UnityEngine;
using UnityEngine.AI;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Genesis.Core;
using Genesis.Data;
using Genesis.Simulation.Combat;
using Genesis.Items;
using FishNet.Connection;
using System.Collections.Generic;

namespace Genesis.Simulation {

    [RequireComponent(typeof(NetworkObject))]
    public class EnemyMob : NetworkBehaviour, IDamageable {

        [Header("Data")]
        [SerializeField] private EnemyData _data;

        [Header("Loot")]
        [SerializeField] private GameObject _lootBagPrefab;

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

        // Arc distribution
        private float _arcAngleOffset;

        // StatusEffects
        private StatusEffectSystem _statusSystem;

        // Return leash
        private const float RETURN_THRESHOLD = 1f;

        public string EnemyTag => _data != null ? _data.EnemyTag : "";
        public string DisplayName => _data != null ? _data.DisplayName : gameObject.name;
        public float CurrentHealthValue => _currentHealth.Value;
        public float MaxHealthValue => _data != null ? _data.MaxHealth : 100f;
        public int GoldReward => _data != null ? _data.GoldReward : 0;

        public override void OnStartNetwork() {
            base.OnStartNetwork();
            _currentHealth.OnChange += OnHealthSyncChanged;
            _isDead.OnChange += OnDeadSyncChanged;
        }

        public override void OnStartServer() {
            base.OnStartServer();

            // Reset all state (critical for FishNet object pooling — objects are reused, not destroyed)
            _isDead.Value = false;
            _aiState = AIState.Idle;
            _target = null;
            _targetNob = null;
            _healTarget = null;
            _lastAttackTime = 0f;
            _lastHealTime = 0f;
            _isAnticipating = false;
            _anticipationTimer = 0f;
            _isRecovering = false;
            _recoveryTimer = 0f;
            _isWaitingAtPatrol = false;
            _patrolWaitTimer = 0f;

            _agent = GetComponent<NavMeshAgent>();
            if (_agent != null) {
                _agent.speed = _data != null ? _data.MoveSpeed : 3f;
                _agent.stoppingDistance = GetIdealStoppingDistance();
            }

            _spawnPosition = transform.position;
            _statusSystem = GetComponent<StatusEffectSystem>();
            _arcAngleOffset = GetInstanceID() * 2.399f; // Golden angle for optimal distribution

            // Re-enable colliders (disabled on death by OnDeadSyncChanged)
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = true;
            foreach (var childCol in GetComponentsInChildren<Collider>(true)) {
                childCol.enabled = true;
            }

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

            // Dynamically adjust stopping distance based on current state
            if (_agent != null) _agent.stoppingDistance = GetIdealStoppingDistance();

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
                // Keep facing target during anticipation
                FaceTelegraphTarget();

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
                FaceTelegraphTarget();
                _recoveryTimer -= Time.deltaTime;
                if (_recoveryTimer <= 0f) {
                    _isRecovering = false;
                }
                return true;
            }

            return false;
        }

        private void FaceTelegraphTarget() {
            if (_data != null && _data.Archetype == EnemyArchetype.Support && _healTarget != null && !_healTarget._isDead.Value) {
                FaceTarget(_healTarget.transform.position);
            } else if (_target != null) {
                FaceTarget(_target.position);
            }
        }

        // ═══════════════════════════════════════════════════════
        // IDLE
        // ═══════════════════════════════════════════════════════

        private void HandleIdle() {
            // Support: scan for wounded allies even without a player nearby
            if (_data != null && _data.Archetype == EnemyArchetype.Support) {
                _healTarget = FindLowestHPAlly();
                if (_healTarget != null) {
                    ScanForTargets(); // Try to find a player too, but not required
                    _aiState = AIState.Aggro;
                    return;
                }
            }

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
            // Support: can operate without a player target (healing allies)
            if (_data != null && _data.Archetype == EnemyArchetype.Support) {
                // Leash check
                float supportLeash = _data.LeashRange;
                float distFromSpawn = Vector3.Distance(transform.position, _spawnPosition);
                if (distFromSpawn > supportLeash) {
                    LoseTarget();
                    _aiState = AIState.Return;
                    return;
                }

                // Invalidate stale player target but don't abort
                if (_target != null && !IsTargetValid()) {
                    _target = null;
                    _targetNob = null;
                }

                AggroAsSupport();
                return;
            }

            if (_target == null || !IsTargetValid()) {
                LoseTarget();
                return;
            }

            // Leash check
            float leashRange = _data != null ? _data.LeashRange : 20f;
            float distFromSpawn2 = Vector3.Distance(transform.position, _spawnPosition);
            if (distFromSpawn2 > leashRange) {
                LoseTarget();
                _aiState = AIState.Return;
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

            // Ranged enemies: move to arc position at preferred distance
            if (isRanged && _data.PreferredDistance > 0f) {
                Vector3 arcPos = GetArcPosition(_target.position, _data.PreferredDistance);
                float distToArc = Vector3.Distance(transform.position, arcPos);

                if (distToArc <= 1.5f || distToTarget <= _data.PreferredDistance) {
                    _aiState = AIState.Attack;
                    if (_agent != null) _agent.ResetPath();
                    return;
                }

                if (_agent != null && _agent.isOnNavMesh) {
                    _agent.SetDestination(arcPos);
                }
                return;
            }

            MoveToward(_target.position);
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
            // Only re-scan if current heal target is invalid or healthy enough
            if (_healTarget == null || _healTarget._isDead.Value ||
                (_healTarget._data != null && _healTarget._currentHealth.Value / _healTarget._data.MaxHealth >= 0.9f)) {
                _healTarget = FindLowestHPAlly();
            }

            if (_healTarget != null) {
                float distToAlly = Vector3.Distance(transform.position, _healTarget.transform.position);
                float healRange = _data != null ? _data.AttackRange : 3f;

                if (distToAlly <= healRange) {
                    _aiState = AIState.Attack;
                    if (_agent != null) _agent.ResetPath();
                    return;
                }

                // Move toward wounded ally and face them
                MoveToward(_healTarget.transform.position);
                FaceTarget(_healTarget.transform.position);
            } else {
                // No wounded allies
                if (_target != null) {
                    // Player nearby: check kite or hold arc position
                    float distToPlayer = Vector3.Distance(transform.position, _target.position);
                    float kiteThreshold = _data != null ? _data.KiteThreshold : 3f;
                    if (distToPlayer < kiteThreshold) {
                        _aiState = AIState.Kiting;
                        return;
                    }

                    float prefDist = _data != null ? _data.PreferredDistance : 8f;
                    Vector3 arcPos = GetArcPosition(_target.position, prefDist);
                    float distToArc = Vector3.Distance(transform.position, arcPos);
                    if (distToArc > 2f) {
                        MoveToward(arcPos);
                    } else {
                        FaceTarget(_target.position);
                    }
                } else {
                    // No player and no wounded ally — return to idle and keep scanning
                    _aiState = AIState.Idle;
                }
            }
        }

        private void AttackAsSupport() {
            // Re-scan if target is null, dead, or healthy enough (same threshold as AggroAsSupport)
            if (_healTarget == null || _healTarget._isDead.Value ||
                (_healTarget._data != null && _healTarget._currentHealth.Value / _healTarget._data.MaxHealth >= 0.9f)) {
                _healTarget = FindLowestHPAlly();
                if (_healTarget == null) {
                    _aiState = AIState.Aggro;
                    return;
                }
            }

            float distToAlly = Vector3.Distance(transform.position, _healTarget.transform.position);
            float healRange = _data != null ? _data.AttackRange : 3f;

            if (distToAlly > healRange * 1.5f) {
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
                var ally = hit.GetComponentInParent<EnemyMob>();
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
            float maxHp = _data != null ? _data.MaxHealth : 100f;
            float actualHeal = Mathf.Min(amount, maxHp - _currentHealth.Value);
            _currentHealth.Value = Mathf.Min(_currentHealth.Value + amount, maxHp);
            if (actualHeal > 0f) {
                RpcShowHealText($"+{actualHeal:F0}");
            }
        }

        [ObserversRpc]
        private void RpcShowHealText(string text) {
            var data = new Genesis.Data.FloatingTextData(
                transform.position + Vector3.up * 1.5f,
                text,
                "heal");
            EventBus.Trigger("OnShowFloatingText", data);
        }

        // ═══════════════════════════════════════════════════════
        // TANK BEHAVIOR
        // ═══════════════════════════════════════════════════════

        private void AggroAsTank() {
            // Try to interpose between player and ranged/support allies
            Vector3 interposeTarget = GetInterposePosition();

            float distToTarget = Vector3.Distance(transform.position, _target.position);
            float distToInterpose = Vector3.Distance(transform.position, interposeTarget);
            float attackRange = _data != null ? _data.AttackRange : 3f;

            // If at interpose position and player is within attack range, fight
            if (distToTarget <= attackRange) {
                _aiState = AIState.Attack;
                if (_agent != null) _agent.ResetPath();
                return;
            }

            // Prioritize reaching interpose position; if already there, face the player
            if (distToInterpose <= attackRange * 0.5f) {
                if (_agent != null) _agent.ResetPath();
                FaceTarget(_target.position);
                return;
            }

            // Move toward interpose position
            MoveToward(interposeTarget);
        }

        private Vector3 GetInterposePosition() {
            if (_target == null) return transform.position;

            // Scan from spawn position with leash range so we always find our allies
            // even when the tank has chased far toward the player
            float scanRange = _data != null ? _data.LeashRange : 20f;
            Collider[] hits = Physics.OverlapSphere(_spawnPosition, scanRange);

            Vector3 allyCenter = Vector3.zero;
            int allyCount = 0;

            foreach (var hit in hits) {
                var ally = hit.GetComponentInParent<EnemyMob>();
                if (ally == null || ally == this || ally._isDead.Value) continue;
                if (ally._data == null) continue;
                if (ally._data.Archetype == EnemyArchetype.Ranged || ally._data.Archetype == EnemyArchetype.Support) {
                    allyCenter += ally.transform.position;
                    allyCount++;
                }
            }

            if (allyCount == 0) return _target.position;

            allyCenter /= allyCount;

            // Position the tank between player and allies, closer to the ally side to block
            Vector3 dirPlayerToAllies = (allyCenter - _target.position).normalized;
            float distPlayerToAllies = Vector3.Distance(_target.position, allyCenter);
            // Stand at 30% of the distance from allies toward player (blocking position)
            float blockDistance = Mathf.Max(distPlayerToAllies * 0.3f, _data != null ? _data.AttackRange : 3f);
            return allyCenter - dirPlayerToAllies * blockDistance;
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

            // Kiting: ranged enemies hold and shoot if player gets too close
            if (isRanged && _data != null) {
                float kiteThreshold = _data.KiteThreshold;
                if (distToTarget < kiteThreshold) {
                    _aiState = AIState.Kiting;
                    return;
                }
            }

            // Ranged: drift toward arc position while attacking
            if (isRanged && _data != null && _data.PreferredDistance > 0f) {
                Vector3 arcPos = GetArcPosition(_target.position, _data.PreferredDistance);
                float distToArc = Vector3.Distance(transform.position, arcPos);
                if (distToArc > 2f && _agent != null && _agent.isOnNavMesh) {
                    _agent.SetDestination(arcPos);
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

            bool isRanged = _data != null && _data.IsRanged;

            // Kiting: fire kiting projectile (fireball)
            if (_aiState == AIState.Kiting && _data != null && _data.KitingProjectilePrefab != null) {
                FireKitingProjectile(damage);
            }
            // Ranged: regular projectile
            else if (isRanged && _data.ProjectilePrefab != null) {
                FireProjectile(damage);
            }
            // Ranged fallback: use kiting projectile for regular attacks too
            else if (isRanged && _data.KitingProjectilePrefab != null) {
                FireKitingProjectile(damage);
            }
            // Ranged without any projectile: skip (no melee fallback)
            else if (isRanged) {
                return;
            }
            // Melee attack (only for non-ranged enemies)
            else {
                var damageable = _target.GetComponent<IDamageable>();
                if (damageable != null) {
                    damageable.TakeDamage(damage, base.NetworkObject);
                }

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

        [Server]
        private void FireKitingProjectile(float damage) {
            Vector3 spawnPos = transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;
            Vector3 direction = (_target.position + Vector3.up * 1f - spawnPos).normalized;

            GameObject projectile = Instantiate(_data.KitingProjectilePrefab, spawnPos, Quaternion.LookRotation(direction));

            if (projectile.TryGetComponent(out Combat.ProjectileController controller)) {
                float speed = _data.KitingProjectileSpeed;
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

            // Player backed off -> return to normal attack
            if (distToTarget >= kiteDistance) {
                _aiState = AIState.Attack;
                return;
            }

            // STOP and hold position
            if (_agent != null) _agent.ResetPath();

            // Face target
            FaceTarget(_target.position);

            // Fire kiting projectile (fireball) when cooldown ready
            float cooldown = _data != null ? _data.AttackCooldown : 2f;
            if (Time.time - _lastAttackTime >= cooldown) {
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

        private Vector3 GetArcPosition(Vector3 targetPos, float radius) {
            float angle = _arcAngleOffset;
            Vector3 arcPos = targetPos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

            if (NavMesh.SamplePosition(arcPos, out NavMeshHit hit, radius * 0.5f, NavMesh.AllAreas)) {
                return hit.position;
            }
            return arcPos;
        }

        private float GetIdealStoppingDistance() {
            if (_data == null) return 1.5f;
            bool isRanged = _data.IsRanged;
            switch (_aiState) {
                case AIState.Kiting:
                    return 0.5f;
                case AIState.Attack:
                    if (_data.Archetype == EnemyArchetype.Support)
                        return _data.AttackRange * 0.5f;
                    if (isRanged)
                        return Mathf.Min(_data.PreferredDistance * 0.8f, 2f);
                    return _data.AttackRange * 0.8f;
                case AIState.Aggro:
                    if (_data.Archetype == EnemyArchetype.Support)
                        return 0.5f;
                    if (isRanged)
                        return Mathf.Min(_data.PreferredDistance * 0.5f, 2f);
                    return _data.AttackRange * 0.8f;
                default:
                    return _data.AttackRange * 0.8f;
            }
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

            if (_data != null && _data.HitSound != null)
                RpcPlayEnemyHitSound();

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

            if (_data != null && _data.DeathSound != null)
                RpcPlayEnemyDeathSound();

            // Drop loot bag if enemy has loot table
            if (_data != null && _data.LootTable != null) {
                SpawnLootBag();
            }

            // Despawn after delay
            StartCoroutine(DespawnAfterDelay(3f));
        }

        [Server]
        private void SpawnLootBag() {
            List<ItemSlot> loot = _data.LootTable.GetLoot();
            if (loot.Count == 0) return;
            if (_lootBagPrefab == null) return;

            Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 10f)) {
                spawnPos = hit.point + Vector3.up * 0.5f;
            }

            GameObject bagObj = Instantiate(_lootBagPrefab, spawnPos, Quaternion.identity);
            base.ServerManager.Spawn(bagObj);

            LootBag bag = bagObj.GetComponent<LootBag>();
            if (bag != null) {
                bag.Initialize(loot, _data.DisplayName);
            }

            RpcPlayLootDropSound(spawnPos);
        }

        [ObserversRpc]
        private void RpcPlayLootDropSound(Vector3 position) {
            EventBus.Trigger("OnLootDropped", position);
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

        [ObserversRpc]
        private void RpcPlayEnemyHitSound() {
            if (_data != null && _data.HitSound != null)
                EventBus.Trigger("OnPlaySFX3D", _data.HitSound, transform.position);
        }

        [ObserversRpc]
        private void RpcPlayEnemyDeathSound() {
            if (_data != null && _data.DeathSound != null)
                EventBus.Trigger("OnPlaySFX3D", _data.DeathSound, transform.position);
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

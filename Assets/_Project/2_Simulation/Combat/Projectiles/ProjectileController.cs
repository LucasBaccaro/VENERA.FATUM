using UnityEngine;
using FishNet.Object;
using Genesis.Data;
using Genesis.Core;
using Genesis.Simulation.World;
using Genesis.Simulation;

namespace Genesis.Simulation.Combat {

    [RequireComponent(typeof(NetworkObject))]
    public class ProjectileController : NetworkBehaviour {

        private NetworkObject _owner;
        private float _damage;
        private Vector3 _velocity;
        private float _radius = 0.2f;
        private float _spawnTime;
        private bool _initialized;
        private bool _ownerIsEnemy;
        private StatusEffectData[] _effectsToApply;
        private AbilityCategory _category = AbilityCategory.Magical;
        private int _abilityId = -1;

        [Header("Visuals")]
        [SerializeField] private GameObject impactVfxPrefab;

        public void Initialize(NetworkObject owner, float damage, Vector3 velocity, float radius, StatusEffectData[] effects = null, AbilityCategory category = AbilityCategory.Magical, int abilityId = -1) {
            _owner = owner;
            _damage = damage;
            _velocity = velocity;
            _radius = radius;
            _effectsToApply = effects;
            _category = category;
            _abilityId = abilityId;
            _spawnTime = Time.time;
            _initialized = true;
            _ownerIsEnemy = _owner != null && _owner.GetComponent<EnemyMob>() != null;

            // Ignorar colisiones físicas con el dueño (si usamos Rigidbody/Colliders)
            if (_owner != null) {
                Collider[] ownerColliders = _owner.GetComponentsInChildren<Collider>();
                Collider myCollider = GetComponent<Collider>();
                if (myCollider != null) {
                    foreach (var col in ownerColliders) Physics.IgnoreCollision(col, myCollider);
                }
            }
        }

        public override void OnStartServer() {
            base.OnStartServer();
            // Server mueve el proyectil
        }

        [Server]
        void FixedUpdate() {
            if (!_initialized) return;

            // Timeout (5 seg)
            if (Time.time - _spawnTime > 5f) {
                Despawn();
                return;
            }

            float distance = _velocity.magnitude * Time.fixedDeltaTime;
            Vector3 direction = _velocity.normalized;

            // Detección de Colisión (SphereCast)
            // Enemy projectiles ignore other enemies; reflected projectiles detect them
            int mask = _ownerIsEnemy
                ? LayerMask.GetMask("Environment", "Player", "Colliders")
                : LayerMask.GetMask("Enemy", "Environment", "Player", "Colliders");

            // Origen ajustado para evitar colisionar con uno mismo si nace muy cerca
            Vector3 origin = transform.position + direction * 0.1f;

            if (Physics.SphereCast(origin, _radius, direction, out RaycastHit hit, distance, mask)) {
                HandleImpact(hit);
                return;
            }

            // Mover
            transform.position += direction * distance;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        [Server]
        private void HandleImpact(RaycastHit hit) {
            // ═══ CASO 1: Impacto con Environment o Colliders (pared/obstáculo) ═══
            int hitLayer = hit.collider.gameObject.layer;
            if (hitLayer == LayerMask.NameToLayer("Environment") || hitLayer == LayerMask.NameToLayer("Colliders")) {
                SpawnImpactVFX(hit.point, hit.normal);
                Despawn();
                return;
            }

            // ═══ CASO 2: Impacto con Entidad ═══
            NetworkObject targetNetObj = hit.collider.GetComponentInParent<NetworkObject>();
            if (targetNetObj == null) {
                Despawn();
                return;
            }

            // No auto-daño (el proyectil no daña a quien lo disparó)
            if (targetNetObj == _owner) return;

            // Party pass-through: projectile passes through party members without impact
            if (_owner != null && !_ownerIsEnemy) {
                int ownerId = _owner.OwnerId;
                int hitId = targetNetObj.OwnerId;
                if (ownerId >= 0 && hitId >= 0
                    && ServiceLocator.Instance.TryGet<PartyManager>(out var pm)
                    && pm.IsInSameParty(ownerId, hitId)) {
                    return;
                }
            }

            // ═══ CASO 3: REFLECT (target tiene buff de reflejo activo) ═══
            if (targetNetObj.TryGetComponent(out StatusEffectSystem statusSystem)) {
                if (statusSystem.HasEffect(EffectType.Reflect)) {
                    // Reflejar el proyectil en dirección opuesta
                    _velocity = Vector3.Reflect(_velocity, hit.normal);
                    _owner = targetNetObj; // El target ahora es el nuevo dueño del proyectil
                    _ownerIsEnemy = false; // Proyectil reflejado puede dañar enemigos

                    // VFX de reflejo (escudo brillante)
                    RpcPlayReflectVFX(hit.point);

                    Debug.Log($"[ProjectileController] Projectile reflected by {targetNetObj.name}!");

                    return; // NO despawnear, el proyectil sigue volando
                }
            }

            // ═══ SAFE ZONE VALIDATION (before damage) ═══
            if (!CombatValidator.CanApplyDamage(targetNetObj, _owner, out string reason)) {
                Debug.Log($"[ProjectileController] Projectile hit blocked: {reason}");
                SpawnImpactVFX(hit.point, hit.normal); // Still show impact
                Despawn();
                return;
            }

            // ═══ CASO 4: Daño con CombatCalculator ═══
            PlayerAttributes ownerAttrs = _owner != null ? _owner.GetComponent<PlayerAttributes>() : null;
            AttributeConfig config = ownerAttrs != null ? ownerAttrs.Config : null;

            if (targetNetObj.TryGetComponent(out PlayerStats targetPlayerStats)) {
                CombatResult result = CombatCalculator.CalculateDamage(_owner, targetNetObj, _damage, _category, config);
                if (result.ResultType != DamageResultType.Evaded) {
                    targetPlayerStats.TakeDamage(result.FinalDamage, _owner, result.ResultType);
                    if (result.LifeStealAmount > 0f) {
                        PlayerStats ownerStats = _owner != null ? _owner.GetComponent<PlayerStats>() : null;
                        ownerStats?.Heal(result.LifeStealAmount);
                    }
                }
            } else if (targetNetObj.TryGetComponent(out IDamageable damageable)) {
                CombatResult result = CombatCalculator.CalculateDamage(_owner, targetNetObj, _damage, _category, config);
                damageable.TakeDamage(result.FinalDamage, _owner);
                if (result.LifeStealAmount > 0f) {
                    PlayerStats ownerStats = _owner != null ? _owner.GetComponent<PlayerStats>() : null;
                    ownerStats?.Heal(result.LifeStealAmount);
                }
            }

            // ═══ CASO 5: Aplicar Status Effects ═══
            if (_effectsToApply != null && _effectsToApply.Length > 0) {
                StatusEffectSystem targetStatus = targetNetObj.GetComponent<StatusEffectSystem>();
                if (targetStatus != null) {
                    foreach (var effectData in _effectsToApply) {
                        targetStatus.ApplyEffect(effectData);
                        Debug.Log($"[ProjectileController] Applied {effectData.Name} to {targetNetObj.name}");
                    }
                } else {
                    Debug.LogWarning($"[ProjectileController] {targetNetObj.name} has no StatusEffectSystem!");
                }
            }

            // VFX de impacto (explosión, sangre, chispas)
            SpawnImpactVFX(hit.point, hit.normal);

            // Despawn del proyectil
            Despawn();
        }

        [ObserversRpc]
        private void RpcPlayReflectVFX(Vector3 position) {
            // Efecto visual de reflejo (solo clientes)
            // TODO: Instanciar partículas de escudo brillante cuando estén disponibles
            Debug.Log($"[ProjectileController] Playing reflect VFX at {position}");
        }

        private void SpawnImpactVFX(Vector3 pos, Vector3 normal) {
            if (impactVfxPrefab != null) {
                GameObject vfx = Instantiate(impactVfxPrefab, pos, Quaternion.LookRotation(normal));
                Spawn(vfx); // Spawn en red
                Destroy(vfx, 2f);
            }

            // Impact sound — ability projectiles use AbilityData, enemy projectiles use EnemyData
            if (_abilityId >= 0) {
                RpcPlayImpactSound(pos, _abilityId);
            } else if (_ownerIsEnemy && _owner != null) {
                var enemyMob = _owner.GetComponent<EnemyMob>();
                if (enemyMob != null) {
                    enemyMob.PlayProjectileImpactSound(pos);
                }
            }
        }

        [ObserversRpc]
        private void RpcPlayImpactSound(Vector3 position, int abilityId) {
            if (AbilityDatabase.Instance == null) return;
            AbilityData ability = AbilityDatabase.Instance.GetAbility(abilityId);
            if (ability != null && ability.ImpactSound != null) {
                EventBus.Trigger("OnPlaySFX3D", ability.ImpactSound, position);
            }
        }

        private void Despawn() {
            if (NetworkObject.IsSpawned) {
                Despawn(gameObject);
            } else {
                Destroy(gameObject);
            }
        }
    }
}

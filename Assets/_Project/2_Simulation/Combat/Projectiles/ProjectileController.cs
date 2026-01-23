using UnityEngine;
using FishNet.Object;
using Genesis.Data;
using Genesis.Core;

namespace Genesis.Simulation.Combat {

    [RequireComponent(typeof(NetworkObject))]
    public class ProjectileController : NetworkBehaviour {

        private NetworkObject _owner;
        private float _damage;
        private Vector3 _velocity;
        private float _radius = 0.2f;
        private float _spawnTime;
        private bool _initialized;

        [Header("Visuals")]
        [SerializeField] private GameObject impactVfxPrefab;

        public void Initialize(NetworkObject owner, float damage, Vector3 velocity, float radius) {
            _owner = owner;
            _damage = damage;
            _velocity = velocity;
            _radius = radius;
            _spawnTime = Time.time;
            _initialized = true;

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
            // LayerMask: Enemy (6) + Environment (8) + Player (3)
            int mask = LayerMask.GetMask("Enemy", "Environment", "Player");

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
            // Ignorar al dueño (no auto-daño)
            if (hit.collider.TryGetComponent(out NetworkObject netObj)) {
                if (netObj == _owner) return;
            }

            // Aplicar daño
            if (hit.collider.TryGetComponent(out IDamageable damageable)) {
                damageable.TakeDamage(_damage, _owner);
            }

            // VFX
            SpawnImpactVFX(hit.point, hit.normal);

            // Destruir
            Despawn();
        }

        private void SpawnImpactVFX(Vector3 pos, Vector3 normal) {
            if (impactVfxPrefab != null) {
                GameObject vfx = Instantiate(impactVfxPrefab, pos, Quaternion.LookRotation(normal));
                Spawn(vfx); // Spawn en red
                Destroy(vfx, 2f);
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

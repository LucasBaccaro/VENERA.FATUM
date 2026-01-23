using UnityEngine;
using FishNet.Object;
using Genesis.Data;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Proyectil que viaja hacia un target y aplica efectos al impactar
    /// Usado por habilidades Targeted que requieren proyectil visual
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class TargetedProjectile : NetworkBehaviour {

        private NetworkObject _caster;
        private NetworkObject _target;
        private float _speed;
        private float _spawnTime;
        private bool _initialized;
        private AbilityData _abilityData;

        [Header("Settings")]
        [SerializeField] private float maxLifetime = 3f; // Timeout de seguridad
        [SerializeField] private float arrivalThreshold = 0.2f; // Distancia para "llegar"

        /// <summary>
        /// Inicializa el proyectil con toda la información de la habilidad
        /// </summary>
        /// <param name="caster">Quien lanzó la habilidad</param>
        /// <param name="target">NetworkObject del objetivo</param>
        /// <param name="speed">Velocidad del proyectil</param>
        /// <param name="data">Datos de la habilidad (daño, heal, effects, etc)</param>
        public void Initialize(NetworkObject caster, NetworkObject target, float speed, AbilityData data) {
            _caster = caster;
            _target = target;
            _speed = speed;
            _abilityData = data;
            _spawnTime = Time.time;
            _initialized = true;
        }

        public override void OnStartServer() {
            base.OnStartServer();
            // El servidor mueve el proyectil
        }

        void Update() {
            if (!base.IsServer || !_initialized) return;

            // Timeout de seguridad
            if (Time.time - _spawnTime > maxLifetime) {
                Debug.LogWarning("[TargetedProjectile] Timeout - despawning");
                Despawn();
                return;
            }

            // Si el target fue destruido, despawnear
            if (_target == null) {
                Debug.Log("[TargetedProjectile] Target destroyed - despawning");
                Despawn();
                return;
            }

            // Calcular dirección hacia el target
            Vector3 targetPos = _target.transform.position + Vector3.up * 1f; // Centro del target
            Vector3 direction = (targetPos - transform.position).normalized;
            float distanceToTarget = Vector3.Distance(transform.position, targetPos);

            // Verificar si llegamos
            if (distanceToTarget <= arrivalThreshold) {
                OnArrival();
                return;
            }

            // Mover hacia el target
            float step = _speed * Time.deltaTime;
            transform.position += direction * step;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        /// <summary>
        /// Llamado cuando el proyectil llega al target - APLICA EL DAÑO AQUÍ
        /// </summary>
        private void OnArrival() {
            if (_target == null || _abilityData == null) {
                Despawn();
                return;
            }

            // APLICAR TODOS LOS EFECTOS AL TARGET
            TargetedLogic.ApplyEffectsToTarget(_caster, _target, _abilityData);

            Debug.Log($"[TargetedProjectile] {_abilityData.Name} hit {_target.name}!");

            // Despawnear este proyectil
            Despawn();
        }

        private void Despawn() {
            if (base.NetworkObject != null && base.NetworkObject.IsSpawned) {
                base.Despawn(gameObject);
            } else {
                Destroy(gameObject);
            }
        }
    }
}

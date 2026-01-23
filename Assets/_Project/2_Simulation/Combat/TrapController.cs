using UnityEngine;
using FishNet.Object;
using Genesis.Data;
using System.Collections.Generic;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Controlador para trampas persistentes (Trampa de Hielo, etc)
    /// La trampa persiste en el mundo hasta ser activada por un enemigo o expirar
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(SphereCollider))]
    public class TrapController : NetworkBehaviour {

        [Header("Visual")]
        [SerializeField] private GameObject model; // Modelo 3D de la trampa
        [SerializeField] private GameObject triggerVFX; // VFX al activarse

        private NetworkObject _owner;
        private float _damage;
        private float _triggerRadius;
        private float _lifetime;
        private AbilityData _abilityData;
        private float _spawnTime;
        private bool _hasTriggered;

        // Collider como trigger
        private SphereCollider _triggerCollider;

        // Cooldown anti-spam (para evitar activar múltiples veces)
        private HashSet<int> _triggeredEntities = new HashSet<int>();

        void Awake() {
            _triggerCollider = GetComponent<SphereCollider>();
            _triggerCollider.isTrigger = true;
        }

        /// <summary>
        /// Inicializa la trampa (llamado desde TrapLogic)
        /// CRITICAL: Solo llamar en SERVER
        /// </summary>
        public void Initialize(NetworkObject owner, float damage, float triggerRadius, float lifetime, AbilityData abilityData) {
            _owner = owner;
            _damage = damage;
            _triggerRadius = triggerRadius;
            _lifetime = lifetime;
            _abilityData = abilityData;
            _spawnTime = Time.time;
            _hasTriggered = false;

            // Configurar collider
            _triggerCollider.radius = triggerRadius;

            Debug.Log($"[TrapController] Trap initialized. Owner: {owner.name}, Damage: {damage}, Radius: {triggerRadius}, Lifetime: {lifetime}s");
        }

        public override void OnStartServer() {
            base.OnStartServer();
            // Server gestiona la lógica de la trampa
        }

        [Server]
        void FixedUpdate() {
            // Timeout (expiración)
            if (Time.time - _spawnTime > _lifetime) {
                Expire();
                return;
            }
        }

        /// <summary>
        /// Trigger cuando un enemigo entra en el área
        /// </summary>
        [Server]
        void OnTriggerEnter(Collider other) {
            if (_hasTriggered) return;

            // Verificar que sea un enemigo
            if (other.TryGetComponent(out NetworkObject netObj)) {

                // Ignorar al owner
                if (netObj == _owner) return;

                // Ignorar si ya lo activamos (por si hay overlap)
                if (_triggeredEntities.Contains(netObj.ObjectId)) return;

                // Verificar que sea un enemigo (Layer Enemy o validar team)
                if (other.gameObject.layer != LayerMask.NameToLayer("Enemy")) {
                    // Si no es enemigo, skip
                    // TODO: Implementar sistema de teams para PvP
                    return;
                }

                // ACTIVAR TRAMPA
                Trigger(netObj);
            }
        }

        /// <summary>
        /// Activa la trampa (aplica daño/efectos)
        /// </summary>
        [Server]
        private void Trigger(NetworkObject victim) {
            _hasTriggered = true;
            _triggeredEntities.Add(victim.ObjectId);

            // Aplicar DAMAGE
            if (_damage > 0) {
                if (victim.TryGetComponent(out IDamageable damageable)) {
                    damageable.TakeDamage(_damage, _owner);
                    Debug.Log($"[TrapController] Trap triggered by {victim.name}. Dealt {_damage} damage");
                }
            }

            // Aplicar STATUS EFFECTS
            if (_abilityData != null && _abilityData.ApplyToTarget != null && _abilityData.ApplyToTarget.Length > 0) {
                // TODO: StatusEffectSystem.ApplyEffects(victim, _abilityData.ApplyToTarget);
                Debug.Log($"[TrapController] Applied {_abilityData.ApplyToTarget.Length} effects to {victim.name}");
            }

            // VFX de activación
            if (_abilityData != null && _abilityData.ImpactVFX != null) {
                GameObject vfx = Instantiate(_abilityData.ImpactVFX, transform.position, Quaternion.identity);
                FishNet.InstanceFinder.ServerManager.Spawn(vfx);
                Destroy(vfx, 2f);
            } else if (triggerVFX != null) {
                GameObject vfx = Instantiate(triggerVFX, transform.position, Quaternion.identity);
                FishNet.InstanceFinder.ServerManager.Spawn(vfx);
                Destroy(vfx, 2f);
            }

            // Ocultar modelo (o destruir después de VFX)
            if (model != null) {
                model.SetActive(false);
            }

            // Destruir trampa después de un delay (para que se vea la VFX)
            Invoke(nameof(DestroySelf), 0.5f);
        }

        /// <summary>
        /// Expiración (lifetime terminó)
        /// </summary>
        [Server]
        private void Expire() {
            Debug.Log($"[TrapController] Trap expired (lifetime: {_lifetime}s)");

            // VFX de expiración (opcional)
            // ...

            DestroySelf();
        }

        /// <summary>
        /// Destruye la trampa (despawn)
        /// </summary>
        [Server]
        private void DestroySelf() {
            if (NetworkObject.IsSpawned) {
                Despawn(gameObject);
            } else {
                Destroy(gameObject);
            }
        }
    }
}

using UnityEngine;
using FishNet.Object;
using Genesis.Data;
using Genesis.Simulation;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Lógica para habilidades Targeted (sistema legacy)
    /// Aplica daño, heal o buff directamente al target seleccionado
    /// Ejemplos: Golpe Rápido, Daga de Maná, Punición, Luz Sanadora
    /// </summary>
    [CreateAssetMenu(fileName = "Logic_Targeted", menuName = "Genesis/Combat/Logic/Targeted")]
    public class TargetedLogic : AbilityLogic {

        [Header("Projectile Visual (Optional)")]
        [Tooltip("Si se asigna, spawneará un proyectil visual que viaja hacia el target")]
        public bool useVisualProjectile = true;
        public float projectileSpeed = 20f;

        // Valores default para Targeted abilities (se pueden sobrescribir en el inspector)
        private void OnEnable() {
            if (MovementGracePeriod == 0.3f) MovementGracePeriod = 0.15f; // Targeted abilities tienen menos grace period
            if (MovementThreshold == 0.1f) MovementThreshold = 0.2f;
            // CancelOnMovement = true por defecto
        }

        /// <summary>
        /// Override Execute para tener acceso directo al target NetworkObject
        /// que viene desde el servidor (enviado por PlayerCombat)
        /// </summary>
        public override void Execute(NetworkObject caster, NetworkObject target, Vector3 groundPoint, AbilityData data) {
            // Validación
            if (target == null) {
                Debug.LogError($"[TargetedLogic] {data.Name} requires a target!");
                return;
            }

            // PROYECTIL (si está configurado, el proyectil aplicará el daño al impactar)
            if (useVisualProjectile && data.ProjectilePrefab != null) {
                SpawnTargetedProjectile(caster, target, data);
            } else {
                // MODO INSTANTÁNEO (sin proyectil): Aplicar efectos inmediatamente
                ApplyEffectsToTarget(caster, target, data);
            }

            // STATUS EFFECTS (to self) - Estos se aplican siempre al castear
            if (data.ApplyToSelf != null && data.ApplyToSelf.Length > 0) {
                StatusEffectSystem casterStatus = caster.GetComponent<StatusEffectSystem>();
                if (casterStatus != null) {
                    foreach (var effectData in data.ApplyToSelf) {
                        casterStatus.ApplyEffect(effectData);
                        Debug.Log($"[TargetedLogic] Applied {effectData.Name} to self");
                    }
                }
            }
        }

        /// <summary>
        /// Spawna un proyectil que viaja hacia el target y aplica el daño al impactar
        /// </summary>
        private void SpawnTargetedProjectile(NetworkObject caster, NetworkObject target, AbilityData data) {
            Vector3 spawnPos = caster.transform.position + Vector3.up * 1.5f + caster.transform.forward * 0.5f;
            Vector3 direction = (target.transform.position - spawnPos).normalized;

            GameObject instance = Object.Instantiate(data.ProjectilePrefab, spawnPos, Quaternion.LookRotation(direction));

            // Configurar el proyectil con toda la data
            if (instance.TryGetComponent(out TargetedProjectile targetedProj)) {
                targetedProj.Initialize(caster, target, projectileSpeed, data);
            } else {
                Debug.LogWarning($"[TargetedLogic] {data.ProjectilePrefab.name} no tiene componente TargetedProjectile!");
            }

            // Spawn en red
            FishNet.InstanceFinder.ServerManager.Spawn(instance);
        }

        /// <summary>
        /// Aplica todos los efectos al target (daño, heal, status effects, VFX)
        /// Usado cuando NO hay proyectil, o llamado por el proyectil al impactar
        /// </summary>
        public static void ApplyEffectsToTarget(NetworkObject caster, NetworkObject target, AbilityData data) {
            if (target == null) return;

            // DAMAGE
            if (data.BaseDamage > 0) {
                if (target.TryGetComponent(out IDamageable damageable)) {
                    damageable.TakeDamage(data.BaseDamage, caster);
                    Debug.Log($"[TargetedLogic] {caster.name} dealt {data.BaseDamage} damage to {target.name}");
                }
            }

            // HEAL
            if (data.BaseHeal > 0) {
                if (target.TryGetComponent(out PlayerStats stats)) {
                    stats.RestoreHealth(data.BaseHeal);
                    Debug.Log($"[TargetedLogic] {caster.name} healed {target.name} for {data.BaseHeal}");
                }
            }

            // STATUS EFFECTS (to target)
            if (data.ApplyToTarget != null && data.ApplyToTarget.Length > 0) {
                StatusEffectSystem targetStatus = target.GetComponent<StatusEffectSystem>();
                if (targetStatus != null) {
                    foreach (var effectData in data.ApplyToTarget) {
                        targetStatus.ApplyEffect(effectData);
                        Debug.Log($"[TargetedLogic] Applied {effectData.Name} to {target.name}");
                    }
                }
            }

            // IMPACT VFX
            if (data.ImpactVFX != null) {
                Vector3 impactPos = target.transform.position + Vector3.up * 1f;
                GameObject vfx = Object.Instantiate(data.ImpactVFX, impactPos, Quaternion.identity);
                FishNet.InstanceFinder.ServerManager.Spawn(vfx);
                Object.Destroy(vfx, 2f);
            }
        }

        /// <summary>
        /// Mantener compatibilidad con ExecuteDirectional (aunque ya no se usa directamente)
        /// </summary>
        public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {
            Debug.LogWarning("[TargetedLogic] ExecuteDirectional llamado directamente, pero debería usar Execute() con target");
        }
    }
}

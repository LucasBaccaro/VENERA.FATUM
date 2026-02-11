using UnityEngine;
using FishNet.Object;
using Genesis.Data;
using Genesis.Simulation;
using Genesis.Simulation.Combat;

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
                // Si ApplyEffectsInstant es true, ya se aplicaron en PlayerCombat
                if (!data.ApplyEffectsInstant) {
                    ApplyEffectsToTarget(caster, target, data);
                } else if (data.BaseDamage > 0 || data.BaseHeal > 0 || data.ImpactVFX != null) {
                    // Si es instant, igual necesitamos aplicar daño/heal/vfx si NO hay proyectil
                    ApplyCombatValuesToTarget(caster, target, data);
                }
            }

            // STATUS EFFECTS (to self) - Estos se aplican siempre al castear
            // Si ApplyEffectsInstant es true, ya se aplicaron en PlayerCombat
            if (!data.ApplyEffectsInstant && data.ApplyToSelf != null && data.ApplyToSelf.Length > 0) {
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

            // Get config for combat calculations
            PlayerAttributes casterAttrs = caster != null ? caster.GetComponent<PlayerAttributes>() : null;
            AttributeConfig config = casterAttrs != null ? casterAttrs.Config : null;

            // DAMAGE
            if (data.BaseDamage > 0) {
                if (target.TryGetComponent(out PlayerStats targetStats)) {
                    CombatResult result = CombatCalculator.CalculateDamage(caster, target, data.BaseDamage, data.Category, config);
                    if (result.ResultType != DamageResultType.Evaded) {
                        targetStats.TakeDamage(result.FinalDamage, caster, result.ResultType);
                        if (result.LifeStealAmount > 0f) {
                            PlayerStats casterStats = caster.GetComponent<PlayerStats>();
                            casterStats?.Heal(result.LifeStealAmount);
                        }
                    }
                    Debug.Log($"[TargetedLogic] {caster.name} dealt {result.FinalDamage:F0} ({result.ResultType}) to {target.name}");
                } else if (target.TryGetComponent(out IDamageable damageable)) {
                    damageable.TakeDamage(data.BaseDamage, caster);
                }
            }

            // HEAL
            if (data.BaseHeal > 0) {
                if (target.TryGetComponent(out PlayerStats stats)) {
                    CombatResult healResult = CombatCalculator.CalculateHeal(caster, data.BaseHeal, config);
                    stats.RestoreHealth(healResult.FinalDamage);
                    Debug.Log($"[TargetedLogic] {caster.name} healed {target.name} for {healResult.FinalDamage:F0} ({healResult.ResultType})");
                }
            }

            // STATUS EFFECTS (to target)
            // Si ApplyEffectsInstant es true, ya se aplicaron en PlayerCombat al inicio
            if (!data.ApplyEffectsInstant && data.ApplyToTarget != null && data.ApplyToTarget.Length > 0) {
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
                Object.Destroy(vfx, data.ImpactVFXDuration);
            }
        }

        /// <summary>
        /// Aplica solo daño, heal y VFX (sin status effects).
        /// Usado cuando ApplyEffectsInstant es true.
        /// </summary>
        private static void ApplyCombatValuesToTarget(NetworkObject caster, NetworkObject target, AbilityData data) {
            if (target == null) return;

            PlayerAttributes casterAttrs = caster != null ? caster.GetComponent<PlayerAttributes>() : null;
            AttributeConfig config = casterAttrs != null ? casterAttrs.Config : null;

            // DAMAGE
            if (data.BaseDamage > 0) {
                if (target.TryGetComponent(out PlayerStats targetStats)) {
                    CombatResult result = CombatCalculator.CalculateDamage(caster, target, data.BaseDamage, data.Category, config);
                    if (result.ResultType != DamageResultType.Evaded) {
                        targetStats.TakeDamage(result.FinalDamage, caster, result.ResultType);
                        if (result.LifeStealAmount > 0f) {
                            PlayerStats casterStats = caster.GetComponent<PlayerStats>();
                            casterStats?.Heal(result.LifeStealAmount);
                        }
                    }
                } else if (target.TryGetComponent(out IDamageable damageable)) {
                    damageable.TakeDamage(data.BaseDamage, caster);
                }
            }

            // HEAL
            if (data.BaseHeal > 0) {
                if (target.TryGetComponent(out PlayerStats stats)) {
                    CombatResult healResult = CombatCalculator.CalculateHeal(caster, data.BaseHeal, config);
                    stats.RestoreHealth(healResult.FinalDamage);
                }
            }

            // IMPACT VFX
            if (data.ImpactVFX != null) {
                Vector3 impactPos = target.transform.position + Vector3.up * 1f;
                GameObject vfx = Object.Instantiate(data.ImpactVFX, impactPos, Quaternion.identity);
                FishNet.InstanceFinder.ServerManager.Spawn(vfx);
                Object.Destroy(vfx, data.ImpactVFXDuration);
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

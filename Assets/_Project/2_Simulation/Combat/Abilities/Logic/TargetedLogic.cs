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

        public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

            // Para targeted abilities, necesitamos el target actual
            if (caster.TryGetComponent(out TargetingSystem targeting)) {
                NetworkObject target = targeting.CurrentTarget;

                if (target == null) {
                    Debug.LogError($"[TargetedLogic] {data.Name} requires a target!");
                    return;
                }

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
                    // TODO: StatusEffectSystem.ApplyEffects(target, data.ApplyToTarget);
                    Debug.Log($"[TargetedLogic] Applied {data.ApplyToTarget.Length} effects to target");
                }

                // IMPACT VFX
                if (data.ImpactVFX != null) {
                    Vector3 impactPos = target.transform.position + Vector3.up * 1f;
                    GameObject vfx = Object.Instantiate(data.ImpactVFX, impactPos, Quaternion.identity);
                    FishNet.InstanceFinder.ServerManager.Spawn(vfx);
                    Object.Destroy(vfx, 2f);
                }
            } else {
                Debug.LogError($"[TargetedLogic] Caster {caster.name} missing TargetingSystem component!");
            }

            // STATUS EFFECTS (to self)
            if (data.ApplyToSelf != null && data.ApplyToSelf.Length > 0) {
                // TODO: StatusEffectSystem.ApplyEffects(caster, data.ApplyToSelf);
                Debug.Log($"[TargetedLogic] Applied {data.ApplyToSelf.Length} effects to self");
            }
        }
    }
}

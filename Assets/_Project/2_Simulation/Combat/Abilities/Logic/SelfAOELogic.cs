using UnityEngine;
using FishNet.Object;
using Genesis.Data;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Lógica para habilidades AOE centradas en el caster (self-centered)
    /// Aplica efecto en un radio alrededor del jugador (instant cast)
    /// Ejemplos: Torbellino, Nova de Escarcha
    /// </summary>
    [CreateAssetMenu(fileName = "Logic_SelfAOE", menuName = "Genesis/Combat/Logic/Self AOE")]
    public class SelfAOELogic : AbilityLogic {

        [Header("Self AOE Settings")]
        [SerializeField] private bool includeSelf = false; // Si el caster se daña a sí mismo

        public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

            Vector3 casterPos = caster.transform.position;

            // NOTE: CastVFX se spawna en PlayerCombat durante el casting
            // Aquí solo spawneamos el ImpactVFX

            // Detectar enemigos en radio
            Collider[] hits = Physics.OverlapSphere(casterPos, data.Radius, LayerMask.GetMask("Enemy", "Player"));

            int hitCount = 0;

            foreach (var hit in hits) {
                if (hit.TryGetComponent(out NetworkObject netObj)) {

                    // Ignorar al caster (a menos que includeSelf = true)
                    if (netObj == caster && !includeSelf) continue;

                    // Aplicar DAMAGE
                    if (data.BaseDamage > 0) {
                        if (hit.TryGetComponent(out IDamageable damageable)) {
                            damageable.TakeDamage(data.BaseDamage, caster);
                            hitCount++;
                        }
                    }

                    // Aplicar STATUS EFFECTS
                    if (data.ApplyToTarget != null && data.ApplyToTarget.Length > 0) {
                        // TODO: StatusEffectSystem.ApplyEffects(netObj, data.ApplyToTarget);
                        Debug.Log($"[SelfAOELogic] Applied {data.ApplyToTarget.Length} effects to {netObj.name}");
                    }

                    // Impact VFX individual en cada enemigo
                    if (data.ImpactVFX != null) {
                        GameObject impactVfx = Object.Instantiate(data.ImpactVFX, hit.transform.position + Vector3.up * 1f, Quaternion.identity);
                        FishNet.InstanceFinder.ServerManager.Spawn(impactVfx);
                        Object.Destroy(impactVfx, 1f);
                    }
                }
            }

            Debug.Log($"[SelfAOELogic] {caster.name} cast {data.Name}. Hit {hitCount} targets in {data.Radius}m radius");
        }
    }
}

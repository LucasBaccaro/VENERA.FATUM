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
            Debug.Log($"[SelfAOELogic] Executing {data.Name} (Radius: {data.Radius}) for {caster.name} at {casterPos}");

            // NOTE: CastVFX se spawna en PlayerCombat durante el casting
            // Aquí solo spawneamos el ImpactVFX

            // 1. APLICAR STATUS EFFECTS AL CASTER (Self)
            // Si ApplyEffectsInstant es true, ya se aplicaron en PlayerCombat al inicio
            if (!data.ApplyEffectsInstant && data.ApplyToSelf != null && data.ApplyToSelf.Length > 0) {
                StatusEffectSystem casterStatus = caster.GetComponent<StatusEffectSystem>();
                if (casterStatus != null) {
                    foreach (var effectData in data.ApplyToSelf) {
                        casterStatus.ApplyEffect(effectData);
                    }
                }
            }

            // 2. DETECTAR OBJETIVOS EN RADIO
            Collider[] hits = Physics.OverlapSphere(casterPos, data.Radius, LayerMask.GetMask("Enemy", "Player"));

            int hitCount = 0;

            foreach (var hit in hits) {
                if (hit.TryGetComponent(out NetworkObject netObj)) {

                    // Ignorar al caster (a menos que includeSelf = true)
                    if (netObj == caster && !includeSelf) continue;

                    Debug.Log($"[SelfAOELogic] Hit {hit.name}");

                    // Aplicar DAMAGE
                    if (data.BaseDamage > 0) {
                        if (hit.TryGetComponent(out IDamageable damageable)) {
                            damageable.TakeDamage(data.BaseDamage, caster);
                            hitCount++;
                        }
                    }

                    // Aplicar STATUS EFFECTS AL TARGET
                    // NOTA: Para habilidades channeled (como Torbellino), los efectos a objetivos se suelen aplicar en cada tick.
                    // Si ApplyEffectsInstant es true, los efectos al TARGET en una habilidad NO dirigida (ground/aoe)
                    // son ambiguos. Pero para Targeted abilities ya lo manejamos.
                    // En SelfAOE, asumimos que ApplyEffectsInstant afecta principalmente a Self.
                    if (data.ApplyToTarget != null && data.ApplyToTarget.Length > 0) {
                        StatusEffectSystem statusSystem = netObj.GetComponent<StatusEffectSystem>();
                        if (statusSystem != null) {
                            foreach (var effectData in data.ApplyToTarget) {
                                statusSystem.ApplyEffect(effectData);
                            }
                        }
                    }

                    // Impact VFX individual en cada enemigo
                    if (data.ImpactVFX != null) {
                        GameObject impactVfx = Object.Instantiate(data.ImpactVFX, hit.transform.position + Vector3.up * 1f, Quaternion.identity);
                        FishNet.InstanceFinder.ServerManager.Spawn(impactVfx);
                        Object.Destroy(impactVfx, 1f);
                    }
                }
            }

            Debug.Log($"[SelfAOELogic] Finished {data.Name}. Total valid hits: {hitCount}");
        }
    }
}

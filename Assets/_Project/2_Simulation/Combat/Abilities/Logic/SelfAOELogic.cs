using UnityEngine;
using FishNet.Object;
using Genesis.Data;
using Genesis.Simulation;

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
        [Tooltip("Radio con el que fue diseñado el efecto visual (escala 1).")]
        [SerializeField] private float baseVisualRadius = 2.5f;

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

            // 2. SPAWN CENTRAL IMPACT VFX (Feedback visual constante)
            if (data.ImpactVFX != null) {
                GameObject centralVfx = Object.Instantiate(data.ImpactVFX, casterPos + Vector3.up * 0.1f, Quaternion.identity);
                
                // ESCALAR EL VFX
                float scaleMultiplier = data.Radius / baseVisualRadius;
                centralVfx.transform.localScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);

                FishNet.InstanceFinder.ServerManager.Spawn(centralVfx);
                Object.Destroy(centralVfx, data.ImpactVFXDuration);
            }

            // Get config for combat calculations
            PlayerAttributes casterAttrs = caster != null ? caster.GetComponent<PlayerAttributes>() : null;
            AttributeConfig config = casterAttrs != null ? casterAttrs.Config : null;

            // 3. DETECTAR OBJETIVOS EN RADIO
            Collider[] hits = Physics.OverlapSphere(casterPos, data.Radius, LayerMask.GetMask("Enemy", "Player"));

            int hitCount = 0;

            foreach (var hit in hits) {
                var netObj = hit.GetComponentInParent<NetworkObject>();
                if (netObj != null) {

                    // Ignorar al caster (a menos que includeSelf = true)
                    if (netObj == caster && !includeSelf) continue;

                    Debug.Log($"[SelfAOELogic] Hit {netObj.name}");

                    // Aplicar DAMAGE
                    if (data.BaseDamage > 0) {
                        if (netObj.TryGetComponent(out PlayerStats targetStats)) {
                            CombatResult result = CombatCalculator.CalculateDamage(caster, netObj, data.BaseDamage, data.Category, config);
                            if (result.ResultType != DamageResultType.Evaded) {
                                targetStats.TakeDamage(result.FinalDamage, caster, result.ResultType);
                                if (result.LifeStealAmount > 0f) {
                                    PlayerStats casterStats = caster.GetComponent<PlayerStats>();
                                    casterStats?.Heal(result.LifeStealAmount);
                                }
                                hitCount++;
                            }
                        } else if (netObj.TryGetComponent(out IDamageable damageable)) {
                            CombatResult result = CombatCalculator.CalculateDamage(caster, netObj, data.BaseDamage, data.Category, config);
                            damageable.TakeDamage(result.FinalDamage, caster);
                            if (result.LifeStealAmount > 0f) {
                                PlayerStats casterStats = caster.GetComponent<PlayerStats>();
                                casterStats?.Heal(result.LifeStealAmount);
                            }
                            hitCount++;
                        }
                    }

                    // Aplicar STATUS EFFECTS AL TARGET
                    if (data.ApplyToTarget != null && data.ApplyToTarget.Length > 0) {
                        StatusEffectSystem statusSystem = netObj.GetComponent<StatusEffectSystem>();
                        if (statusSystem != null) {
                            foreach (var effectData in data.ApplyToTarget) {
                                statusSystem.ApplyEffect(effectData);
                            }
                        }
                    }

                    // NOTA: Hemos eliminado el ImpactVFX individual por enemigo para usar el CENTRAL
                    // y mantener consistencia con AOELogic, evitando ruido visual excesivo.
                }
            }

            // IMPACT SOUND (once at caster position)
            if (data.ImpactSound != null) {
                PlayerCombat combat = caster.GetComponent<PlayerCombat>();
                combat?.RpcPlayImpactSoundAtPosition(casterPos, data.ID);
            }

            Debug.Log($"[SelfAOELogic] Finished {data.Name}. Total valid hits: {hitCount}");
        }
    }
}

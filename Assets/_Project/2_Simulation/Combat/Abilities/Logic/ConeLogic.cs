using UnityEngine;
using FishNet.Object;
using Genesis.Data;
using Genesis.Simulation;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Lógica para habilidades cónicas (área frontal en forma de abanico)
    /// Aplica daño a todos los enemigos dentro del ángulo especificado
    /// Ejemplos: Multidisparo
    /// </summary>
    [CreateAssetMenu(fileName = "Logic_Cone", menuName = "Genesis/Combat/Logic/Cone")]
    public class ConeLogic : AbilityLogic {

        [Header("Cone Settings")]
        [SerializeField] private bool requiresLineOfSight = false; // Si requiere LoS a cada target

        public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

            Vector3 casterPos = caster.transform.position;
            float halfAngle = data.Angle / 2f;

            // NOTE: CastVFX se spawna en PlayerCombat durante el casting
            // Aquí solo spawneamos el ImpactVFX por target

            // Get config for combat calculations
            PlayerAttributes casterAttrs = caster != null ? caster.GetComponent<PlayerAttributes>() : null;
            AttributeConfig config = casterAttrs != null ? casterAttrs.Config : null;

            // Detectar todos los enemigos en esfera (luego filtrar por ángulo)
            Collider[] hits = Physics.OverlapSphere(casterPos, data.Range, LayerMask.GetMask("Enemy", "Player"));

            int hitCount = 0;

            foreach (var hit in hits) {
                var netObj = hit.GetComponentInParent<NetworkObject>();
                if (netObj != null) {

                    // Ignorar al caster
                    if (netObj == caster) continue;

                    Vector3 dirToTarget = (netObj.transform.position - casterPos).normalized;
                    float angleToTarget = Vector3.Angle(direction, dirToTarget);

                    // Verificar si está dentro del cono
                    if (angleToTarget <= halfAngle) {

                        // Opcional: Line of Sight check
                        if (requiresLineOfSight) {
                            if (Physics.Linecast(casterPos, netObj.transform.position, out RaycastHit losHit, LayerMask.GetMask("Environment"))) {
                                continue;
                            }
                        }

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

                        // Aplicar STATUS EFFECTS
                        if (data.ApplyToTarget != null && data.ApplyToTarget.Length > 0) {
                            StatusEffectSystem statusSystem = netObj.GetComponent<StatusEffectSystem>();
                            if (statusSystem != null) {
                                foreach (var effectData in data.ApplyToTarget) {
                                    statusSystem.ApplyEffect(effectData);
                                }
                            }
                        }

                        // Impact VFX en cada target
                        if (data.ImpactVFX != null) {
                            GameObject impactVfx = Object.Instantiate(data.ImpactVFX, netObj.transform.position + Vector3.up * 1f, Quaternion.identity);
                            FishNet.InstanceFinder.ServerManager.Spawn(impactVfx);
                            Object.Destroy(impactVfx, data.ImpactVFXDuration);
                        }
                    }
                }
            }

            Debug.Log($"[ConeLogic] {caster.name} cast {data.Name}. Hit {hitCount} targets in {data.Angle}° cone");
        }
    }
}

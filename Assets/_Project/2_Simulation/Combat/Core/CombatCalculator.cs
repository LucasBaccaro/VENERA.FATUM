using UnityEngine;
using FishNet.Object;
using Genesis.Data;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Static server-side combat calculator.
    /// Applies attribute scaling, crit/overpower, evasion, and life steal.
    /// </summary>
    public static class CombatCalculator {

        /// <summary>
        /// Calculate damage with full attribute pipeline.
        /// </summary>
        public static CombatResult CalculateDamage(
            NetworkObject caster,
            NetworkObject target,
            float baseDamage,
            AbilityCategory category,
            AttributeConfig config) {

            CombatResult result = new CombatResult {
                FinalDamage = baseDamage,
                ResultType = DamageResultType.Normal,
                WasCritical = false,
                WasOverpower = false,
                LifeStealAmount = 0f
            };

            if (config == null) return result;

            PlayerAttributes casterAttrs = caster != null ? caster.GetComponent<PlayerAttributes>() : null;
            PlayerAttributes targetAttrs = target != null ? target.GetComponent<PlayerAttributes>() : null;

            // 1. Apply primary attribute scaling
            if (casterAttrs != null) {
                if (category == AbilityCategory.Physical) {
                    result.FinalDamage *= (1f + casterAttrs.PhysicalDamageBonus);
                } else if (category == AbilityCategory.Magical) {
                    result.FinalDamage *= (1f + casterAttrs.MagicDamageBonus);
                }
            }

            // 2. Check Evasion (target AGI)
            if (targetAttrs != null && targetAttrs.EvasionChance > 0f) {
                float evasionRoll = Random.Range(0f, 1f);
                if (evasionRoll < targetAttrs.EvasionChance) {
                    result.FinalDamage = 0f;
                    result.ResultType = DamageResultType.Evaded;
                    return result;
                }
            }

            // 4. Overpower vs Critical (mutually exclusive)
            if (casterAttrs != null) {
                // Overpower: STR-based, physical attacks
                if (category == AbilityCategory.Physical && casterAttrs.OverpowerChance > 0f) {
                    float overpowerRoll = Random.Range(0f, 1f);
                    if (overpowerRoll < casterAttrs.OverpowerChance) {
                        result.FinalDamage *= config.OverpowerDamageMultiplier;
                        result.ResultType = DamageResultType.Overpower;
                        result.WasOverpower = true;
                    }
                }

                // Critical: Only if not already Overpower
                if (!result.WasOverpower) {
                    float critChance = (category == AbilityCategory.Physical)
                        ? casterAttrs.CritChance
                        : casterAttrs.SpellCritChance;

                    if (critChance > 0f) {
                        float critRoll = Random.Range(0f, 1f);
                        if (critRoll < critChance) {
                            result.FinalDamage *= config.CritDamageMultiplier;
                            result.ResultType = DamageResultType.Critical;
                            result.WasCritical = true;
                        }
                    }
                }
            }

            // 5. Life Steal
            if (casterAttrs != null && casterAttrs.LifeSteal > 0f) {
                result.LifeStealAmount = result.FinalDamage * casterAttrs.LifeSteal;
            }

            return result;
        }

        /// <summary>
        /// Calculate heal with attribute pipeline (WIS scaling + crit heal).
        /// </summary>
        public static CombatResult CalculateHeal(
            NetworkObject caster,
            float baseHeal,
            AttributeConfig config) {

            CombatResult result = new CombatResult {
                FinalDamage = baseHeal,
                ResultType = DamageResultType.Normal,
                WasCritical = false,
                WasOverpower = false,
                LifeStealAmount = 0f
            };

            if (config == null) return result;

            PlayerAttributes casterAttrs = caster != null ? caster.GetComponent<PlayerAttributes>() : null;

            // 1. WIS healing power scaling
            if (casterAttrs != null && casterAttrs.HealingPowerBonus > 0f) {
                result.FinalDamage *= (1f + casterAttrs.HealingPowerBonus);
            }

            // 2. Critical Heal (INT-based spell crit)
            if (casterAttrs != null && casterAttrs.SpellCritChance > 0f) {
                float critRoll = Random.Range(0f, 1f);
                if (critRoll < casterAttrs.SpellCritChance) {
                    result.FinalDamage *= config.CritHealMultiplier;
                    result.ResultType = DamageResultType.CriticalHeal;
                    result.WasCritical = true;
                }
            }

            return result;
        }
    }
}

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

            // Apply damage variance (±%)
            float variance = config != null ? config.DamageVariance : 0.15f;
            float variedDamage = baseDamage * Random.Range(1f - variance, 1f + variance);

            CombatResult result = new CombatResult {
                FinalDamage = variedDamage,
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

            // 2. Block mitigation (target Block - caster Penetration)
            if (targetAttrs != null && targetAttrs.BlockValue > 0f) {
                float penetration = casterAttrs != null ? Mathf.Clamp01(casterAttrs.Penetration) : 0f;
                float effectiveBlock = targetAttrs.BlockValue * (1f - penetration);
                if (effectiveBlock > 0f) {
                    float mitigation = effectiveBlock / (effectiveBlock + 100f);
                    result.FinalDamage *= (1f - mitigation);
                }
            }

            // 2.5 Armor mitigation (physical only)
            if (category == AbilityCategory.Physical && targetAttrs != null && targetAttrs.Armor > 0f) {
                float armorMit = targetAttrs.Armor / (targetAttrs.Armor + 100f);
                result.FinalDamage *= (1f - armorMit);
            }

            // 2.5 Magic Resistance mitigation (magical only)
            if (category == AbilityCategory.Magical && targetAttrs != null && targetAttrs.MagicResistance > 0f) {
                float magResMit = targetAttrs.MagicResistance / (targetAttrs.MagicResistance + 100f);
                result.FinalDamage *= (1f - magResMit);
            }

            // 3. Check Evasion (target AGI)
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

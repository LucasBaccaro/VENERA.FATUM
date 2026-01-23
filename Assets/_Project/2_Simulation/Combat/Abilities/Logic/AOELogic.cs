using UnityEngine;
using FishNet.Object;
using Genesis.Data;
using System.Collections;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Lógica para habilidades AOE ground-targeted (círculo en el suelo)
    /// Aplica daño/heal en área circular al punto seleccionado
    /// Ejemplos: Meteorito, Sagrario, Salva
    /// </summary>
    [CreateAssetMenu(fileName = "Logic_AOE", menuName = "Genesis/Combat/Logic/AOE")]
    public class AOELogic : AbilityLogic {

        [Header("AOE Settings")]
        [SerializeField] private float impactDelay = 0f; // Delay antes del impacto (ej: Meteorito 1s)
        [SerializeField] private bool affectsAllies = false;
        [SerializeField] private bool affectsEnemies = true;

        public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

            if (impactDelay > 0) {
                // Spawn warning VFX y esperar delay
                if (data.CastVFX != null) {
                    GameObject warningVfx = Object.Instantiate(data.CastVFX, targetPoint, Quaternion.identity);
                    FishNet.InstanceFinder.ServerManager.Spawn(warningVfx);
                    Object.Destroy(warningVfx, impactDelay + 1f);
                }

                // Ejecutar impacto después del delay
                caster.StartCoroutine(DelayedImpact(caster, targetPoint, data));
            } else {
                // Impacto inmediato
                ApplyAOEEffect(caster, targetPoint, data);
            }
        }

        private IEnumerator DelayedImpact(NetworkObject caster, Vector3 targetPoint, AbilityData data) {
            yield return new WaitForSeconds(impactDelay);
            ApplyAOEEffect(caster, targetPoint, data);
        }

        private void ApplyAOEEffect(NetworkObject caster, Vector3 targetPoint, AbilityData data) {

            // Spawn impact VFX
            if (data.ImpactVFX != null) {
                GameObject vfx = Object.Instantiate(data.ImpactVFX, targetPoint, Quaternion.identity);
                FishNet.InstanceFinder.ServerManager.Spawn(vfx);
                Object.Destroy(vfx, 3f);
            }

            // Detectar todos los targets en radio
            LayerMask mask = GetLayerMask();
            Collider[] hits = Physics.OverlapSphere(targetPoint, data.Radius, mask);

            int damageCount = 0;
            int healCount = 0;

            foreach (var hit in hits) {
                if (hit.TryGetComponent(out NetworkObject netObj)) {

                    // Ignorar al caster (opcional)
                    if (netObj == caster) continue;

                    // Aplicar DAMAGE
                    if (data.BaseDamage > 0 && affectsEnemies) {
                        if (hit.TryGetComponent(out IDamageable damageable)) {
                            damageable.TakeDamage(data.BaseDamage, caster);
                            damageCount++;
                        }
                    }

                    // Aplicar HEAL
                    if (data.BaseHeal > 0 && affectsAllies) {
                        if (hit.TryGetComponent(out PlayerStats stats)) {
                            stats.RestoreHealth(data.BaseHeal);
                            healCount++;
                        }
                    }

                    // Aplicar STATUS EFFECTS
                    if (data.ApplyToTarget != null && data.ApplyToTarget.Length > 0) {
                        // TODO: StatusEffectSystem.ApplyEffects(netObj, data.ApplyToTarget);
                    }
                }
            }

            Debug.Log($"[AOELogic] {caster.name} cast {data.Name} at {targetPoint}. Damaged: {damageCount}, Healed: {healCount}");
        }

        private LayerMask GetLayerMask() {
            // Detectar según configuración
            if (affectsEnemies && affectsAllies) {
                return LayerMask.GetMask("Enemy", "Player");
            } else if (affectsEnemies) {
                return LayerMask.GetMask("Enemy");
            } else if (affectsAllies) {
                return LayerMask.GetMask("Player");
            }
            return 0;
        }
    }
}

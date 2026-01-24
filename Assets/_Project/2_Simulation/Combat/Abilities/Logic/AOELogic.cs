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

        [Header("Warning Indicator")]
        [Tooltip("Prefab de AOEWarningIndicator para mostrar durante el delay. Opcional.")]
        [SerializeField] private GameObject warningIndicatorPrefab;

        public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

            if (impactDelay > 0) {
                // Spawnar warning indicator en el suelo
                if (warningIndicatorPrefab != null) {
                    // Ajustar posición al suelo (sin offset, el indicador maneja su propia altura)
                    Vector3 spawnPos = targetPoint;
                    spawnPos.y = targetPoint.y; // Mantener altura del ground

                    // IMPORTANTE: Spawnearlo con rotación correcta desde el principio para evitar glitch visual en clientes
                    Quaternion spawnRot = Quaternion.Euler(90f, 0f, 0f);
                    GameObject warningObj = Object.Instantiate(warningIndicatorPrefab, spawnPos, spawnRot);

                    // Verificar que tiene NetworkObject
                    NetworkObject nob = warningObj.GetComponent<NetworkObject>();
                    if (nob != null) {
                        // Spawnearlo en red para que todos los clientes lo vean
                        FishNet.InstanceFinder.ServerManager.Spawn(warningObj);

                        // Inicializar el warning indicator DESPUÉS del spawn
                        if (warningObj.TryGetComponent<AOEWarningIndicator>(out var indicator)) {
                            indicator.Initialize(spawnPos, data.Radius, impactDelay);
                            Debug.Log($"[AOELogic] Warning indicator spawned at {spawnPos} for {data.Name} (radius: {data.Radius}, delay: {impactDelay}s)");
                        } else {
                            Debug.LogError($"[AOELogic] Warning prefab missing AOEWarningIndicator component!");
                        }
                    } else {
                        Debug.LogError($"[AOELogic] Warning prefab missing NetworkObject! Cannot spawn in network.");
                        Object.Destroy(warningObj);
                    }
                } else {
                    Debug.LogWarning($"[AOELogic] {data.Name} has impactDelay but no warningIndicatorPrefab assigned!");
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
            // IMPORTANTE: Siempre incluimos ambos layers (Enemy y Player) y filtramos por team en vez de por layer
            // Esto permite que habilidades dañen a players en PvP
            if (affectsEnemies && affectsAllies) {
                return LayerMask.GetMask("Enemy", "Player");
            } else if (affectsEnemies) {
                return LayerMask.GetMask("Enemy", "Player"); // Incluir Player para PvP
            } else if (affectsAllies) {
                return LayerMask.GetMask("Player");
            }
            return 0;
        }
    }
}

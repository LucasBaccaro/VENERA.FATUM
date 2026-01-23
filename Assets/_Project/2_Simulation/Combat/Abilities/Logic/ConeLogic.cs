using UnityEngine;
using FishNet.Object;
using Genesis.Data;

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

            // VFX del cono
            if (data.CastVFX != null) {
                GameObject vfx = Object.Instantiate(data.CastVFX, casterPos, Quaternion.LookRotation(direction));
                FishNet.InstanceFinder.ServerManager.Spawn(vfx);
                Object.Destroy(vfx, 2f);
            }

            // Detectar todos los enemigos en esfera (luego filtrar por ángulo)
            Collider[] hits = Physics.OverlapSphere(casterPos, data.Range, LayerMask.GetMask("Enemy", "Player"));

            int hitCount = 0;

            foreach (var hit in hits) {
                if (hit.TryGetComponent(out NetworkObject netObj)) {

                    // Ignorar al caster
                    if (netObj == caster) continue;

                    Vector3 dirToTarget = (hit.transform.position - casterPos).normalized;
                    float angleToTarget = Vector3.Angle(direction, dirToTarget);

                    // Verificar si está dentro del cono
                    if (angleToTarget <= halfAngle) {

                        // Opcional: Line of Sight check
                        if (requiresLineOfSight) {
                            if (Physics.Linecast(casterPos, hit.transform.position, out RaycastHit losHit, LayerMask.GetMask("Environment"))) {
                                // Obstruido por pared
                                continue;
                            }
                        }

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
                        }

                        // Impact VFX en cada target
                        if (data.ImpactVFX != null) {
                            GameObject impactVfx = Object.Instantiate(data.ImpactVFX, hit.transform.position + Vector3.up * 1f, Quaternion.identity);
                            FishNet.InstanceFinder.ServerManager.Spawn(impactVfx);
                            Object.Destroy(impactVfx, 1f);
                        }
                    }
                }
            }

            Debug.Log($"[ConeLogic] {caster.name} cast {data.Name}. Hit {hitCount} targets in {data.Angle}° cone");
        }
    }
}

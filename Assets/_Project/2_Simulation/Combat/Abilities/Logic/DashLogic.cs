using UnityEngine;
using FishNet.Object;
using Genesis.Data;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Lógica para habilidades de movimiento/dash
    /// Teleporta al jugador hacia la posición objetivo
    /// Ejemplos: Carga (forward), Desenganche (backward)
    /// </summary>
    [CreateAssetMenu(fileName = "Logic_Dash", menuName = "Genesis/Combat/Logic/Dash")]
    public class DashLogic : AbilityLogic {

        [Header("Dash Settings")]
        [SerializeField] private bool isBackwards = false; // True para Desenganche
        [SerializeField] private bool canDashThroughEnemies = false; // Si puede atravesar enemigos
        [SerializeField] private bool applyDamageInPath = false; // Si aplica daño durante el dash

        public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

            Vector3 startPos = caster.transform.position;

            // Invertir dirección si es backwards (Desenganche)
            if (isBackwards) {
                direction = -direction;
                targetPoint = startPos + direction * data.Range;
            }

            // Validar destino (debe haber suelo)
            if (!Physics.Raycast(targetPoint + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 5f, LayerMask.GetMask("Environment"))) {
                Debug.LogWarning($"[DashLogic] Invalid destination for {caster.name} - no ground");
                return;
            }

            Vector3 finalPosition = groundHit.point + Vector3.up * 0.5f; // Offset para no quedar enterrado

            // TELEPORT
            caster.transform.position = finalPosition;

            // VFX trail (desde posición inicial hasta final)
            if (data.CastVFX != null) {
                // Spawn en mitad del camino
                Vector3 midPoint = (startPos + finalPosition) / 2f;
                GameObject vfx = Object.Instantiate(data.CastVFX, midPoint, Quaternion.LookRotation(direction));
                FishNet.InstanceFinder.ServerManager.Spawn(vfx);
                Object.Destroy(vfx, 1f);
            }

            // Opcional: Damage a enemigos en el trayecto
            if (applyDamageInPath && data.BaseDamage > 0) {
                ApplyDashDamage(caster, startPos, finalPosition, direction, data);
            }

            // Aplicar STATUS EFFECTS a sí mismo (ej: invulnerabilidad durante dash)
            if (data.ApplyToSelf != null && data.ApplyToSelf.Length > 0) {
                // TODO: StatusEffectSystem.ApplyEffects(caster, data.ApplyToSelf);
                Debug.Log($"[DashLogic] Applied {data.ApplyToSelf.Length} effects to self");
            }

            Debug.Log($"[DashLogic] {caster.name} dashed {(isBackwards ? "backwards" : "forward")} to {finalPosition}");
        }

        private void ApplyDashDamage(NetworkObject caster, Vector3 startPos, Vector3 endPos, Vector3 direction, AbilityData data) {

            float distance = Vector3.Distance(startPos, endPos);

            // SphereCast a lo largo del trayecto
            RaycastHit[] hits = Physics.SphereCastAll(startPos, data.Radius, direction, distance, LayerMask.GetMask("Enemy"));

            foreach (var hit in hits) {
                if (hit.collider.TryGetComponent(out NetworkObject netObj)) {
                    if (netObj == caster) continue; // No dañarse a sí mismo

                    if (hit.collider.TryGetComponent(out IDamageable damageable)) {
                        damageable.TakeDamage(data.BaseDamage, caster);

                        // Impact VFX
                        if (data.ImpactVFX != null) {
                            GameObject impactVfx = Object.Instantiate(data.ImpactVFX, hit.point, Quaternion.identity);
                            FishNet.InstanceFinder.ServerManager.Spawn(impactVfx);
                            Object.Destroy(impactVfx, 1f);
                        }

                        Debug.Log($"[DashLogic] {caster.name} hit {netObj.name} during dash for {data.BaseDamage} damage");
                    }
                }
            }
        }
    }
}

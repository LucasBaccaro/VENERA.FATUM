using UnityEngine;
using FishNet.Object;
using Genesis.Data;
using UnityEngine.AI;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Lógica para habilidades de movimiento/dash.
    /// Teleporta al jugador usando NavMeshAgent.Warp para evitar conflictos.
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

            Vector3 finalPosition = targetPoint;

            // 1. Validar destino usando NavMesh (más robusto que Raycast)
            if (NavMesh.SamplePosition(targetPoint, out NavMeshHit navHit, 5.0f, NavMesh.AllAreas)) {
                finalPosition = navHit.position;
            } else {
                // Fallback: Raycast físico si no hay NavMesh cerca
                if (Physics.Raycast(targetPoint + Vector3.up * 5f, Vector3.down, out RaycastHit groundHit, 10f, LayerMask.GetMask("Environment"))) {
                    finalPosition = groundHit.point;
                } else {
                    Debug.LogWarning($"[DashLogic] Invalid destination for {caster.name} - no valid ground found");
                    return;
                }
            }

            // 2. MOVER
            // Prioridad: PlayerMotorMultiplayer (Smooth Dash) > NavMeshAgent > CharacterController > Transform
            
            float dashDuration = 0.25f; // Duración fija para el dash suave

            if (caster.TryGetComponent(out PlayerMotorMultiplayer motor)) {
                motor.PerformDash(finalPosition, dashDuration);
                Debug.Log($"[DashLogic] Started smooth dash to {finalPosition}");
            }
            else if (caster.TryGetComponent(out NavMeshAgent agent)) {
                // ... resto de lógica legacy ...
                if (agent.Warp(finalPosition)) {
                    Debug.Log($"[DashLogic] Warped agent to {finalPosition}");
                } else {
                    Debug.LogWarning($"[DashLogic] Warp failed. Fallback to transform.");
                    caster.transform.position = finalPosition;
                }
            } 
            else if (caster.TryGetComponent(out CharacterController cc)) {
                // CharacterController bloquea cambios de posición si está activo
                cc.enabled = false;
                caster.transform.position = finalPosition + Vector3.up * 0.1f;
                cc.enabled = true;
                Debug.Log($"[DashLogic] Teleported CharacterController to {finalPosition}");
            }
            else {
                caster.transform.position = finalPosition + Vector3.up * 0.1f;
                Debug.Log($"[DashLogic] Moved transform (no agent/cc) to {finalPosition}");
            }

            // VFX trail (desde posición inicial hasta final)
            if (data.CastVFX != null) {
                // Spawn en mitad del camino
                Vector3 midPoint = (startPos + finalPosition) / 2f;
                GameObject vfx = Instantiate(data.CastVFX, midPoint, Quaternion.LookRotation(direction));
                
                // Solo el servidor puede spawnear NetworkObjects
                if (caster.IsServer) {
                    FishNet.InstanceFinder.ServerManager.Spawn(vfx);
                }
                
                Destroy(vfx, 1f);
            }

            // Opcional: Damage a enemigos en el trayecto (SOLO SERVIDOR)
            if (caster.IsServer && applyDamageInPath && data.BaseDamage > 0) {
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
                            GameObject impactVfx = Instantiate(data.ImpactVFX, hit.point, Quaternion.identity);
                            FishNet.InstanceFinder.ServerManager.Spawn(impactVfx);
                            Destroy(impactVfx, 1f);
                        }

                        Debug.Log($"[DashLogic] {caster.name} hit {netObj.name} during dash for {data.BaseDamage} damage");
                    }
                }
            }
        }
    }
}

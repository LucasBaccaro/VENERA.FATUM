using UnityEngine;
using FishNet.Object;
using Genesis.Data;
using System.Collections.Generic;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Lógica para habilidades de channeling (rayos continuos)
    /// Detecta enemigos en una línea recta y aplica daño por tick
    /// Ejemplos: Rayo de Hielo (Vel'Koz style)
    /// </summary>
    [CreateAssetMenu(fileName = "Logic_Channel", menuName = "Genesis/Combat/Logic/Channel")]
    public class ChannelLogic : AbilityLogic {

        [Header("Channel Settings")]
        [Tooltip("Si es true, spawna VFX de impacto en cada enemigo golpeado")]
        [SerializeField] private bool spawnImpactVFX = true;

        // Valores default para Channeling (se pueden sobrescribir en el inspector)
        private void OnEnable() {
            if (MovementGracePeriod == 0.3f) MovementGracePeriod = 0.5f; // Channeling tiene más grace period
            if (MovementThreshold == 0.1f) MovementThreshold = 0.1f;
            // CancelOnMovement = true por defecto
        }

        public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

            // Forzar dirección horizontal
            direction.y = 0;
            if (direction.sqrMagnitude < 0.001f) direction = caster.transform.forward;
            direction.Normalize();

            Vector3 origin = caster.transform.position + Vector3.up * 1.5f; // Altura del pecho
            float maxDistance = Vector3.Distance(origin, targetPoint);
            maxDistance = Mathf.Min(maxDistance, data.Range); // Clampear al rango máximo

            // Determinar qué enemigos golpear
            List<NetworkObject> hitEnemies = new List<NetworkObject>();

            if (data.ChannelHitAllTargets) {
                // HIT ALL: Usar SphereCast o RaycastAll para detectar todos
                RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxDistance, LayerMask.GetMask("Enemy", "Player"));

                foreach (var hit in hits) {
                    if (hit.collider.TryGetComponent(out NetworkObject netObj)) {
                        // Ignorar al caster
                        if (netObj == caster) continue;
                        hitEnemies.Add(netObj);
                    }
                }
            } else {
                // HIT FIRST ONLY: Usar Raycast simple
                if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, LayerMask.GetMask("Enemy", "Player"))) {
                    if (hit.collider.TryGetComponent(out NetworkObject netObj)) {
                        if (netObj != caster) {
                            hitEnemies.Add(netObj);
                        }
                    }
                }
            }

            // Aplicar daño a todos los enemigos detectados
            int hitCount = 0;
            foreach (var enemy in hitEnemies) {
                // Aplicar DAMAGE
                if (data.BaseDamage > 0) {
                    if (enemy.TryGetComponent(out IDamageable damageable)) {
                        damageable.TakeDamage(data.BaseDamage, caster);
                        hitCount++;
                    }
                }

                // Aplicar STATUS EFFECTS
                if (data.ApplyToTarget != null && data.ApplyToTarget.Length > 0) {
                    StatusEffectSystem statusSystem = enemy.GetComponent<StatusEffectSystem>();
                    if (statusSystem != null) {
                        foreach (var effectData in data.ApplyToTarget) {
                            statusSystem.ApplyEffect(effectData);
                            Debug.Log($"[ChannelLogic] Applied {effectData.Name} to {enemy.name}");
                        }
                    } else {
                        Debug.LogWarning($"[ChannelLogic] {enemy.name} has no StatusEffectSystem component!");
                    }
                }

                // Spawnar Impact VFX
                if (spawnImpactVFX && data.ImpactVFX != null) {
                    Vector3 impactPos = enemy.transform.position + Vector3.up * 1f;
                    GameObject impactVfx = Object.Instantiate(data.ImpactVFX, impactPos, Quaternion.identity);

                    // Spawn en red si tiene NetworkObject
                    if (impactVfx.TryGetComponent<NetworkObject>(out var vfxNetObj)) {
                        FishNet.InstanceFinder.ServerManager.Spawn(impactVfx);
                    }

                    Object.Destroy(impactVfx, 2f);
                }
            }

            Debug.Log($"[ChannelLogic] {caster.name} channeling {data.Name}: Hit {hitCount} enemies");
        }
    }
}

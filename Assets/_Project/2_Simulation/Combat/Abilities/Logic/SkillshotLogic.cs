using UnityEngine;
using FishNet.Object;
using Genesis.Data;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Lógica para skillshots direccionales (proyectiles en línea recta)
    /// El proyectil viaja hacia la dirección apuntada y daña al primer enemigo impactado
    /// Ejemplos: Bola de Fuego
    /// </summary>
    [CreateAssetMenu(fileName = "Logic_Skillshot", menuName = "Genesis/Combat/Logic/Skillshot")]
    public class SkillshotLogic : AbilityLogic {

        public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

            // Forzar dirección horizontal pura (paralela al suelo)
            direction.y = 0;
            if (direction.sqrMagnitude < 0.001f) direction = caster.transform.forward; // Safety
            direction.Normalize();

            // Punto de spawn: Altura del pecho (~1.5m) + un poco adelante para no chocar con el propio collider
            Vector3 spawnPos = caster.transform.position + Vector3.up * 1.5f + direction * 0.5f;

            // Validar que haya prefab asignado
            if (data.ProjectilePrefab == null) {
                Debug.LogError($"[SkillshotLogic] Ability {data.Name} missing ProjectilePrefab!");
                return;
            }

            // Instanciar proyectil
            GameObject instance = Instantiate(data.ProjectilePrefab, spawnPos, Quaternion.LookRotation(direction));

            // Configurar ProjectileController
            if (instance.TryGetComponent(out ProjectileController controller)) {
                controller.Initialize(caster, data.BaseDamage, direction * data.ProjectileSpeed, data.Radius);
            } else {
                Debug.LogError($"[SkillshotLogic] ProjectilePrefab missing ProjectileController component!");
                Destroy(instance);
                return;
            }

            // Spawn en red
            FishNet.InstanceFinder.ServerManager.Spawn(instance);

            // Cast VFX (en el caster)
            if (data.CastVFX != null) {
                GameObject castVfx = Instantiate(data.CastVFX, spawnPos, Quaternion.LookRotation(direction));
                FishNet.InstanceFinder.ServerManager.Spawn(castVfx);
                Destroy(castVfx, 1f);
            }

            Debug.Log($"[SkillshotLogic] {caster.name} cast {data.Name} in direction {direction}");
        }
    }
}

using UnityEngine;
using FishNet.Object;
using Genesis.Data;
using Genesis.Core;

namespace Genesis.Simulation.Combat {

    [CreateAssetMenu(fileName = "Logic_Projectile", menuName = "Genesis/Combat/Logic/Projectile")]
    public class ProjectileLogic : AbilityLogic {

        public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

            // Punto de origen (idealmente un bone "Hand_R", aquí simplificado)
            Vector3 spawnPos = caster.transform.position + Vector3.up * 1.5f + caster.transform.forward * 0.5f;

            // Instanciar Proyectil (debería usar Pool, por ahora directo)
            if (data.ProjectilePrefab == null) {
                Debug.LogError($"Habilidad {data.Name} no tiene ProjectilePrefab asignado!");
                return;
            }

            GameObject instance = Object.Instantiate(data.ProjectilePrefab, spawnPos, Quaternion.LookRotation(direction));

            // Configurar
            if (instance.TryGetComponent(out ProjectileController controller)) {
                controller.Initialize(caster, data.BaseDamage, direction * data.ProjectileSpeed, data.Radius, data.ApplyToTarget);
            }

            // Spawn en red
            FishNet.InstanceFinder.ServerManager.Spawn(instance);

            Debug.Log($"[ProjectileLogic] Spawned {data.Name} projectile in direction {direction}");
        }
    }
}

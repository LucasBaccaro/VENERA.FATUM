using UnityEngine;
using FishNet.Object;
using Genesis.Data;
using Genesis.Core;

namespace Genesis.Simulation.Combat {

    [CreateAssetMenu(fileName = "Logic_Projectile", menuName = "Genesis/Combat/Logic/Projectile")]
    public class ProjectileLogic : AbilityLogic {

        public override void Execute(NetworkObject caster, NetworkObject target, Vector3 groundPoint, AbilityData data) {
            
            // Punto de origen (idealmente un bone "Hand_R", aquí simplificado)
            Vector3 spawnPos = caster.transform.position + Vector3.up * 1.5f + caster.transform.forward * 0.5f;

            // Dirección
            Vector3 direction = caster.transform.forward;
            if (target != null) {
                direction = (target.transform.position + Vector3.up * 1f - spawnPos).normalized;
            } else if (groundPoint != Vector3.zero) {
                direction = (groundPoint - spawnPos).normalized;
            }

            // Instanciar Proyectil (debería usar Pool, por ahora directo)
            if (data.ProjectilePrefab == null) {
                Debug.LogError($"Habilidad {data.Name} no tiene ProjectilePrefab asignado!");
                return;
            }

            GameObject instance = Instantiate(data.ProjectilePrefab, spawnPos, Quaternion.LookRotation(direction));
            
            // Configurar
            if (instance.TryGetComponent(out ProjectileController controller)) {
                controller.Initialize(caster, data.BaseDamage, direction * data.ProjectileSpeed, 0.3f);
            }

            // Spawn en red
            FishNet.InstanceFinder.ServerManager.Spawn(instance);
        }
    }
}

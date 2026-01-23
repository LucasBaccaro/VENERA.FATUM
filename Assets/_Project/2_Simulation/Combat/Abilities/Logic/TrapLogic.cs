using UnityEngine;
using FishNet.Object;
using Genesis.Data;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Lógica para trampas (objetos persistentes que se activan al contacto)
    /// Coloca un objeto en el mundo que persiste hasta ser activado o expirar
    /// Ejemplos: Trampa de Hielo
    /// </summary>
    [CreateAssetMenu(fileName = "Logic_Trap", menuName = "Genesis/Combat/Logic/Trap")]
    public class TrapLogic : AbilityLogic {

        [Header("Trap Settings")]
        [SerializeField] private GameObject trapPrefab; // Prefab de la trampa (debe tener TrapController)
        [SerializeField] private float trapLifetime = 30f; // Segundos antes de expirar
        [SerializeField] private bool visibleToEnemies = true; // Si los enemigos ven la trampa

        public override void ExecuteDirectional(NetworkObject caster, Vector3 targetPoint, Vector3 direction, AbilityData data) {

            // Validar prefab
            GameObject prefabToUse = trapPrefab != null ? trapPrefab : data.ProjectilePrefab;

            if (prefabToUse == null) {
                Debug.LogError($"[TrapLogic] Ability {data.Name} missing trapPrefab!");
                return;
            }

            // Spawn trampa en el punto objetivo
            GameObject trap = Object.Instantiate(prefabToUse, targetPoint, Quaternion.identity);

            // Configurar TrapController
            if (trap.TryGetComponent(out TrapController controller)) {
                controller.Initialize(caster, data.BaseDamage, data.Radius, trapLifetime, data);
            } else {
                Debug.LogError($"[TrapLogic] TrapPrefab missing TrapController component!");
                Object.Destroy(trap);
                return;
            }

            // Spawn en red
            FishNet.InstanceFinder.ServerManager.Spawn(trap);

            // Cast VFX (placement)
            if (data.CastVFX != null) {
                GameObject vfx = Object.Instantiate(data.CastVFX, targetPoint, Quaternion.identity);
                FishNet.InstanceFinder.ServerManager.Spawn(vfx);
                Object.Destroy(vfx, 1f);
            }

            Debug.Log($"[TrapLogic] {caster.name} placed {data.Name} at {targetPoint}. Lifetime: {trapLifetime}s");
        }
    }
}

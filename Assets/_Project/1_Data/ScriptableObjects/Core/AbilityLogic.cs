using UnityEngine;
using FishNet.Object;

namespace Genesis.Data {

    /// <summary>
    /// Clase base abstracta para la lógica de habilidades.
    /// Las implementaciones concretas (Proyectil, Melee, AoE) vivirán en Simulation.
    /// </summary>
    public abstract class AbilityLogic : ScriptableObject {
        
        /// <summary>
        /// Ejecutado SOLO en el servidor. Implementa la mecánica principal.
        /// </summary>
        /// <param name="caster">Quien lanza la habilidad</param>
        /// <param name="target">Target seleccionado (puede ser null)</param>
        /// <param name="groundPoint">Posición en suelo (si es ground target)</param>
        /// <param name="data">Datos de configuración (daño, rango, etc)</param>
        public abstract void Execute(
            NetworkObject caster, 
            NetworkObject target, 
            Vector3 groundPoint,
            AbilityData data
        );

        /// <summary>
        /// Validación extra opcional en servidor (ej: Line of Sight, requisitos especiales)
        /// </summary>
        public virtual bool Validate(NetworkObject caster, NetworkObject target, Vector3 point) {
            return true;
        }
    }
}

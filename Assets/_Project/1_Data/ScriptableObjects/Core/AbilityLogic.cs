using UnityEngine;
using FishNet.Object;

namespace Genesis.Data {

    /// <summary>
    /// Clase base abstracta para la lógica de habilidades.
    /// Las implementaciones concretas (Proyectil, Melee, AoE) vivirán en Simulation.
    /// </summary>
    public abstract class AbilityLogic : ScriptableObject {

        [Header("Movement Interruption")]
        [Tooltip("Tiempo de gracia antes de cancelar por movimiento (segundos). 0 = cancelación inmediata")]
        public float MovementGracePeriod = 0.3f;

        [Tooltip("Si es true, el jugador debe permanecer quieto durante el cast/channel")]
        public bool CancelOnMovement = true;

        [Tooltip("Distancia mínima para considerar que el jugador se movió (metros)")]
        public float MovementThreshold = 0.1f;

        /// <summary>
        /// LEGACY: Execute con target seleccionado (mantener compatibilidad con sistema anterior)
        /// Por defecto, redirige a ExecuteDirectional calculando la dirección
        /// </summary>
        /// <param name="caster">Quien lanza la habilidad</param>
        /// <param name="target">Target seleccionado (puede ser null)</param>
        /// <param name="groundPoint">Posición en suelo (si es ground target)</param>
        /// <param name="data">Datos de configuración (daño, rango, etc)</param>
        public virtual void Execute(
            NetworkObject caster,
            NetworkObject target,
            Vector3 groundPoint,
            AbilityData data
        ) {
            // Default: Calcular dirección y redirigir a ExecuteDirectional
            Vector3 direction = caster.transform.forward;
            Vector3 targetPoint = groundPoint;

            if (target != null) {
                direction = (target.transform.position - caster.transform.position).normalized;
                targetPoint = target.transform.position;
            } else if (groundPoint != Vector3.zero) {
                direction = (groundPoint - caster.transform.position).normalized;
            }

            ExecuteDirectional(caster, targetPoint, direction, data);
        }

        /// <summary>
        /// NEW: Execute direccional (para skillshots y habilidades con indicadores)
        /// CRITICAL: Solo llamar en SERVER
        /// </summary>
        /// <param name="caster">Quien lanza la habilidad</param>
        /// <param name="targetPoint">Punto objetivo (puede ser posición de enemigo o punto en el suelo)</param>
        /// <param name="direction">Dirección normalizada de la habilidad</param>
        /// <param name="data">Datos de configuración</param>
        public abstract void ExecuteDirectional(
            NetworkObject caster,
            Vector3 targetPoint,
            Vector3 direction,
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

using UnityEngine;
using Genesis.Data;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Clase base abstracta para todos los indicadores visuales de habilidades.
    /// Los indicadores muestran el área/dirección de efecto antes de confirmar la habilidad.
    /// </summary>
    public abstract class AbilityIndicator : MonoBehaviour {

        [Header("Visual Settings")]
        [SerializeField] protected Color validColor = new Color(0, 1, 0, 0.3f);
        [SerializeField] protected Color invalidColor = new Color(1, 0, 0, 0.3f);
        [SerializeField] protected LayerMask obstacleMask;

        protected bool _isActive;
        protected bool _isValid;
        protected AbilityData _abilityData;

        /// <summary>
        /// Inicializa el indicador con los datos de la habilidad
        /// </summary>
        public abstract void Initialize(AbilityData abilityData);

        /// <summary>
        /// Actualiza la posición/orientación del indicador basado en el mouse
        /// </summary>
        /// <param name="worldPoint">Punto en el mundo donde apunta el mouse</param>
        /// <param name="direction">Dirección desde el caster hacia el punto</param>
        public abstract void UpdatePosition(Vector3 worldPoint, Vector3 direction);

        /// <summary>
        /// Obtiene el punto objetivo final (para enviar al servidor)
        /// </summary>
        public abstract Vector3 GetTargetPoint();

        /// <summary>
        /// Obtiene la dirección normalizada (para enviar al servidor)
        /// </summary>
        public abstract Vector3 GetDirection();

        /// <summary>
        /// Verifica si la posición actual es válida (no obstruida, en rango, etc)
        /// </summary>
        public abstract bool IsValid();

        /// <summary>
        /// Muestra el indicador
        /// </summary>
        public virtual void Show() {
            _isActive = true;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Oculta el indicador
        /// </summary>
        public virtual void Hide() {
            _isActive = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Helper: Actualiza el color de un material según validez
        /// </summary>
        protected void UpdateMaterialColor(Material material) {
            if (material != null) {
                material.color = _isValid ? validColor : invalidColor;
            }
        }

        /// <summary>
        /// Helper: Actualiza el color de un renderer según validez
        /// </summary>
        protected void UpdateRendererColor(Renderer renderer) {
            if (renderer != null && renderer.material != null) {
                renderer.material.color = _isValid ? validColor : invalidColor;
            }
        }
    }
}

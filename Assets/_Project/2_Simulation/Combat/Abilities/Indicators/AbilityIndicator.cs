using UnityEngine;
using Genesis.Data;

namespace Genesis.Simulation.Combat
{
    /// <summary>
    /// Clase base abstracta para todos los indicadores visuales de habilidades.
    /// Responsabilidades:
    /// - Manejar estado activo/validez.
    /// - Proveer throttle de validación (para evitar spikes por física).
    /// - Cambiar color SIN instanciar materiales (MaterialPropertyBlock).
    /// </summary>
    public abstract class AbilityIndicator : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] protected Color validColor = new Color(0, 1, 0, 0.30f);
        [SerializeField] protected Color invalidColor = new Color(1, 0, 0, 0.30f);

        [Tooltip("Máscara para obstáculos (paredes, props sólidos, etc). Cada indicador decide si la usa.")]
        [SerializeField] protected LayerMask obstacleMask;

        [Header("Performance")]
        [Tooltip("Cada cuánto recalcular la validez (segundos). 0.05 = 20Hz.")]
        [SerializeField] protected float validationInterval = 0.05f;


[Header("Ground Projection")]
[Tooltip("Layers considerados 'suelo' para proyectar el indicador (ej: Environment/Ground).")]
[SerializeField] protected LayerMask groundMask = ~0;

[Tooltip("Altura desde la cual se dispara el ray hacia abajo para buscar suelo.")]
[SerializeField] protected float groundRayHeight = 100f;

[Tooltip("Distancia total del ray hacia abajo. Debe ser > groundRayHeight.")]
[SerializeField] protected float groundRayDistance = 250f;

[Tooltip("Offset vertical para evitar z-fighting con el piso.")]
[SerializeField] protected float groundProjectionOffset = 0.02f;


        protected bool _isActive;
        protected bool _isValid;
        protected AbilityData _abilityData;

        // Throttle
        private float _nextValidationTime;

        // Para evitar tocar color cada frame si no cambió
        private bool _lastAppliedValidState;
        private bool _hasAppliedStateOnce;

        // MPB para evitar renderer.material (instancias/GC)
        private MaterialPropertyBlock _mpb;

        // Color property ids comunes
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP Lit
        private static readonly int ColorId     = Shader.PropertyToID("_Color");     // Standard/Unlit
        private static readonly int TintId      = Shader.PropertyToID("_TintColor"); // algunos shaders

        protected virtual void Awake()
        {
	groundMask = LayerMask.GetMask("Ground", "Environment");
            _mpb = new MaterialPropertyBlock();
        }

        /// <summary>Inicializa el indicador con los datos de la habilidad</summary>
        public abstract void Initialize(AbilityData abilityData);

        /// <summary>
        /// Actualiza la posición/orientación del indicador basado en el mouse
        /// worldPoint: punto del mundo donde apunta el mouse (ya resuelto por tu AbilityIndicatorSystem)
        /// direction: dirección desde el caster hacia el punto (normalizada / plana)
        /// </summary>
        public abstract void UpdatePosition(Vector3 worldPoint, Vector3 direction);

        /// <summary>Obtiene el punto objetivo final (para enviar al servidor)</summary>
        public abstract Vector3 GetTargetPoint();

        /// <summary>Obtiene la dirección normalizada (para enviar al servidor)</summary>
        public abstract Vector3 GetDirection();

        /// <summary>Verifica si la posición actual es válida</summary>
        public abstract bool IsValid();

        public virtual void Show()
        {
            _isActive = true;
            gameObject.SetActive(true);

            // Forzar re-aplicar color cuando aparece
            _hasAppliedStateOnce = false;
        }

        public virtual void Hide()
        {
            _isActive = false;
            gameObject.SetActive(false);
        }

/// <summary>
/// Proyecta un punto del mundo hacia el suelo usando un raycast SIEMPRE desde arriba.
/// Devuelve true si encontró suelo; el punto resultante queda con un pequeño offset en Y.
/// Esto evita "stuck" cuando el mouse pasa por múltiples alturas.
/// </summary>
protected bool TryProjectToGround(Vector3 worldPoint, out Vector3 projectedPoint)
{
    Vector3 origin = worldPoint + Vector3.up * groundRayHeight;

    if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
    {
        projectedPoint = hit.point;
        projectedPoint.y += groundProjectionOffset ;
        return true;
    }

    // Fallback: al menos devolvemos el worldPoint (para que el indicador no se congele)
    projectedPoint = worldPoint;
    projectedPoint.y += groundProjectionOffset;
    return false;
}


        // =========================================================
        // THROTTLE
        // =========================================================

        /// <summary>
        /// Llamalo desde tus indicadores: si devuelve true, es momento de recalcular _isValid.
        /// </summary>
        protected bool ShouldValidate()
        {
            if (validationInterval <= 0f)
                return true;

            if (Time.time < _nextValidationTime)
                return false;

            _nextValidationTime = Time.time + validationInterval;
            return true;
        }

        // =========================================================
        // COLOR HELPERS (NO ALLOC)
        // =========================================================

        /// <summary>
        /// Actualiza color sobre un Renderer sin instanciar materiales.
        /// Ideal: llamarlo cada frame NO duele, pero mejor si lo llamás sólo cuando cambia _isValid.
        /// Este método ya incluye ese "only on change".
        /// </summary>
        protected void UpdateRendererColor(Renderer renderer)
        {
            if (renderer == null)
                return;

            // Evitar trabajo si el estado no cambió
            if (_hasAppliedStateOnce && _lastAppliedValidState == _isValid)
                return;

            _hasAppliedStateOnce = true;
            _lastAppliedValidState = _isValid;

            Color c = _isValid ? validColor : invalidColor;

            // Si no hay material asignado, no podemos setear
            var mat = renderer.sharedMaterial;
            if (mat == null)
                return;

            // Seteo via MPB al property que exista
            renderer.GetPropertyBlock(_mpb);

            bool set = false;
            if (mat.HasProperty(BaseColorId))
            {
                _mpb.SetColor(BaseColorId, c);
                set = true;
            }
            else if (mat.HasProperty(ColorId))
            {
                _mpb.SetColor(ColorId, c);
                set = true;
            }
            else if (mat.HasProperty(TintId))
            {
                _mpb.SetColor(TintId, c);
                set = true;
            }

            if (set)
                renderer.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// Si algún indicador trabaja directo con Material (no recomendado), al menos evitá tocar .color
        /// si el shader no lo soporta. Igual: preferí UpdateRendererColor.
        /// </summary>
        protected void UpdateMaterialColor(Material material)
        {
            if (material == null)
                return;

            Color c = _isValid ? validColor : invalidColor;

            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, c);
            else if (material.HasProperty(ColorId))
                material.SetColor(ColorId, c);
            else if (material.HasProperty(TintId))
                material.SetColor(TintId, c);
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using Genesis.Data;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Sistema central de gestión de indicadores visuales de habilidades.
    /// Instancia, actualiza y destruye indicadores según el tipo de habilidad.
    /// </summary>
    public class AbilityIndicatorSystem : MonoBehaviour {

        [Header("Indicator Prefabs")]
        [SerializeField] private GameObject lineIndicatorPrefab;
        [SerializeField] private GameObject circleIndicatorPrefab;
        [SerializeField] private GameObject coneIndicatorPrefab;
        [SerializeField] private GameObject arrowIndicatorPrefab;
        [SerializeField] private GameObject trapIndicatorPrefab;

        private AbilityIndicator _currentIndicator;
        private Camera _mainCamera;
        private Transform _playerTransform;

        void Awake() {
            _mainCamera = Camera.main;
        }

        /// <summary>
        /// Muestra el indicador apropiado para la habilidad especificada
        /// </summary>
        /// <param name="ability">Datos de la habilidad</param>
        /// <param name="playerTransform">Transform del jugador (parent del indicador)</param>
        public void ShowIndicator(AbilityData ability, Transform playerTransform) {
            HideIndicator();

            _playerTransform = playerTransform;

            GameObject prefab = GetPrefabForAbility(ability);
            if (prefab == null) {
                Debug.LogWarning($"[AbilityIndicatorSystem] No prefab found for IndicatorType: {ability.IndicatorType}");
                return;
            }

            // Instanciar indicador con un pequeño offset vertical para que no se entierre
            Vector3 spawnPos = playerTransform.position + Vector3.up * 1f;
            GameObject instance = Instantiate(prefab, spawnPos, Quaternion.identity);
            _currentIndicator = instance.GetComponent<AbilityIndicator>();

            if (_currentIndicator != null) {
                _currentIndicator.Initialize(ability);
                
                // PARENTING LOGIC:
                // - Circle indicators ground-targeted (Meteorito) NO se parentean para evitar heredar escala del Player
                //   y así coincidir con el AOE Warning (que es un objeto de mundo)
                // - Line, Arrow, Cone, Trap, y Self-centered SÍ se parentean para seguir al jugador
                bool shouldUnparent = (ability.IndicatorType == IndicatorType.Circle && ability.TargetingMode == TargetType.Ground);
                
                if (shouldUnparent) {
                    _currentIndicator.transform.SetParent(null); // Objeto en el mundo
                    _currentIndicator.transform.position = spawnPos;
                } else {
                    // Parentear al jugador (Line, Arrow, Cone, Trap, Self)
                    _currentIndicator.transform.SetParent(playerTransform);
                    _currentIndicator.transform.localPosition = new Vector3(0f, 1f, 0f);
                }
                
                _currentIndicator.Show();

                Debug.Log($"[AbilityIndicatorSystem] Showing {ability.IndicatorType} indicator for {ability.Name}");
            } else {
                Debug.LogError($"[AbilityIndicatorSystem] Prefab {prefab.name} missing AbilityIndicator component!");
                Destroy(instance);
            }
        }

        /// <summary>
        /// Oculta y destruye el indicador actual
        /// </summary>
        public void HideIndicator() {
            if (_currentIndicator != null) {
                _currentIndicator.Hide();
                Destroy(_currentIndicator.gameObject);
                _currentIndicator = null;
            }
        }

        /// <summary>
        /// Actualiza la posición del indicador basado en la posición del mouse
        /// Llamar cada frame durante aiming mode
        /// </summary>
        /// <param name="mouseScreenPosition">Posición del mouse en pantalla</param>
        public void UpdateIndicator(Vector2 mouseScreenPosition) {
    if (_currentIndicator == null || _mainCamera == null || _playerTransform == null) return;

    Ray ray = _mainCamera.ScreenPointToRay(mouseScreenPosition);



    LayerMask groundMask = LayerMask.GetMask("Ground");

    // Raycast al mundo para obtener punto en el suelo (Ground) incluso si el mouse está sobre Environment (pared/casa)


// --- AIM POINT: para indicadores de línea tipo proyectil ---
// Queremos un punto de aim aunque el mouse esté sobre una pared/casa.
// Así la dirección sale bien siempre.

Vector3 targetPoint;

LayerMask aimMask = LayerMask.GetMask("Ground", "Environment"); // <- ambos
const float maxAimDistance = 1000f;

if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore)) {
    targetPoint = hit.point;
}
else {
    // Fallback: plano virtual a la altura del jugador
    Plane groundPlane = new Plane(Vector3.up, _playerTransform.position);
    if (groundPlane.Raycast(ray, out float enter)) {
        targetPoint = ray.GetPoint(enter);
    } else {
        return;
    }
}

// (Opcional pero recomendado) levantar apenas el targetPoint para evitar z-fighting / micro intersecciones visuales
targetPoint.y += 0.05f;


    Vector3 diff = targetPoint - _playerTransform.position;
    diff.y = 0;
    Vector3 direction = diff.normalized;

    _currentIndicator.UpdatePosition(targetPoint, direction);
}

        /// <summary>
        /// Obtiene el indicador actualmente activo (null si no hay)
        /// </summary>
        public AbilityIndicator GetCurrentIndicator() => _currentIndicator;

        /// <summary>
        /// Verifica si hay un indicador activo
        /// </summary>
        public bool HasActiveIndicator() => _currentIndicator != null;

        /// <summary>
        /// Obtiene el prefab apropiado según el tipo de indicador de la habilidad
        /// </summary>
        private GameObject GetPrefabForAbility(AbilityData ability) {
            switch (ability.IndicatorType) {
                case IndicatorType.Line:
                    return lineIndicatorPrefab;

                case IndicatorType.Circle:
                    return circleIndicatorPrefab;

                case IndicatorType.Cone:
                    return coneIndicatorPrefab;

                case IndicatorType.Arrow:
                    return arrowIndicatorPrefab;

                case IndicatorType.Trap:
                    return trapIndicatorPrefab;

                case IndicatorType.None:
                    // Targeted abilities no tienen indicador
                    return null;

                default:
                    Debug.LogWarning($"[AbilityIndicatorSystem] Unknown IndicatorType: {ability.IndicatorType}");
                    return null;
            }
        }

        void OnDestroy() {
            HideIndicator();
        }
    }
}

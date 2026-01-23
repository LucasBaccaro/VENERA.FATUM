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

            // Instanciar indicador
            GameObject instance = Instantiate(prefab, playerTransform.position, Quaternion.identity);
            _currentIndicator = instance.GetComponent<AbilityIndicator>();

            if (_currentIndicator != null) {
                _currentIndicator.Initialize(ability);
                _currentIndicator.transform.SetParent(playerTransform);
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

            // Raycast al suelo para obtener punto en el mundo
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, LayerMask.GetMask("Environment"))) {
                Vector3 direction = (hit.point - _playerTransform.position).normalized;
                _currentIndicator.UpdatePosition(hit.point, direction);
            } else {
                // Si no hay suelo, usar un plano virtual a altura del jugador
                Plane groundPlane = new Plane(Vector3.up, _playerTransform.position);
                if (groundPlane.Raycast(ray, out float enter)) {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    Vector3 direction = (hitPoint - _playerTransform.position).normalized;
                    _currentIndicator.UpdatePosition(hitPoint, direction);
                }
            }
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

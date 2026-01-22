using UnityEngine;
using UnityEngine.InputSystem;

namespace Genesis.Core {

    /// <summary>
    /// Wrapper para el New Input System.
    /// Proporciona una API simple para acceder al input del jugador.
    /// NOTA: Usa InputActionAsset directamente (no requiere clase generada)
    /// </summary>
    public class InputManager : Singleton<InputManager> {

        [Header("Input Configuration")]
        [SerializeField] private InputActionAsset inputActions;

        private InputActionMap _playerActionMap;
        private InputAction _moveAction;

        // Cached values
        private Vector2 _moveInput;

        // ═══════════════════════════════════════════════════════
        // PROPERTIES (Public Read-Only)
        // ═══════════════════════════════════════════════════════

        public Vector2 MoveInput => _moveInput;
        public float Horizontal => _moveInput.x;
        public float Vertical => _moveInput.y;

        // ═══════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════

        protected override void Awake() {
            base.Awake();

            if (inputActions == null) {
                Debug.LogError("[InputManager] InputActionAsset no asignado! Asigna 'InputSystem_Actions' en el Inspector.");
                return;
            }

            // Obtener action map y actions
            _playerActionMap = inputActions.FindActionMap("Player");

            if (_playerActionMap == null) {
                Debug.LogError("[InputManager] Action Map 'Player' no encontrado!");
                return;
            }

            _moveAction = _playerActionMap.FindAction("Move");

            if (_moveAction == null) {
                Debug.LogError("[InputManager] Action 'Move' no encontrado!");
                return;
            }

            // Suscribirse a eventos de input
            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;

            // Habilitar el mapa por defecto al iniciar
            _playerActionMap.Enable();

            Debug.Log("[InputManager] Initialized successfully");
        }

        void OnEnable() {
            if (_playerActionMap != null) {
                _playerActionMap.Enable();
            }
        }

        void OnDisable() {
            if (_playerActionMap != null) {
                _playerActionMap.Disable();
            }
        }

        protected override void OnDestroy() {
            base.OnDestroy();

            // Desuscribirse
            if (_moveAction != null) {
                _moveAction.performed -= OnMovePerformed;
                _moveAction.canceled -= OnMoveCanceled;
            }
        }

        // ═══════════════════════════════════════════════════════
        // INPUT CALLBACKS
        // ═══════════════════════════════════════════════════════

        private void OnMovePerformed(InputAction.CallbackContext context) {
            _moveInput = context.ReadValue<Vector2>();
        }

        private void OnMoveCanceled(InputAction.CallbackContext context) {
            _moveInput = Vector2.zero;
        }

        // ═══════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Obtiene el input de movimiento normalizado (WASD/Flechas)
        /// </summary>
        public Vector2 GetMoveInput() {
            return _moveInput;
        }

        /// <summary>
        /// Obtiene el input como Vector3 (para movimiento 3D - Y siempre 0)
        /// </summary>
        public Vector3 GetMoveInput3D() {
            return new Vector3(_moveInput.x, 0, _moveInput.y);
        }

        /// <summary>
        /// Verifica si hay input de movimiento
        /// </summary>
        public bool IsMoving() {
            return _moveInput.sqrMagnitude > 0.01f;
        }

        /// <summary>
        /// Cambia entre el action map Player y UI
        /// </summary>
        public void SetPlayerControlsEnabled(bool enabled) {
            if (enabled) {
                inputActions.FindActionMap("Player")?.Enable();
                inputActions.FindActionMap("UI")?.Disable();
            } else {
                inputActions.FindActionMap("Player")?.Disable();
                inputActions.FindActionMap("UI")?.Enable();
            }
        }

        // ═══════════════════════════════════════════════════════
        // ACTIONS ACCESS (Para fases futuras)
        // ═══════════════════════════════════════════════════════

        public InputAction GetAction(string actionName) {
            return _playerActionMap?.FindAction(actionName);
        }

        public InputActionAsset GetInputActions() => inputActions;
    }
}

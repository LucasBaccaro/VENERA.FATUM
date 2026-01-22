using UnityEngine;
using UnityEngine.UIElements;
using Genesis.Core;
using Genesis.Simulation;

namespace Genesis.Presentation.UI {

    /// <summary>
    /// Controlador del HUD principal del jugador.
    /// Muestra barras de HP/Mana y se actualiza mediante eventos del EventBus.
    /// </summary>
    public class HUDController : MonoBehaviour {

        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        // Referencias a elementos UI
        private VisualElement _root;
        private ProgressBar _healthBar;
        private ProgressBar _manaBar;
        private Label _healthText;
        private Label _manaText;

        // Player reference
        private PlayerStats _localPlayerStats;

        // ═══════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════

        void OnEnable() {
            // Suscribirse a eventos
            EventBus.Subscribe<float, float>("OnHealthChanged", OnHealthChanged);
            EventBus.Subscribe<float, float>("OnManaChanged", OnManaChanged);
        }

        void OnDisable() {
            // Desuscribirse
            EventBus.Unsubscribe<float, float>("OnHealthChanged", OnHealthChanged);
            EventBus.Unsubscribe<float, float>("OnManaChanged", OnManaChanged);
        }

        void Start() {
            if (uiDocument == null) {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null) {
                Debug.LogError("[HUDController] No se encontró UIDocument!");
                return;
            }

            InitializeUI();
        }

        // ═══════════════════════════════════════════════════════
        // UI INITIALIZATION
        // ═══════════════════════════════════════════════════════

        private void InitializeUI() {
            _root = uiDocument.rootVisualElement;

            // Buscar elementos por nombre
            _healthBar = _root.Q<ProgressBar>("HealthBar");
            _manaBar = _root.Q<ProgressBar>("ManaBar");
            _healthText = _root.Q<Label>("HealthText");
            _manaText = _root.Q<Label>("ManaText");

            // Validar que existan
            if (_healthBar == null) {
                Debug.LogWarning("[HUDController] HealthBar no encontrado en UXML");
            }

            if (_manaBar == null) {
                Debug.LogWarning("[HUDController] ManaBar no encontrado en UXML");
            }

            // Valores iniciales
            SetHealth(100f, 100f);
            SetMana(100f, 100f);
        }

        // ═══════════════════════════════════════════════════════
        // EVENT HANDLERS
        // ═══════════════════════════════════════════════════════

        private void OnHealthChanged(float current, float max) {
            SetHealth(current, max);
        }

        private void OnManaChanged(float current, float max) {
            SetMana(current, max);
        }

        // ═══════════════════════════════════════════════════════
        // UI UPDATES
        // ═══════════════════════════════════════════════════════

        private void SetHealth(float current, float max) {
            if (_healthBar != null) {
                _healthBar.value = (current / max) * 100f;
                _healthBar.title = $"{current:F0} / {max:F0}";
            }

            if (_healthText != null) {
                _healthText.text = $"HP: {current:F0} / {max:F0}";
            }
        }

        private void SetMana(float current, float max) {
            if (_manaBar != null) {
                _manaBar.value = (current / max) * 100f;
                _manaBar.title = $"{current:F0} / {max:F0}";
            }

            if (_manaText != null) {
                _manaText.text = $"MP: {current:F0} / {max:F0}";
            }
        }

        // ═══════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════

        public void SetPlayerStats(PlayerStats stats) {
            _localPlayerStats = stats;

            if (stats != null) {
                SetHealth(stats.CurrentHealth, stats.MaxHealth);
                SetMana(stats.CurrentMana, stats.MaxMana);
            }
        }
    }
}

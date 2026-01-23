using UnityEngine;
using UnityEngine.UIElements;
using Genesis.Core;
using Genesis.Simulation;
using FishNet.Object; // Necesario para NetworkObject

namespace Genesis.Presentation.UI {

    /// <summary>
    /// Controlador del HUD principal del jugador.
    /// Muestra barras de HP/Mana y se actualiza mediante eventos del EventBus.
    /// Actualizado Fase 3: Soporte para Targeting.
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

        // Cast & GCD bars
        private ProgressBar _castBar;
        private ProgressBar _gcdBar;
        private Label _castText;
        private Label _gcdText;

        // Targeting UI (Fase 3)
        private VisualElement _targetFrame;
        private Label _targetNameLabel;
        private ProgressBar _targetHealthBar;

        // Player reference
        private PlayerStats _localPlayerStats;

        // ═══════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════

        void OnEnable() {
            // Stats
            EventBus.Subscribe<float, float>("OnHealthChanged", OnHealthChanged);
            EventBus.Subscribe<float, float>("OnManaChanged", OnManaChanged);
            
            // Targeting
            EventBus.Subscribe<NetworkObject>("OnTargetChanged", OnTargetChanged);
            EventBus.Subscribe("OnTargetCleared", OnTargetCleared);
        }

        void OnDisable() {
            // Stats
            EventBus.Unsubscribe<float, float>("OnHealthChanged", OnHealthChanged);
            EventBus.Unsubscribe<float, float>("OnManaChanged", OnManaChanged);
            
            // Targeting
            EventBus.Unsubscribe<NetworkObject>("OnTargetChanged", OnTargetChanged);
            EventBus.Unsubscribe("OnTargetCleared", OnTargetCleared);
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

            // Stats
            _healthBar = _root.Q<ProgressBar>("HealthBar");
            _manaBar = _root.Q<ProgressBar>("ManaBar");
            _healthText = _root.Q<Label>("HealthText");
            _manaText = _root.Q<Label>("ManaText");

            // Cast & GCD
            _castBar = _root.Q<ProgressBar>("CastBar");
            _gcdBar = _root.Q<ProgressBar>("GCDBar");
            _castText = _root.Q<Label>("CastText");
            _gcdText = _root.Q<Label>("GCDText");

            // Targeting
            _targetFrame = _root.Q<VisualElement>("TargetFrame"); // Asumimos que existe o lo crearemos en UXML
            if (_targetFrame != null) {
                _targetNameLabel = _targetFrame.Q<Label>("TargetName");
                _targetHealthBar = _targetFrame.Q<ProgressBar>("TargetHealth");
                _targetFrame.style.display = DisplayStyle.None; // Ocultar por defecto
            }

            // Valores iniciales
            SetHealth(100f, 100f);
            SetMana(100f, 100f);
            SetCastProgress(0f, "");
            SetGCDProgress(0f);
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

        private void OnTargetChanged(NetworkObject target) {
            if (_targetFrame == null) return;
            
            _targetFrame.style.display = DisplayStyle.Flex;
            if (_targetNameLabel != null) _targetNameLabel.text = target.name;
            
            // TODO: Suscribirse a cambios de vida del target
            // Por ahora solo mostramos el nombre
            Debug.Log($"[HUD] Target Selected: {target.name}");
        }

        private void OnTargetCleared() {
            if (_targetFrame == null) return;
            _targetFrame.style.display = DisplayStyle.None;
            Debug.Log("[HUD] Target Cleared");
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

        public void SetPlayerStats(PlayerStats stats) {
            _localPlayerStats = stats;
            if (stats != null) {
                SetHealth(stats.CurrentHealth, stats.MaxHealth);
                SetMana(stats.CurrentMana, stats.MaxMana);
            }
        }

        // ═══════════════════════════════════════════════════════
        // CAST & GCD BARS (Called by AbilityBarDebugController)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Actualiza la barra de casteo (0-100%)
        /// </summary>
        public void SetCastProgress(float percent, string abilityName) {
            if (_castBar != null) {
                _castBar.value = percent;
                _castBar.title = percent > 0 ? $"{percent:F0}%" : "";
            }

            if (_castText != null) {
                _castText.text = !string.IsNullOrEmpty(abilityName) ? $"Casting: {abilityName}" : "";
            }
        }

        /// <summary>
        /// Actualiza la barra de GCD (0-100%)
        /// </summary>
        public void SetGCDProgress(float percent) {
            if (_gcdBar != null) {
                _gcdBar.value = percent;
                _gcdBar.title = percent > 0 ? $"{percent:F0}%" : "";
            }

            if (_gcdText != null) {
                _gcdText.text = percent > 0 ? "Active" : "";
            }
        }
    }
}

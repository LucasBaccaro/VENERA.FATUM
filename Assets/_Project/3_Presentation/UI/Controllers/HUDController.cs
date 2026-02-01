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
        private Label _gcdText;
        private Label _notificationLabel;

        // Cast & GCD bars
        private ProgressBar _castBar;
        private ProgressBar _gcdBar;
        private Label _castText;

        [Header("Stylized Profile")]
        private Label _levelLabel;
        private Label _playerNameLabel;
        private VisualElement _classIcon;
        private VisualElement _portraitIcon;

        [Header("Stylized Bars")]
        private VisualElement _healthBarMask;
        private VisualElement _manaBarMask;
        private VisualElement _expBarMask;
        private Label _healthTextStylized;
        private Label _manaTextStylized;

        // Targeting UI (Fase 3)
        private VisualElement _targetFrame;
        private Label _targetNameLabel;
        private ProgressBar _targetHealthBar;
        private Label _targetHealthText;
        private ProgressBar _targetManaBar;

        // Player & Target references
        private PlayerStats _localPlayerStats;
        private NetworkObject _currentTarget;
        private PlayerStats _currentTargetStats;

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

            // Class
            EventBus.Subscribe<string, Sprite>("OnClassChanged", OnClassChanged);

            // Errors
            EventBus.Subscribe<string>("OnCombatError", OnCombatError);
        }

        void OnDisable() {
            // Stats
            EventBus.Unsubscribe<float, float>("OnHealthChanged", OnHealthChanged);
            EventBus.Unsubscribe<float, float>("OnManaChanged", OnManaChanged);
            
            // Targeting
            EventBus.Unsubscribe<NetworkObject>("OnTargetChanged", OnTargetChanged);
            EventBus.Unsubscribe("OnTargetCleared", OnTargetCleared);

            // Class
            EventBus.Unsubscribe<string, Sprite>("OnClassChanged", OnClassChanged);

            // Errors
            EventBus.Unsubscribe<string>("OnCombatError", OnCombatError);
        }

        void Update() {
            UpdateTargetUI();
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
            // Stats (Stylized)
            _healthBarMask = _root.Q<VisualElement>("HealthBar_Mask");
            _manaBarMask = _root.Q<VisualElement>("ManaBar_Mask");
            _expBarMask = _root.Q<VisualElement>("ExpBar_Mask");
            _healthTextStylized = _root.Q<Label>("HealthText_Stylized");
            _manaTextStylized = _root.Q<Label>("ManaText_Stylized");

            // Profile (Stylized)
            _levelLabel = _root.Q<Label>("LevelLabel");
            _playerNameLabel = _root.Q<Label>("PlayerName");
            _classIcon = _root.Q<VisualElement>("ClassIcon");
            _portraitIcon = _root.Q<VisualElement>("PortraitIcon");

            // Cast & GCD
            _castBar = _root.Q<ProgressBar>("CastBar");
            _gcdBar = _root.Q<ProgressBar>("GCDBar");
            _castText = _root.Q<Label>("CastText");
            _gcdText = _root.Q<Label>("GCDText");
            _notificationLabel = _root.Q<Label>("NotificationLabel");

            // Targeting
            _targetFrame = _root.Q<VisualElement>("TargetFrame");
            if (_targetFrame != null) {
                _targetNameLabel = _targetFrame.Q<Label>("TargetName");
                _targetHealthBar = _targetFrame.Q<ProgressBar>("TargetHealth");
                _targetHealthText = _targetFrame.Q<Label>("TargetHealthText");
                _targetManaBar = _targetFrame.Q<ProgressBar>("TargetMana");
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
            if (_targetFrame == null || target == null) return;
            
            _currentTarget = target;
            _currentTargetStats = target.GetComponent<PlayerStats>();
            
            _targetFrame.style.display = DisplayStyle.Flex;
            
            // Determinar nombre
            if (target.CompareTag("Player")) {
                _targetNameLabel.text = "Player_Enemy";
            } else if (target.name.Contains("Dummy")) {
                // Limpiar el "(Clone)" si existe
                string cleanName = target.name.Replace("(Clone)", "").Trim();
                _targetNameLabel.text = cleanName;
            } else {
                _targetNameLabel.text = target.name.Replace("(Clone)", "").Trim();
            }
            
            UpdateTargetUI();
            Debug.Log($"[HUD] Target Selected: {_targetNameLabel.text}");
        }

        private void OnTargetCleared() {
            _currentTarget = null;
            _currentTargetStats = null;
            
            if (_targetFrame != null) {
                _targetFrame.style.display = DisplayStyle.None;
            }
            Debug.Log("[HUD] Target Cleared");
        }

        private void UpdateTargetUI() {
            if (_currentTarget == null || _targetFrame == null || _targetFrame.style.display == DisplayStyle.None) return;

            if (_currentTargetStats != null) {
                // Actualizar Vida
                float hpPercent = (_currentTargetStats.CurrentHealth / _currentTargetStats.MaxHealth) * 100f;
                if (_targetHealthBar != null) _targetHealthBar.value = hpPercent;
                if (_targetHealthText != null) _targetHealthText.text = $"{_currentTargetStats.CurrentHealth:F0} / {_currentTargetStats.MaxHealth:F0}";

                // Actualizar Maná (Solo si tiene maná máximo > 0)
                if (_currentTargetStats.MaxMana > 0) {
                    if (_targetManaBar != null) {
                        _targetManaBar.style.display = DisplayStyle.Flex;
                        _targetManaBar.value = (_currentTargetStats.CurrentMana / _currentTargetStats.MaxMana) * 100f;
                    }
                } else {
                    if (_targetManaBar != null) _targetManaBar.style.display = DisplayStyle.None;
                }
            } else {
                // Si no tiene PlayerStats (ej: un objeto decorativo targeteable), ocultar barras o mostrar 0
                if (_targetHealthBar != null) _targetHealthBar.value = 0;
                if (_targetHealthText != null) _targetHealthText.text = "- / -";
                if (_targetManaBar != null) _targetManaBar.style.display = DisplayStyle.None;
            }
        }

        private void OnClassChanged(string className, Sprite classIcon) {
            if (_classIcon != null && classIcon != null) {
                _classIcon.style.backgroundImage = new StyleBackground(classIcon);
            }
            Debug.Log($"[HUD] Class UI Update: {className}");
        }

        private void OnCombatError(string message) {
            ShowNotification(message);
        }

        private void ShowNotification(string message) {
            if (_notificationLabel == null) return;

            _notificationLabel.text = message;
            _notificationLabel.style.opacity = 1;

            // Cancelar invocaciones previas para no ocultar antes de tiempo si hay spam
            CancelInvoke(nameof(HideNotification));
            Invoke(nameof(HideNotification), 2f);
        }

        private void HideNotification() {
            if (_notificationLabel != null) {
                _notificationLabel.style.opacity = 0;
            }
        }

        // ═══════════════════════════════════════════════════════
        // UI UPDATES
        // ═══════════════════════════════════════════════════════

        private void SetHealth(float current, float max) {
            if (_healthBarMask != null) {
                float percent = Mathf.Clamp((current / max) * 100f, 0, 100);
                _healthBarMask.style.width = new Length(percent, LengthUnit.Percent);
            }

            if (_healthTextStylized != null) {
                _healthTextStylized.text = $"{current:F0} / {max:F0}";
            }
        }

        private void SetMana(float current, float max) {
            if (_manaBarMask != null) {
                float percent = Mathf.Clamp((current / max) * 100f, 0, 100);
                _manaBarMask.style.width = new Length(percent, LengthUnit.Percent);
            }

            if (_manaTextStylized != null) {
                _manaTextStylized.text = $"{current:F0} / {max:F0}";
            }
        }

        /// <summary>
        /// Actualiza la barra de experiencia (0-100%)
        /// </summary>
        public void SetExperience(float percent) {
            if (_expBarMask != null) {
                float clampedPercent = Mathf.Clamp(percent, 0, 100);
                _expBarMask.style.width = new Length(clampedPercent, LengthUnit.Percent);
            }
        }

        public void SetLevel(int level) {
            if (_levelLabel != null) {
                _levelLabel.text = level.ToString();
            }
        }

        public void SetPlayerName(string playerName) {
            if (_playerNameLabel != null) {
                _playerNameLabel.text = playerName;
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

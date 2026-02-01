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
        private Label _notificationLabel;

        // Cast & GCD bars
        private VisualElement _castGroup;
        private VisualElement _castBarMask;
        private VisualElement _castBarFill;
        private Label _castAbilityLabel;
        private Label _castTimerLabel;
        private VisualElement _castTickIndicator;
        private VisualElement _castTickContainer;
        private ProgressBar _gcdBar;
        private Label _gcdText;

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

            // Errors
            EventBus.Subscribe<string>("OnCombatError", OnCombatError);

            // Cast Updates
            EventBus.Subscribe<Genesis.Data.CastUpdateData>("OnCastUpdate", OnCastUpdate);
        }

        void OnDisable() {
            // Stats
            EventBus.Unsubscribe<float, float>("OnHealthChanged", OnHealthChanged);
            EventBus.Unsubscribe<float, float>("OnManaChanged", OnManaChanged);
            
            // Targeting
            EventBus.Unsubscribe<NetworkObject>("OnTargetChanged", OnTargetChanged);
            EventBus.Unsubscribe("OnTargetCleared", OnTargetCleared);

            // Errors
            EventBus.Unsubscribe<string>("OnCombatError", OnCombatError);

            // Cast Updates
            EventBus.Unsubscribe<Genesis.Data.CastUpdateData>("OnCastUpdate", OnCastUpdate);
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
            if (uiDocument == null) return;
            
            _root = uiDocument.rootVisualElement;
            if (_root == null) {
                Debug.LogWarning($"[HUDController] [{gameObject.name}] UI Document has no root element. Check if VisualTreeAsset is assigned.");
                return;
            }

            // Verify PanelSettings
            if (uiDocument.panelSettings == null) {
                Debug.LogError($"[HUDController] [{gameObject.name}] UI Document has no PanelSettings assigned! This can cause NullReferenceException during reload.");
            }

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

            // Cast & GCD (Check if they exist before assignment)
            // They seem to have been moved or removed in the recent refactor
            // Cast & GCD
            _castGroup = _root.Q<VisualElement>("CastGroup");
            _castBarMask = _root.Q<VisualElement>("CastBar_Fill_Mask");
            _castBarFill = _root.Q<VisualElement>("CastBar_Fill");
            _castAbilityLabel = _root.Q<Label>("CastAbilityLabel");
            _castTimerLabel = _root.Q<Label>("CastTimerLabel");
            _castTickIndicator = _root.Q<VisualElement>("CastBar_TickRate");
            _castTickContainer = _root.Q<VisualElement>("CastBar_TickContainer");

            _gcdBar = _root.Q<ProgressBar>("GCDBar");
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

            Debug.Log($"[HUDController] [{gameObject.name}] UI Initialized successfully.");
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
            if (_castGroup == null) return;

            if (percent > 0) {
                _castGroup.style.display = DisplayStyle.Flex;
                
                if (_castBarMask != null) {
                    _castBarMask.style.width = new Length(percent, LengthUnit.Percent);
                }

                if (_castAbilityLabel != null) {
                    _castAbilityLabel.text = abilityName;
                }

                // El tiempo restante se calcula en base al percent y la duración total. 
                // Pero como HUDController solo recibe percent y name, necesitaremos 
                // o pasar el tiempo restante o calcularlo si tenemos la referencia.
                // Por ahora, mostraremos el porcentaje como fallback si no hay timer específico.
                // Sin embargo, el usuario pidió tiempo restante.
            } else {
                _castGroup.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Sobrecarga para manejar tiempo restante e indicadores de channeling.
        /// </summary>
        public void UpdateCastBar(Genesis.Data.CastUpdateData data) {
            if (_castGroup == null) return;

            if (data.Percent > 0) {
                _castGroup.style.display = DisplayStyle.Flex;
                
                if (_castBarMask != null) {
                    _castBarMask.style.width = new Length(data.Percent, LengthUnit.Percent);
                }

                if (_castAbilityLabel != null) {
                    _castAbilityLabel.text = data.AbilityName;
                }

                if (_castTimerLabel != null) {
                    _castTimerLabel.text = data.RemainingTime > 0 ? $"{data.RemainingTime:F1}s" : "";
                }

                if (_castBarFill != null) {
                    _castBarFill.style.unityBackgroundImageTintColor = GetCategoryColor(data.Category);
                }

                // --- GESTIÓN DE TICKS ---
                if (_castTickContainer != null) {
                    _castTickContainer.Clear();

                    if (data.IsChanneling && data.TickRate > 0 && data.Duration > 0) {
                        // Calcular cuántos ticks hay
                        int tickCount = Mathf.FloorToInt(data.Duration / data.TickRate);
                        
                        // Generar marcas intermitentes
                        for (int i = 1; i < tickCount; i++) {
                            float tickTime = i * data.TickRate;
                            float tickPercent = (tickTime / data.Duration) * 100f;

                            VisualElement tick = new VisualElement();
                            tick.AddToClassList("cast-bar-tick");
                            tick.style.display = DisplayStyle.Flex;
                            tick.style.position = Position.Absolute;
                            tick.style.left = new Length(tickPercent, LengthUnit.Percent);
                            
                            _castTickContainer.Add(tick);
                        }
                    }
                }
            } else {
                _castGroup.style.display = DisplayStyle.None;
            }
        }

        private Color GetCategoryColor(Genesis.Data.AbilityCategory category) {
            switch (category) {
                case Genesis.Data.AbilityCategory.Magical:
                    return new Color(0.4f, 0.6f, 1f, 1f); // Azul brillante / Celeste
                case Genesis.Data.AbilityCategory.Physical:
                    return new Color(1f, 0.3f, 0.3f, 1f); // Rojo / Naranja
                case Genesis.Data.AbilityCategory.Utility:
                    return new Color(0.5f, 1f, 0.5f, 1f); // Verde
                default:
                    return Color.white;
            }
        }

        private void OnCastUpdate(Genesis.Data.CastUpdateData data) {
            UpdateCastBar(data);
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

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Genesis.Core;
using Genesis.Data;
using System.Collections.Generic;

namespace Genesis.Presentation.UI {

    /// <summary>
    /// Debug UI controller for ability bar visualization.
    /// Shows ability cooldowns, GCD, combat states, and event log.
    /// Toggle with F3 key.
    /// </summary>
    public class AbilityBarDebugController : MonoBehaviour {

        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Player Combat Reference")]
        [SerializeField] private Genesis.Simulation.PlayerCombat playerCombat;

        [Header("HUD Reference (for Cast/GCD bars)")]
        private HUDController _hudController;

        // Root element
        private VisualElement _root;
        private VisualElement _overlay;

        // Ability Slots (6 slots)
        private VisualElement[] _abilitySlots = new VisualElement[6];
        private VisualElement[] _abilityIcons = new VisualElement[6];
        private VisualElement[] _cooldownOverlays = new VisualElement[6];
        private Label[] _cooldownTexts = new Label[6];
        private VisualElement[] _stateIndicators = new VisualElement[6];

        // Detail Panel
        private Label _detailName;
        private Label _detailMana;
        private Label _detailCooldown;
        private Label _detailCastTime;
        private Label _detailRange;
        private Label _detailIndicator;
        private Label _combatStateLabel;

        // Event Log
        private ScrollView _eventLog;
        private List<Label> _logEntries = new List<Label>();
        private const int MAX_LOG_ENTRIES = 20;

        // State
        private bool _isVisible = false;
        private AbilityData _lastCastAbility;

        // ═══════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════

        void OnEnable() {
            // Subscribe to EventBus events
            EventBus.Subscribe<int, string>("OnAbilityCast", OnAbilityCast);
            EventBus.Subscribe<int, float>("OnAbilityCooldownStart", OnAbilityCooldownStart);
            EventBus.Subscribe<string>("OnCombatStateChanged", OnCombatStateChanged);
            EventBus.Subscribe<int, string>("OnAbilityFailed", OnAbilityFailed);
            EventBus.Subscribe<float, string>("OnCastProgress", OnCastProgress);
        }

        void OnDisable() {
            // Unsubscribe from EventBus events
            EventBus.Unsubscribe<int, string>("OnAbilityCast", OnAbilityCast);
            EventBus.Unsubscribe<int, float>("OnAbilityCooldownStart", OnAbilityCooldownStart);
            EventBus.Unsubscribe<string>("OnCombatStateChanged", OnCombatStateChanged);
            EventBus.Unsubscribe<int, string>("OnAbilityFailed", OnAbilityFailed);
            EventBus.Unsubscribe<float, string>("OnCastProgress", OnCastProgress);
        }

        void Start() {
            if (uiDocument == null) {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null) {
                Debug.LogError("[AbilityBarDebugController] No UIDocument found!");
                return;
            }

            InitializeUI();
        }

        void Update() {
            // Toggle visibility with F3
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) {
                ToggleVisibility();
            }

            // Update cooldowns only if visible
            if (_isVisible && playerCombat != null) {
                UpdateCooldowns();
            }

            // Always update GCD bar in HUD (even when debug UI is hidden)
            if (playerCombat != null && _hudController != null) {
                UpdateGCDInHUD();
            }
        }

        // ═══════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════

        private void InitializeUI() {
            _root = uiDocument.rootVisualElement;
            _overlay = _root.Q<VisualElement>("AbilityDebugOverlay");

            // Query all ability slots
            for (int i = 0; i < 6; i++) {
                int slotNum = i + 1;
                _abilitySlots[i] = _root.Q<VisualElement>($"AbilitySlot{slotNum}");
                _abilityIcons[i] = _root.Q<VisualElement>($"AbilityIcon{slotNum}");
                _cooldownOverlays[i] = _root.Q<VisualElement>($"CooldownOverlay{slotNum}");
                _cooldownTexts[i] = _root.Q<Label>($"CooldownText{slotNum}");
                _stateIndicators[i] = _root.Q<VisualElement>($"StateIndicator{slotNum}");
            }

            // Detail Panel
            _detailName = _root.Q<Label>("DetailName");
            _detailMana = _root.Q<Label>("DetailMana");
            _detailCooldown = _root.Q<Label>("DetailCooldown");
            _detailCastTime = _root.Q<Label>("DetailCastTime");
            _detailRange = _root.Q<Label>("DetailRange");
            _detailIndicator = _root.Q<Label>("DetailIndicator");
            _combatStateLabel = _root.Q<Label>("CombatState");

            // Event Log
            _eventLog = _root.Q<ScrollView>("EventLog");

            // Find HUDController in the scene (for Cast/GCD bars)
            _hudController = FindObjectOfType<HUDController>();
            if (_hudController == null) {
                Debug.LogWarning("[AbilityBarDebugController] HUDController not found! Cast/GCD bars won't update.");
            }

            // Start hidden
            if (_overlay != null) {
                _overlay.style.display = DisplayStyle.None;
            }

            Debug.Log("[AbilityBarDebugController] UI Initialized. Press F3 to toggle.");
        }

        // ═══════════════════════════════════════════════════════
        // PUBLIC SETTERS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Set the player combat reference (called from scene setup)
        /// </summary>
        public void SetPlayerCombat(Genesis.Simulation.PlayerCombat combat) {
            playerCombat = combat;
            if (playerCombat != null) {
                UpdateAllSlots();
            }
        }

        // ═══════════════════════════════════════════════════════
        // VISIBILITY
        // ═══════════════════════════════════════════════════════

        private void ToggleVisibility() {
            _isVisible = !_isVisible;

            if (_overlay != null) {
                _overlay.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_isVisible && playerCombat != null) {
                UpdateAllSlots();
            }

            Debug.Log($"[AbilityBarDebugController] Visibility: {_isVisible}");
        }

        // ═══════════════════════════════════════════════════════
        // EVENT HANDLERS
        // ═══════════════════════════════════════════════════════

        private void OnAbilityCast(int abilityId, string abilityName) {
            // Update last cast ability
            if (AbilityDatabase.Instance != null) {
                _lastCastAbility = AbilityDatabase.Instance.GetAbility(abilityId);
                if (_lastCastAbility != null) {
                    UpdateDetailPanel(_lastCastAbility);
                }
            }

            // Add to event log
            AddLogEntry($"<color=#64FF64>CAST:</color> {abilityName}", "log-entry-success");
        }

        private void OnAbilityCooldownStart(int abilityId, float duration) {
            // Log cooldown start
            AddLogEntry($"<color=#FFC864>CD:</color> Ability {abilityId} - {duration:F1}s", "log-entry-cooldown");
        }

        private void OnCombatStateChanged(string state) {
            // Update combat state label
            if (_combatStateLabel != null) {
                _combatStateLabel.text = $"State: {state}";

                // Remove all state classes
                _combatStateLabel.RemoveFromClassList("state-idle");
                _combatStateLabel.RemoveFromClassList("state-aiming");
                _combatStateLabel.RemoveFromClassList("state-casting");
                _combatStateLabel.RemoveFromClassList("state-channeling");

                // Add appropriate class
                switch (state.ToLower()) {
                    case "idle":
                        _combatStateLabel.AddToClassList("state-idle");
                        break;
                    case "aiming":
                        _combatStateLabel.AddToClassList("state-aiming");
                        break;
                    case "casting":
                        _combatStateLabel.AddToClassList("state-casting");
                        break;
                    case "channeling":
                        _combatStateLabel.AddToClassList("state-channeling");
                        break;
                }
            }

            // Add to event log
            AddLogEntry($"<color=#96C8FF>STATE:</color> {state}", "log-entry-state");
        }

        private void OnAbilityFailed(int abilityId, string reason) {
            // Add to event log
            string abilityName = "Unknown";
            if (AbilityDatabase.Instance != null) {
                AbilityData ability = AbilityDatabase.Instance.GetAbility(abilityId);
                if (ability != null) {
                    abilityName = ability.Name;
                }
            }

            AddLogEntry($"<color=#FF6464>FAILED:</color> {abilityName} - {reason}", "log-entry-failed");
        }

        private void OnCastProgress(float percent, string abilityName) {
            // Update cast bar in HUD
            if (_hudController != null) {
                _hudController.SetCastProgress(percent, abilityName);
            }
        }

        // ═══════════════════════════════════════════════════════
        // UPDATE LOGIC
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Update all ability slots with current loadout
        /// </summary>
        private void UpdateAllSlots() {
            if (playerCombat == null || playerCombat.abilitySlots == null) return;

            for (int i = 0; i < 6; i++) {
                if (i < playerCombat.abilitySlots.Count) {
                    AbilityData ability = playerCombat.abilitySlots[i];
                    if (ability != null && ability.Icon != null) {
                        // Set icon as background image
                        _abilityIcons[i].style.backgroundImage = new StyleBackground(ability.Icon);
                    } else {
                        // Clear icon
                        _abilityIcons[i].style.backgroundImage = StyleKeyword.Null;
                    }
                } else {
                    // Empty slot
                    _abilityIcons[i].style.backgroundImage = StyleKeyword.Null;
                }
            }
        }

        /// <summary>
        /// Update cooldown overlays and text for all abilities
        /// </summary>
        private void UpdateCooldowns() {
            if (playerCombat == null || playerCombat.abilitySlots == null) return;

            for (int i = 0; i < 6; i++) {
                if (i < playerCombat.abilitySlots.Count) {
                    AbilityData ability = playerCombat.abilitySlots[i];
                    if (ability != null) {
                        float cdRemaining = playerCombat.GetCooldownRemaining(ability.ID);

                        if (cdRemaining > 0) {
                            // Show cooldown overlay and text
                            _cooldownOverlays[i].style.display = DisplayStyle.Flex;
                            _cooldownTexts[i].style.display = DisplayStyle.Flex;
                            _cooldownTexts[i].text = cdRemaining.ToString("F1");

                            // Update state indicator to cooldown color
                            _stateIndicators[i].RemoveFromClassList("state-idle");
                            _stateIndicators[i].AddToClassList("state-cooldown");
                        } else {
                            // Hide cooldown overlay and text
                            _cooldownOverlays[i].style.display = DisplayStyle.None;
                            _cooldownTexts[i].style.display = DisplayStyle.None;

                            // Update state indicator to idle color
                            _stateIndicators[i].RemoveFromClassList("state-cooldown");
                            _stateIndicators[i].AddToClassList("state-idle");
                        }
                    }
                } else {
                    // Empty slot - hide overlays
                    _cooldownOverlays[i].style.display = DisplayStyle.None;
                    _cooldownTexts[i].style.display = DisplayStyle.None;
                }
            }
        }

        /// <summary>
        /// Update GCD progress bar in HUD
        /// </summary>
        private void UpdateGCDInHUD() {
            if (playerCombat == null || _hudController == null) return;

            float gcdRemaining = playerCombat.GetGCDRemaining();

            if (gcdRemaining > 0) {
                // Assuming max GCD is 1.5s (typical for MMOs)
                float gcdMax = 1.5f;
                float gcdPercent = (gcdRemaining / gcdMax) * 100f;
                _hudController.SetGCDProgress(gcdPercent);
            } else {
                _hudController.SetGCDProgress(0f);
            }
        }

        /// <summary>
        /// Update detail panel with ability stats
        /// </summary>
        private void UpdateDetailPanel(AbilityData ability) {
            if (ability == null) return;

            if (_detailName != null) _detailName.text = $"Name: {ability.Name}";
            if (_detailMana != null) _detailMana.text = $"Mana: {ability.ManaCost}";
            if (_detailCooldown != null) _detailCooldown.text = $"Cooldown: {ability.Cooldown}s";
            if (_detailCastTime != null) _detailCastTime.text = $"Cast Time: {ability.CastTime}s";
            if (_detailRange != null) _detailRange.text = $"Range: {ability.Range}m";
            if (_detailIndicator != null) _detailIndicator.text = $"Indicator: {ability.IndicatorType}";
        }

        /// <summary>
        /// Add entry to event log (with auto-scroll and max entries)
        /// </summary>
        private void AddLogEntry(string message, string cssClass) {
            if (_eventLog == null) return;

            // Create new log entry label
            Label entry = new Label(message);
            entry.AddToClassList("log-entry");
            if (!string.IsNullOrEmpty(cssClass)) {
                entry.AddToClassList(cssClass);
            }

            // Add timestamp
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            entry.text = $"[{timestamp}] {message}";

            // Add to log
            _eventLog.Add(entry);
            _logEntries.Add(entry);

            // Maintain max entries limit
            if (_logEntries.Count > MAX_LOG_ENTRIES) {
                Label oldEntry = _logEntries[0];
                _eventLog.Remove(oldEntry);
                _logEntries.RemoveAt(0);
            }

            // Auto-scroll to bottom
            _eventLog.scrollOffset = new Vector2(0, _eventLog.contentContainer.layout.height);
        }
    }
}

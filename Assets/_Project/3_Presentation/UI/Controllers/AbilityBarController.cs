using UnityEngine;
using UnityEngine.UIElements;
using Genesis.Core;
using Genesis.Data;
using System.Collections.Generic;

namespace Genesis.Presentation.UI {

    /// <summary>
    /// Controller for the permanent ability bar HUD.
    /// Displays ability icons, cooldowns, and state indicators.
    /// Always visible and syncs with PlayerCombat via EventBus.
    /// </summary>
    public class AbilityBarController : MonoBehaviour {

        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Player Combat Reference")]
        [SerializeField] private Genesis.Simulation.PlayerCombat playerCombat;

        // Root element
        private VisualElement _root;

        // Ability Slots (6 slots)
        private VisualElement[] _abilitySlots = new VisualElement[6];
        private VisualElement[] _abilityIcons = new VisualElement[6];
        private VisualElement[] _cooldownOverlays = new VisualElement[6];
        private Label[] _cooldownTexts = new Label[6];
        private VisualElement[] _stateIndicators = new VisualElement[6];

        // ═══════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════

        void OnEnable() {
            // Subscribe to EventBus events
            EventBus.Subscribe<int, string>("OnAbilityCast", OnAbilityCast);
            EventBus.Subscribe<int, float>("OnAbilityCooldownStart", OnAbilityCooldownStart);
            EventBus.Subscribe("OnLoadoutChanged", UpdateAllSlots);
        }

        void OnDisable() {
            // Unsubscribe from EventBus events
            EventBus.Unsubscribe<int, string>("OnAbilityCast", OnAbilityCast);
            EventBus.Unsubscribe<int, float>("OnAbilityCooldownStart", OnAbilityCooldownStart);
            EventBus.Unsubscribe("OnLoadoutChanged", UpdateAllSlots);
        }

        void Start() {
            if (uiDocument == null) {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null) {
                Debug.LogError("[AbilityBarController] No UIDocument found!");
                return;
            }

            InitializeUI();
        }

        void Update() {
            // Update cooldowns every frame
            if (playerCombat != null) {
                UpdateCooldowns();
            }
        }

        // ═══════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════

        private void InitializeUI() {
            _root = uiDocument.rootVisualElement;

            // Query all ability slots
            for (int i = 0; i < 6; i++) {
                int slotNum = i + 1;
                _abilitySlots[i] = _root.Q<VisualElement>($"AbilitySlot{slotNum}");
                _abilityIcons[i] = _root.Q<VisualElement>($"AbilityIcon{slotNum}");
                _cooldownOverlays[i] = _root.Q<VisualElement>($"CooldownOverlay{slotNum}");
                _cooldownTexts[i] = _root.Q<Label>($"CooldownText{slotNum}");
                _stateIndicators[i] = _root.Q<VisualElement>($"StateIndicator{slotNum}");

                // Initialize state indicators as idle
                if (_stateIndicators[i] != null) {
                    _stateIndicators[i].AddToClassList("state-idle");
                }
            }

            Debug.Log($"[AbilityBarController] [{gameObject.name}] UI Initialized.");

            // Initial update if playerCombat is already set
            if (playerCombat != null) {
                UpdateAllSlots();
            }
        }

        // ═══════════════════════════════════════════════════════
        // PUBLIC SETTERS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Set the player combat reference (called from scene setup or PlayerUIConnector)
        /// </summary>
        public void SetPlayerCombat(Genesis.Simulation.PlayerCombat combat) {
            playerCombat = combat;
            if (playerCombat != null) {
                UpdateAllSlots();
            }
        }

        // ═══════════════════════════════════════════════════════
        // EVENT HANDLERS
        // ═══════════════════════════════════════════════════════

        private void OnAbilityCast(int abilityId, string abilityName) {
            // Visual feedback when ability is cast (optional)
            // Could add a flash effect or animation here
        }

        private void OnAbilityCooldownStart(int abilityId, float duration) {
            // Cooldown is handled in UpdateCooldowns()
            // This event could be used for additional effects
        }

        // ═══════════════════════════════════════════════════════
        // UPDATE LOGIC
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Update all ability slots with current loadout
        /// </summary>
        private void UpdateAllSlots() {
            if (playerCombat == null) {
                Debug.LogWarning("[AbilityBarController] PlayerCombat is null! Icons won't show. Assign PlayerCombat in Inspector or via SetPlayerCombat().");
                return;
            }
            
            if (playerCombat.abilitySlots == null) {
                Debug.LogWarning("[AbilityBarController] PlayerCombat.abilitySlots is null!");
                return;
            }

            Debug.Log($"[AbilityBarController] [{gameObject.name}] Updating {playerCombat.abilitySlots.Count} ability slots");

            for (int i = 0; i < 6; i++) {
                if (i < playerCombat.abilitySlots.Count) {
                    AbilityData ability = playerCombat.abilitySlots[i];
                    if (ability != null && ability.Icon != null) {
                        // Set icon as background image
                        _abilityIcons[i].style.backgroundImage = new StyleBackground(ability.Icon);
                        Debug.Log($"[AbilityBarController] [{gameObject.name}] Slot {i}: Set icon for {ability.Name}");
                    } else {
                        // Clear icon
                        _abilityIcons[i].style.backgroundImage = StyleKeyword.Null;
                        Debug.Log($"[AbilityBarController] [{gameObject.name}] Slot {i}: Ability or Icon is null");
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
    }
}

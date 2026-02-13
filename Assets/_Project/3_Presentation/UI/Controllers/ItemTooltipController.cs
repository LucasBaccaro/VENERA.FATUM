using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using Genesis.Items;

namespace Genesis.Presentation.UI {
    public class ItemTooltipController : MonoBehaviour {
        // Singleton
        public static ItemTooltipController Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private Vector2 _offset = new Vector2(20, 20); // Standard Bottom-Right offset
        [SerializeField] private float _screenPadding = 10f;
        [SerializeField] private float _hideDelay = 0.05f; // 50ms delay to prevent flickering

        private VisualElement _tooltipRoot;
        private Label _titleLabel;
        private Label _typeLabel;
        private Label _armorLabel;
        private VisualElement _primaryStatsList;
        private VisualElement _substatsList;
        private VisualElement _substatsSection;
        private Label _descriptionLabel;

        private bool _isVisible;
        private Coroutine _hideCoroutine;
        
        // Stat Categories
        private readonly HashSet<StatType> _primaryStats = new HashSet<StatType> {
            StatType.Strength, StatType.Agility, StatType.Intelligence, 
            StatType.Wisdom, StatType.Constitution, 
            StatType.MaxHealth, StatType.MaxMana
        };

        private void Awake() {
            // Singleton setup
            if (Instance == null) {
                Instance = this;
            } else {
                Debug.LogWarning("[ItemTooltipController] Multiple instances detected! Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Initialize();
            HideImmediate();
        }

        private void Initialize() {
            if (_tooltipRoot != null) return;

            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) {
                Debug.LogError("[ItemTooltipController] UIDocument not found!");
                return;
            }

            if (uiDocument.rootVisualElement == null) {
                Debug.LogError("[ItemTooltipController] Root VisualElement is null! Check if UXML is assigned.");
                return;
            }

            _tooltipRoot = uiDocument.rootVisualElement.Q<VisualElement>("TooltipMain");
            
            if (_tooltipRoot == null) {
                Debug.LogError("[ItemTooltipController] Root 'TooltipMain' not found in UXML!");
                return;
            }

            _titleLabel = _tooltipRoot.Q<Label>("ItemName");
            _typeLabel = _tooltipRoot.Q<Label>("ItemType");
            _armorLabel = _tooltipRoot.Q<Label>("ArmorValue");
            _primaryStatsList = _tooltipRoot.Q<VisualElement>("PrimaryStats");
            _substatsList = _tooltipRoot.Q<VisualElement>("SubstatsList");
            _substatsSection = _tooltipRoot.Q<VisualElement>("SubstatsSection");
            _descriptionLabel = _tooltipRoot.Q<Label>("Description");
            
            // Ensure we don't block raycasts
            _tooltipRoot.pickingMode = PickingMode.Ignore;

            Debug.Log("[ItemTooltipController] Initialized successfully.");
        }

        private void Update() {
            if (!_isVisible) return;
            UpdatePosition();
        }

        private void UpdatePosition() {
            if (Mouse.current == null || _tooltipRoot.panel == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            
            // Get panel dimensions
            float panelWidth = _tooltipRoot.panel.visualTree.layout.width;
            float panelHeight = _tooltipRoot.panel.visualTree.layout.height;
            
            // Convert mouse position from screen space to panel space
            // Mouse.position is in screen coordinates (bottom-left origin)
            // UI Toolkit panels use top-left origin, so we need to flip Y
            float mouseX = mousePos.x;
            float mouseY = panelHeight - mousePos.y;  // Flip Y axis
            
            // Get tooltip dimensions
            float width = _tooltipRoot.resolvedStyle.width;
            float height = _tooltipRoot.resolvedStyle.height;

            // If width/height are not resolved yet (NaN), use default offset
            if (float.IsNaN(width) || float.IsNaN(height)) {
                _tooltipRoot.style.left = mouseX + _offset.x;
                _tooltipRoot.style.top = mouseY + _offset.y;
                return;
            }

            // Smart positioning: flip to left if not enough space on right
            float x, y;
            
            // Check if tooltip fits on the right side of cursor
            if (mouseX + _offset.x + width <= panelWidth - _screenPadding) {
                // Enough space on right, use normal offset
                x = mouseX + _offset.x;
            } else {
                // Not enough space on right, flip to left
                x = mouseX - width - _offset.x;
                
                // If still doesn't fit, clamp to left edge
                if (x < _screenPadding) {
                    x = _screenPadding;
                }
            }
            
            // Check if tooltip fits below cursor
            if (mouseY + _offset.y + height <= panelHeight - _screenPadding) {
                // Enough space below, use normal offset
                y = mouseY + _offset.y;
            } else {
                // Not enough space below, flip to above
                y = mouseY - height - _offset.y;
                
                // If still doesn't fit, clamp to top edge
                if (y < _screenPadding) {
                    y = _screenPadding;
                }
            }

            _tooltipRoot.style.left = x;
            _tooltipRoot.style.top = y;
        }

        public void Show(BaseItemData item, ItemRarity rarity) {
            // Cancel any pending hide to prevent flickering
            if (_hideCoroutine != null) {
                StopCoroutine(_hideCoroutine);
                _hideCoroutine = null;
            }

            if (_tooltipRoot == null) Initialize();
            if (_tooltipRoot == null || item == null) return;

            // Basic Info
            if (_titleLabel != null) {
                _titleLabel.text = item.ItemName;
                _titleLabel.style.color = GetRarityColor(rarity);
            }
            
            if (_typeLabel != null) {
                _typeLabel.text = item.Type.ToString();
                if (item is EquipmentItemData equipment) {
                    _typeLabel.text = $"{rarity} {equipment.Slot}";
                }
            }

            if (_descriptionLabel != null) _descriptionLabel.text = item.Description;

            // Stats Logic
            if (_armorLabel != null) _armorLabel.style.display = DisplayStyle.None;
            if (_primaryStatsList != null) _primaryStatsList.Clear();
            if (_substatsList != null) _substatsList.Clear();
            if (_substatsSection != null) _substatsSection.style.display = DisplayStyle.None;

            if (item is EquipmentItemData eqData) {
                PopulateStats(eqData, rarity);
            }

            // Show Tooltip
            _tooltipRoot.style.opacity = 1;
            _isVisible = true;
            UpdatePosition(); // Initial position update
        }

        private void PopulateStats(EquipmentItemData data, ItemRarity rarity) {
            List<StatModifier> stats = data.GetStatsForRarity(rarity);
            if (stats == null || stats.Count == 0) return;

            bool hasSubstats = false;

            foreach (var stat in stats) {
                // 1. Armor
                if (stat.Type == StatType.Armor) {
                    _armorLabel.text = $"{stat.Value} Armor";
                    _armorLabel.style.display = DisplayStyle.Flex;
                    continue;
                }

                // 2. Primary Stats
                if (_primaryStats.Contains(stat.Type)) {
                    var label = new Label(stat.ToString());
                    label.AddToClassList("tooltip-primary-stats");
                    _primaryStatsList.Add(label);
                    continue;
                }

                // 3. Substats
                var subLabel = new Label(stat.ToString());
                subLabel.AddToClassList("tooltip-substats-list");
                _substatsList.Add(subLabel);
                hasSubstats = true;
            }

            if (hasSubstats) {
                _substatsSection.style.display = DisplayStyle.Flex;
            }
        }

        /// <summary>
        /// Hide tooltip with a small delay to prevent flickering when moving between items
        /// </summary>
        public void Hide() {
            if (_hideCoroutine == null) {
                _hideCoroutine = StartCoroutine(HideDelayed());
            }
        }

        /// <summary>
        /// Hide tooltip immediately without delay (used for initialization)
        /// </summary>
        public void HideImmediate() {
            if (_hideCoroutine != null) {
                StopCoroutine(_hideCoroutine);
                _hideCoroutine = null;
            }

            if (_tooltipRoot != null) {
                _tooltipRoot.style.opacity = 0;
            }
            _isVisible = false;
        }

        private IEnumerator HideDelayed() {
            yield return new WaitForSeconds(_hideDelay);
            
            if (_tooltipRoot != null) {
                _tooltipRoot.style.opacity = 0;
            }
            _isVisible = false;
            _hideCoroutine = null;
        }

        private StyleColor GetRarityColor(ItemRarity rarity) {
            switch (rarity) {
                case ItemRarity.Uncommon: return new StyleColor(new Color(0.12f, 1f, 0f)); // Green
                case ItemRarity.Rare: return new StyleColor(new Color(0f, 0.44f, 0.87f)); // Blue
                case ItemRarity.Epic: return new StyleColor(new Color(0.64f, 0.21f, 0.93f)); // Purple
                case ItemRarity.Common: 
                default: return new StyleColor(Color.white);
            }
        }
    }
}

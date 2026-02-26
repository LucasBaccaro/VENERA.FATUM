using UnityEngine;
using UnityEngine.UIElements;
using Genesis.Simulation;
using Genesis.Core;
using System.Collections.Generic;

namespace Genesis.Presentation.UI {

    /// <summary>
    /// Manages 2D Screen-Space nameplates (name at feet) and health bars (above head) for all players.
    /// Projects 3D world positions to 2D UI Toolkit panel coordinates.
    /// </summary>
    public class NameplateManager : MonoBehaviour {

        [Header("References")]
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private VisualTreeAsset _nameplateTemplate;

        [Header("Health Bar")]
        [SerializeField] private float _headHeight = 2.1f;

        private VisualElement _container;
        private Camera _mainCamera;

        private class NameplateData {
            public PlayerClassManager player;
            public PlayerStats stats;
            // Name (positioned at feet)
            public VisualElement nameElement;
            public Label label;
            // Health bar (positioned above head)
            public VisualElement healthBarRoot;
            public VisualElement healthBarFill;
            public VisualElement manaBarFill;
            public VisualElement classIcon;
            public VisualElement hpTickContainer;
            public float lastMaxHealth;
        }

        private List<NameplateData> _activeNameplates = new List<NameplateData>();
        private List<PlayerClassManager> _players = new List<PlayerClassManager>();

        private void OnEnable() {
            _mainCamera = Camera.main;
            InitializeUI();

            // Initial sync: catch any players already in the scene
            var existingPlayers = FindObjectsByType<PlayerClassManager>(FindObjectsSortMode.None);
            foreach (var p in existingPlayers) {
                OnPlayerRegister(p);
            }

            EventBus.Subscribe<PlayerClassManager>("OnPlayerNameplateRegister", OnPlayerRegister);
            EventBus.Subscribe<PlayerClassManager>("OnPlayerNameplateUnregister", OnPlayerUnregister);
            EventBus.Subscribe<string>("OnPlayerNameChanged", OnGlobalNameChanged);
            EventBus.Subscribe<PlayerClassManager>("OnPlayerFactionChanged", OnFactionChanged);
        }

        private void OnDisable() {
            EventBus.Unsubscribe<PlayerClassManager>("OnPlayerNameplateRegister", OnPlayerRegister);
            EventBus.Unsubscribe<PlayerClassManager>("OnPlayerNameplateUnregister", OnPlayerUnregister);
            EventBus.Unsubscribe<string>("OnPlayerNameChanged", OnGlobalNameChanged);
            EventBus.Unsubscribe<PlayerClassManager>("OnPlayerFactionChanged", OnFactionChanged);
        }

        private void OnGlobalNameChanged(string newName) {
            foreach (var data in _activeNameplates) {
                if (data.player != null && data.label != null) {
                    data.label.text = data.player.PlayerName;
                }
            }
        }

        private void OnFactionChanged(PlayerClassManager player) {
            foreach (var data in _activeNameplates) {
                if (data.player == player && data.label != null) {
                    ApplyFactionColor(data.label, player.Faction);
                    break;
                }
            }
        }

        private void ApplyFactionColor(Label label, int faction) {
            label.EnableInClassList("nameplate-label--citizen", faction == 0);
            label.EnableInClassList("nameplate-label--pk", faction == 1);
        }

        private void OnPlayerRegister(PlayerClassManager player) {
            if (!_players.Contains(player)) _players.Add(player);
        }

        private void OnPlayerUnregister(PlayerClassManager player) {
            _players.Remove(player);
        }

        private void InitializeUI() {
            if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) return;

            _container = _uiDocument.rootVisualElement.Q<VisualElement>("NameplateContainer");
            if (_container == null) {
                Debug.LogWarning("[NameplateManager] NameplateContainer not found in UIDocument.");
            }
        }

        private void LateUpdate() {
            if (_container == null) return;
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            UpdateNameplateList();

            for (int i = _activeNameplates.Count - 1; i >= 0; i--) {
                var data = _activeNameplates[i];

                if (data.player == null) {
                    _container.Remove(data.nameElement);
                    _container.Remove(data.healthBarRoot);
                    _activeNameplates.RemoveAt(i);
                    continue;
                }

                UpdateNameplate(data);
            }
        }

        private void UpdateNameplateList() {
            foreach (var player in _players) {
                if (player == null) continue;

                bool found = false;
                foreach (var data in _activeNameplates) {
                    if (data.player == player) {
                        found = true;
                        break;
                    }
                }

                if (!found) {
                    CreateNameplateFor(player);
                }
            }
        }

        private void CreateNameplateFor(PlayerClassManager player) {
            if (_nameplateTemplate == null) return;

            // --- Name element (from template, positioned at feet) ---
            VisualElement element = _nameplateTemplate.Instantiate();
            VisualElement nameplateRoot = element.Q<VisualElement>("Nameplate");
            Label label = element.Q<Label>("NameLabel");

            if (label != null) {
                label.text = player.PlayerName;
                ApplyFactionColor(label, player.Faction);
            }

            _container.Add(nameplateRoot);

            // --- Custom Health/Mana bar with Class Icon ---
            var healthBarRoot = new VisualElement();
            healthBarRoot.AddToClassList("nameplate-healthbar-root");
            healthBarRoot.pickingMode = PickingMode.Ignore;

            // 1. Bars Container
            var barsContainer = new VisualElement();
            barsContainer.AddToClassList("nameplate-bars-container");
            barsContainer.pickingMode = PickingMode.Ignore;
            healthBarRoot.Add(barsContainer);

            // 2. Class Icon (Added last to render on top of bars)
            var classIcon = new VisualElement();
            classIcon.AddToClassList("nameplate-class-icon");
            classIcon.pickingMode = PickingMode.Ignore;
            healthBarRoot.Add(classIcon);

            // Health Bar
            var healthBarBg = new VisualElement();
            healthBarBg.AddToClassList("nameplate-healthbar-bg");
            healthBarBg.pickingMode = PickingMode.Ignore;

            var healthBarFill = new VisualElement();
            healthBarFill.AddToClassList("nameplate-healthbar-fill");
            healthBarFill.pickingMode = PickingMode.Ignore;

            var hpTickContainer = new VisualElement();
            hpTickContainer.style.position = Position.Absolute;
            hpTickContainer.style.width = Length.Percent(100);
            hpTickContainer.style.height = Length.Percent(100);
            hpTickContainer.pickingMode = PickingMode.Ignore;

            healthBarBg.Add(healthBarFill);
            healthBarBg.Add(hpTickContainer);
            barsContainer.Add(healthBarBg);

            // Mana Bar
            var manaBarBg = new VisualElement();
            manaBarBg.AddToClassList("nameplate-manabar-bg");
            manaBarBg.pickingMode = PickingMode.Ignore;

            var manaBarFill = new VisualElement();
            manaBarFill.AddToClassList("nameplate-manabar-fill");
            manaBarFill.pickingMode = PickingMode.Ignore;

            manaBarBg.Add(manaBarFill);
            barsContainer.Add(manaBarBg);

            _container.Add(healthBarRoot);

            _activeNameplates.Add(new NameplateData {
                player = player,
                stats = player.GetComponent<PlayerStats>(),
                nameElement = nameplateRoot,
                label = label,
                healthBarRoot = healthBarRoot,
                healthBarFill = healthBarFill,
                manaBarFill = manaBarFill,
                classIcon = classIcon,
                hpTickContainer = hpTickContainer,
                lastMaxHealth = -1
            });
        }

        private void UpdateNameplate(NameplateData data) {
            Vector3 feetPos = data.player.transform.position;
            Vector3 headPos = feetPos + Vector3.up * _headHeight;

            // Visibility check
            Vector3 viewportPos = _mainCamera.WorldToViewportPoint(headPos);
            bool onScreen = viewportPos.z > 0 && viewportPos.x >= 0 && viewportPos.x <= 1 && viewportPos.y >= 0 && viewportPos.y <= 1;

            string displayName = data.player.PlayerName;
            if (string.IsNullOrEmpty(displayName) || !onScreen) {
                data.nameElement.style.display = DisplayStyle.None;
                data.healthBarRoot.style.display = DisplayStyle.None;
                return;
            }

            data.nameElement.style.display = DisplayStyle.Flex;
            data.healthBarRoot.style.display = DisplayStyle.Flex;

            // Position name at feet
            Vector2 feetPanel = RuntimePanelUtils.CameraTransformWorldToPanel(_container.panel, feetPos, _mainCamera);
            data.nameElement.style.left = feetPanel.x;
            data.nameElement.style.top = feetPanel.y;

            // Position health bar above head - shifted more up as requested
            Vector2 headPanel = RuntimePanelUtils.CameraTransformWorldToPanel(_container.panel, headPos, _mainCamera);
            data.healthBarRoot.style.left = headPanel.x;
            data.healthBarRoot.style.top = headPanel.y - 20f; 

            // Update bar fills and icon
            UpdateOverheadBars(data);
        }

        private void UpdateOverheadBars(NameplateData data) {
            if (data.stats == null) {
                data.stats = data.player.GetComponent<PlayerStats>();
                if (data.stats == null) return;
            }

            // Health Fill
            float maxHp = data.stats.MaxHealth;
            float hpRatio = maxHp > 0f ? Mathf.Clamp01(data.stats.CurrentHealth / maxHp) : 1f;
            data.healthBarFill.style.width = new Length(hpRatio * 100f, LengthUnit.Percent);

            // Mana Fill
            float maxMp = data.stats.MaxMana;
            float mpRatio = maxMp > 0f ? Mathf.Clamp01(data.stats.CurrentMana / maxMp) : 1f;
            if (data.manaBarFill != null) {
                data.manaBarFill.style.width = new Length(mpRatio * 100f, LengthUnit.Percent);
            }

            // Class Icon
            if (data.classIcon != null) {
                var classData = data.player.CurrentClassData;
                if (classData != null && classData.ClassIcon != null) {
                    data.classIcon.style.backgroundImage = new StyleBackground(classData.ClassIcon);
                    data.classIcon.style.display = DisplayStyle.Flex;
                } else {
                    data.classIcon.style.display = DisplayStyle.None;
                }
            }

            // HP Ticks (every 10 HP)
            if (Mathf.Abs(data.lastMaxHealth - maxHp) > 0.1f) {
                data.lastMaxHealth = maxHp;
                UpdateHPTicks(data, maxHp);
            }
        }

        private void UpdateHPTicks(NameplateData data, float maxHp) {
            if (data.hpTickContainer == null) return;
            data.hpTickContainer.Clear();

            int tickCount = Mathf.FloorToInt(maxHp / 10f);
            if (tickCount <= 0 || tickCount > 100) return; // Limit ticks sanity check

            for (int i = 1; i < tickCount; i++) {
                var tick = new VisualElement();
                tick.AddToClassList("nameplate-hp-tick");
                float posPercent = (i * 10f / maxHp) * 100f;
                tick.style.left = new Length(posPercent, LengthUnit.Percent);
                data.hpTickContainer.Add(tick);
            }
        }
    }
}

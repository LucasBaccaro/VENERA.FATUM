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

            // --- Health bar (created programmatically, positioned above head) ---
            var healthBarRoot = new VisualElement();
            healthBarRoot.AddToClassList("nameplate-healthbar-root");
            healthBarRoot.pickingMode = PickingMode.Ignore;

            var healthBarBg = new VisualElement();
            healthBarBg.AddToClassList("nameplate-healthbar-bg");
            healthBarBg.pickingMode = PickingMode.Ignore;

            var healthBarFill = new VisualElement();
            healthBarFill.AddToClassList("nameplate-healthbar-fill");
            healthBarFill.pickingMode = PickingMode.Ignore;

            healthBarBg.Add(healthBarFill);
            healthBarRoot.Add(healthBarBg);
            _container.Add(healthBarRoot);

            _activeNameplates.Add(new NameplateData {
                player = player,
                stats = player.GetComponent<PlayerStats>(),
                nameElement = nameplateRoot,
                label = label,
                healthBarRoot = healthBarRoot,
                healthBarFill = healthBarFill
            });
        }

        private void UpdateNameplate(NameplateData data) {
            Vector3 feetPos = data.player.transform.position;
            Vector3 headPos = feetPos + Vector3.up * _headHeight;

            // Visibility check (use head pos since it's higher)
            Vector3 viewportPos = _mainCamera.WorldToViewportPoint(headPos);
            bool onScreen = viewportPos.z > 0 && viewportPos.x >= 0 && viewportPos.x <= 1 && viewportPos.y >= 0 && viewportPos.y <= 1;

            string displayName = data.player.PlayerName;
            if (string.IsNullOrEmpty(displayName) || !onScreen) {
                data.nameElement.style.display = DisplayStyle.None;
                data.healthBarRoot.style.display = DisplayStyle.None;
                return;
            }

            // Update name text
            if (data.label != null && data.label.text != displayName) {
                data.label.text = displayName;
            }

            data.nameElement.style.display = DisplayStyle.Flex;
            data.healthBarRoot.style.display = DisplayStyle.Flex;

            // Position name at feet
            Vector2 feetPanel = RuntimePanelUtils.CameraTransformWorldToPanel(_container.panel, feetPos, _mainCamera);
            data.nameElement.style.left = feetPanel.x;
            data.nameElement.style.top = feetPanel.y;

            // Position health bar above head
            Vector2 headPanel = RuntimePanelUtils.CameraTransformWorldToPanel(_container.panel, headPos, _mainCamera);
            data.healthBarRoot.style.left = headPanel.x;
            data.healthBarRoot.style.top = headPanel.y;

            // Update health bar fill
            UpdateHealthBar(data);
        }

        private void UpdateHealthBar(NameplateData data) {
            if (data.healthBarFill == null) return;

            if (data.stats == null) {
                data.stats = data.player.GetComponent<PlayerStats>();
                if (data.stats == null) return;
            }

            float max = data.stats.MaxHealth;
            float ratio = max > 0f ? Mathf.Clamp01(data.stats.CurrentHealth / max) : 1f;
            data.healthBarFill.style.width = new Length(ratio * 100f, LengthUnit.Percent);

            // Color: green > yellow > red
            Color barColor;
            if (ratio > 0.5f) {
                barColor = Color.Lerp(new Color(1f, 0.85f, 0f), new Color(0.3f, 0.69f, 0.31f), (ratio - 0.5f) * 2f);
            } else {
                barColor = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(1f, 0.85f, 0f), ratio * 2f);
            }
            data.healthBarFill.style.backgroundColor = barColor;
        }
    }
}

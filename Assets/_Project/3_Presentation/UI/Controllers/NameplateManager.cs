using UnityEngine;
using UnityEngine.UIElements;
using Genesis.Simulation;
using Genesis.Core;
using System.Collections.Generic;

namespace Genesis.Presentation.UI {

    /// <summary>
    /// Manages 2D Screen-Space nameplates for all players.
    /// Projects 3D foot position to 2D UI Toolkit panel coordinates.
    /// </summary>
    public class NameplateManager : MonoBehaviour {

        [Header("References")]
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private VisualTreeAsset _nameplateTemplate;

        private VisualElement _container;
        private Camera _mainCamera;

        private struct NameplateData {
            public PlayerClassManager player;
            public VisualElement element;
            public Label label;
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
        }

        private void OnDisable() {
            EventBus.Unsubscribe<PlayerClassManager>("OnPlayerNameplateRegister", OnPlayerRegister);
            EventBus.Unsubscribe<PlayerClassManager>("OnPlayerNameplateUnregister", OnPlayerUnregister);
            EventBus.Unsubscribe<string>("OnPlayerNameChanged", OnGlobalNameChanged);
        }

        private void OnGlobalNameChanged(string newName) {
            // Force UpdateNameplateList next frame if needed
            // and refresh all existing names
            foreach (var data in _activeNameplates) {
                if (data.player != null && data.label != null) {
                    data.label.text = data.player.PlayerName;
                }
            }
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

            // Simple reconciliation: ensure we have a nameplate for every registered player
            UpdateNameplateList();

            // Position and update nameplates
            for (int i = _activeNameplates.Count - 1; i >= 0; i--) {
                var data = _activeNameplates[i];
                
                if (data.player == null) {
                    _container.Remove(data.element);
                    _activeNameplates.RemoveAt(i);
                    continue;
                }

                UpdateNameplatePosition(data);
            }
        }

        private void UpdateNameplateList() {
            // Add missing players
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

            VisualElement element = _nameplateTemplate.Instantiate();
            VisualElement nameplateRoot = element.Q<VisualElement>("Nameplate");
            Label label = element.Q<Label>("NameLabel");

            if (label != null) {
                label.text = player.PlayerName;
            }

            _container.Add(nameplateRoot);
            _activeNameplates.Add(new NameplateData {
                player = player,
                element = nameplateRoot,
                label = label
            });
        }

        private void UpdateNameplatePosition(NameplateData data) {
            // Position at feet (transform.position)
            Vector3 worldPos = data.player.transform.position;
            
            // Check if on screen
            Vector3 viewportPos = _mainCamera.WorldToViewportPoint(worldPos);
            bool onScreen = viewportPos.z > 0 && viewportPos.x >= 0 && viewportPos.x <= 1 && viewportPos.y >= 0 && viewportPos.y <= 1;

            // Hide if name is empty (waiting for sync)
            string displayName = data.player.PlayerName;
            if (string.IsNullOrEmpty(displayName)) {
                data.element.style.display = DisplayStyle.None;
                return;
            }

            // Update text if name changed (e.g. after sync)
            if (data.label != null && data.label.text != displayName) {
                data.label.text = displayName;
            }

            data.element.style.display = DisplayStyle.Flex;

            // Convert world to panel position
            Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(_container.panel, worldPos, _mainCamera);
            
            // Apply position
            data.element.style.left = panelPos.x;
            data.element.style.top = panelPos.y;
        }
    }
}

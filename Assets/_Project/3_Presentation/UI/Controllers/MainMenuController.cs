using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Genesis.Core;
using Genesis.Core.Networking;
using Genesis.Presentation.Audio;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Genesis.Presentation.UI {

    /// <summary>
    /// Main Menu with tabbed settings (Audio, Graphics) and Logout.
    /// Opened via BtnSetup (OnToggleMainMenu) or ESC key.
    /// Replaces the old AudioSettingsController.
    /// </summary>
    public class MainMenuController : MonoBehaviour {

        [Header("UI")]
        [SerializeField] private UIDocument _uiDocument;

        // ─── Root ───
        private VisualElement _overlay;
        private Button _closeButton;
        private Button _logoutButton;

        // ─── Tabs ───
        private Button _tabAudio;
        private Button _tabGraphics;
        private VisualElement _audioContent;
        private VisualElement _graphicsContent;

        // ─── Audio ───
        private Slider _sliderMaster;
        private Slider _sliderMusic;
        private Slider _sliderSFX;
        private Slider _sliderAmbient;
        private Label _valueMaster;
        private Label _valueMusic;
        private Label _valueSFX;
        private Label _valueAmbient;

        // ─── Graphics ───
        private Slider _sliderSensitivity;
        private Label _valueSensitivity;
        private Toggle _toggleMotionBlur;
        private Toggle _toggleVSync;
        private DropdownField _dropdownAA;

        private bool _isVisible;

        private static readonly string[] AA_OPTIONS = { "Off", "2x MSAA", "4x MSAA", "8x MSAA" };
        private static readonly int[] AA_VALUES = { 0, 2, 4, 8 };

        // ═══════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════

        private void OnEnable() {
            if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) return;

            var root = _uiDocument.rootVisualElement;
            if (root == null) return;

            QueryElements(root);
            SetupCallbacks();
            LoadAllSettings();

            if (_overlay != null)
                _overlay.style.display = DisplayStyle.None;

            EventBus.Subscribe("OnToggleMainMenu", ToggleVisibility);
        }

        private void OnDisable() {
            SaveAllSettings();
            EventBus.Unsubscribe("OnToggleMainMenu", ToggleVisibility);
        }

        private void Update() {
            if (Keyboard.current == null) return;
            if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (LoginController.IsActive) return;

            ToggleVisibility();
        }

        // ═══════════════════════════════════════════════════════
        // QUERY & SETUP
        // ═══════════════════════════════════════════════════════

        private void QueryElements(VisualElement root) {
            _overlay = root.Q<VisualElement>("MainMenuOverlay");
            _closeButton = root.Q<Button>("CloseButton");
            _logoutButton = root.Q<Button>("LogoutButton");

            _tabAudio = root.Q<Button>("TabAudio");
            _tabGraphics = root.Q<Button>("TabGraphics");
            _audioContent = root.Q<VisualElement>("AudioContent");
            _graphicsContent = root.Q<VisualElement>("GraphicsContent");

            // Audio
            _sliderMaster = root.Q<Slider>("SliderMaster");
            _sliderMusic = root.Q<Slider>("SliderMusic");
            _sliderSFX = root.Q<Slider>("SliderSFX");
            _sliderAmbient = root.Q<Slider>("SliderAmbient");
            _valueMaster = root.Q<Label>("ValueMaster");
            _valueMusic = root.Q<Label>("ValueMusic");
            _valueSFX = root.Q<Label>("ValueSFX");
            _valueAmbient = root.Q<Label>("ValueAmbient");

            // Graphics
            _sliderSensitivity = root.Q<Slider>("SliderSensitivity");
            _valueSensitivity = root.Q<Label>("ValueSensitivity");
            _toggleMotionBlur = root.Q<Toggle>("ToggleMotionBlur");
            _toggleVSync = root.Q<Toggle>("ToggleVSync");
            _dropdownAA = root.Q<DropdownField>("DropdownAA");

            if (_dropdownAA != null)
                _dropdownAA.choices = new System.Collections.Generic.List<string>(AA_OPTIONS);
        }

        private void SetupCallbacks() {
            _closeButton?.RegisterCallback<ClickEvent>(_ => Hide());
            _logoutButton?.RegisterCallback<ClickEvent>(_ => OnLogout());

            // Tabs
            _tabAudio?.RegisterCallback<ClickEvent>(_ => SwitchTab("Audio"));
            _tabGraphics?.RegisterCallback<ClickEvent>(_ => SwitchTab("Graphics"));

            // Audio
            _sliderMaster?.RegisterValueChangedCallback(e => {
                AudioManager.Instance?.SetMasterVolume(e.newValue);
                UpdatePercent(_valueMaster, e.newValue);
            });
            _sliderMusic?.RegisterValueChangedCallback(e => {
                AudioManager.Instance?.SetMusicVolume(e.newValue);
                UpdatePercent(_valueMusic, e.newValue);
            });
            _sliderSFX?.RegisterValueChangedCallback(e => {
                AudioManager.Instance?.SetSFXVolume(e.newValue);
                UpdatePercent(_valueSFX, e.newValue);
            });
            _sliderAmbient?.RegisterValueChangedCallback(e => {
                AudioManager.Instance?.SetAmbientVolume(e.newValue);
                UpdatePercent(_valueAmbient, e.newValue);
            });

            // Graphics
            _sliderSensitivity?.RegisterValueChangedCallback(e => OnSensitivityChanged(e.newValue));
            _toggleMotionBlur?.RegisterValueChangedCallback(e => ApplyMotionBlur(e.newValue));
            _toggleVSync?.RegisterValueChangedCallback(e => ApplyVSync(e.newValue));
            _dropdownAA?.RegisterValueChangedCallback(e => OnAAChanged(e.newValue));
        }

        // ═══════════════════════════════════════════════════════
        // TABS
        // ═══════════════════════════════════════════════════════

        private void SwitchTab(string tab) {
            bool isAudio = tab == "Audio";

            if (_audioContent != null)
                _audioContent.style.display = isAudio ? DisplayStyle.Flex : DisplayStyle.None;
            if (_graphicsContent != null)
                _graphicsContent.style.display = isAudio ? DisplayStyle.None : DisplayStyle.Flex;

            _tabAudio?.EnableInClassList("tab-active", isAudio);
            _tabGraphics?.EnableInClassList("tab-active", !isAudio);
        }

        // ═══════════════════════════════════════════════════════
        // VISIBILITY
        // ═══════════════════════════════════════════════════════

        public void ToggleVisibility() {
            if (_isVisible) Hide();
            else Show();
        }

        public void Show() {
            if (_overlay == null) return;
            _overlay.style.display = DisplayStyle.Flex;
            _isVisible = true;
        }

        public void Hide() {
            if (_overlay == null) return;
            _overlay.style.display = DisplayStyle.None;
            _isVisible = false;
            SaveAllSettings();
        }

        public bool IsOpen => _isVisible;

        // ═══════════════════════════════════════════════════════
        // AUDIO
        // ═══════════════════════════════════════════════════════

        private void UpdatePercent(Label label, float value) {
            if (label != null) label.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        // ═══════════════════════════════════════════════════════
        // GRAPHICS
        // ═══════════════════════════════════════════════════════

        private void OnSensitivityChanged(float value) {
            if (_valueSensitivity != null)
                _valueSensitivity.text = $"{Mathf.RoundToInt(value)}";
            if (LostArkCamera.Instance != null)
                LostArkCamera.Instance.yawSpeed = value;
        }

        private void ApplyMotionBlur(bool enabled) {
            var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            foreach (var vol in volumes) {
                if (vol.profile == null) continue;
                foreach (var comp in vol.profile.components) {
                    if (comp.GetType().Name == "MotionBlur") {
                        comp.active = enabled;
                        return;
                    }
                }
            }
        }

        private void ApplyVSync(bool enabled) {
            QualitySettings.vSyncCount = enabled ? 1 : 0;
        }

        private void OnAAChanged(string choice) {
            int index = System.Array.IndexOf(AA_OPTIONS, choice);
            if (index >= 0)
                QualitySettings.antiAliasing = AA_VALUES[index];
        }

        // ═══════════════════════════════════════════════════════
        // LOGOUT
        // ═══════════════════════════════════════════════════════

        private void OnLogout() {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            var bootstrap = Object.FindFirstObjectByType<NetworkBootstrap>();
            bootstrap?.StopConnection();

            LoginData.IsSet = false;
            LoginData.LoginRequired = true;

            SceneManager.LoadScene("Bootstrap");
#endif
        }

        // ═══════════════════════════════════════════════════════
        // PERSISTENCE
        // ═══════════════════════════════════════════════════════

        private void LoadAllSettings() {
            // Audio
            // One-time migration to force 50% for existing users
            if (PlayerPrefs.GetInt("audio_v2_initialized", 0) == 0) {
                PlayerPrefs.SetFloat("vol_master", 0.5f);
                PlayerPrefs.SetFloat("vol_music", 0.5f);
                PlayerPrefs.SetFloat("vol_sfx", 0.5f);
                PlayerPrefs.SetFloat("vol_ambient", 0.5f);
                PlayerPrefs.SetInt("audio_v2_initialized", 1);
                PlayerPrefs.Save();
            }

            float master = PlayerPrefs.GetFloat("vol_master", 0.5f);
            float music = PlayerPrefs.GetFloat("vol_music", 0.5f);
            float sfx = PlayerPrefs.GetFloat("vol_sfx", 0.5f);
            float ambient = PlayerPrefs.GetFloat("vol_ambient", 0.5f);

            SetSlider(_sliderMaster, master); UpdatePercent(_valueMaster, master);
            SetSlider(_sliderMusic, music);   UpdatePercent(_valueMusic, music);
            SetSlider(_sliderSFX, sfx);       UpdatePercent(_valueSFX, sfx);
            SetSlider(_sliderAmbient, ambient); UpdatePercent(_valueAmbient, ambient);

            // Graphics
            float sensitivity = PlayerPrefs.GetFloat("gfx_sensitivity", 20f);
            bool motionBlur = PlayerPrefs.GetInt("gfx_motion_blur", 0) == 1;
            bool vsync = PlayerPrefs.GetInt("gfx_vsync", 1) == 1;
            int aa = PlayerPrefs.GetInt("gfx_antialiasing", 2);

            SetSlider(_sliderSensitivity, sensitivity);
            if (_valueSensitivity != null) _valueSensitivity.text = $"{Mathf.RoundToInt(sensitivity)}";
            if (_toggleMotionBlur != null) _toggleMotionBlur.value = motionBlur;
            if (_toggleVSync != null) _toggleVSync.value = vsync;
            if (_dropdownAA != null) {
                int aaIndex = System.Array.IndexOf(AA_VALUES, aa);
                _dropdownAA.index = aaIndex >= 0 ? aaIndex : 0;
            }

            // Apply graphics on load
            if (LostArkCamera.Instance != null)
                LostArkCamera.Instance.yawSpeed = sensitivity;
            ApplyMotionBlur(motionBlur);
            ApplyVSync(vsync);
            QualitySettings.antiAliasing = aa;
        }

        private void SaveAllSettings() {
            // Audio
            if (_sliderMaster != null)  PlayerPrefs.SetFloat("vol_master", _sliderMaster.value);
            if (_sliderMusic != null)   PlayerPrefs.SetFloat("vol_music", _sliderMusic.value);
            if (_sliderSFX != null)     PlayerPrefs.SetFloat("vol_sfx", _sliderSFX.value);
            if (_sliderAmbient != null) PlayerPrefs.SetFloat("vol_ambient", _sliderAmbient.value);

            // Graphics
            if (_sliderSensitivity != null) PlayerPrefs.SetFloat("gfx_sensitivity", _sliderSensitivity.value);
            if (_toggleMotionBlur != null)  PlayerPrefs.SetInt("gfx_motion_blur", _toggleMotionBlur.value ? 1 : 0);
            if (_toggleVSync != null)       PlayerPrefs.SetInt("gfx_vsync", _toggleVSync.value ? 1 : 0);
            if (_dropdownAA != null) {
                int index = _dropdownAA.index;
                if (index >= 0 && index < AA_VALUES.Length)
                    PlayerPrefs.SetInt("gfx_antialiasing", AA_VALUES[index]);
            }

            PlayerPrefs.Save();
        }

        private void SetSlider(Slider slider, float value) {
            if (slider != null) slider.value = value;
        }
    }
}

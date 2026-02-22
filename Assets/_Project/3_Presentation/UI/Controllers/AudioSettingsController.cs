using UnityEngine;
using UnityEngine.UIElements;
using Genesis.Presentation.Audio;

namespace Genesis.Presentation.UI {

    public class AudioSettingsController : MonoBehaviour {

        [Header("UI")]
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _window;
        private Slider _sliderMaster;
        private Slider _sliderMusic;
        private Slider _sliderSFX;
        private Slider _sliderAmbient;
        private Label _valueMaster;
        private Label _valueMusic;
        private Label _valueSFX;
        private Label _valueAmbient;
        private Button _closeButton;

        private bool _isVisible;

        private void OnEnable() {
            if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) return;

            var root = _uiDocument.rootVisualElement;
            if (root == null) return;

            root.pickingMode = PickingMode.Ignore;

            _window = root.Q<VisualElement>("AudioSettingsWindow");
            _sliderMaster = root.Q<Slider>("SliderMaster");
            _sliderMusic = root.Q<Slider>("SliderMusic");
            _sliderSFX = root.Q<Slider>("SliderSFX");
            _sliderAmbient = root.Q<Slider>("SliderAmbient");
            _valueMaster = root.Q<Label>("ValueMaster");
            _valueMusic = root.Q<Label>("ValueMusic");
            _valueSFX = root.Q<Label>("ValueSFX");
            _valueAmbient = root.Q<Label>("ValueAmbient");
            _closeButton = root.Q<Button>("CloseButton");

            // Load saved values
            LoadSettings();

            // Register callbacks
            _sliderMaster?.RegisterValueChangedCallback(e => OnMasterChanged(e.newValue));
            _sliderMusic?.RegisterValueChangedCallback(e => OnMusicChanged(e.newValue));
            _sliderSFX?.RegisterValueChangedCallback(e => OnSFXChanged(e.newValue));
            _sliderAmbient?.RegisterValueChangedCallback(e => OnAmbientChanged(e.newValue));
            _closeButton?.RegisterCallback<ClickEvent>(e => Hide());

            // Start hidden
            if (_window != null)
                _window.style.display = DisplayStyle.None;

            // Subscribe to toggle event
            Genesis.Core.EventBus.Subscribe("OnToggleAudioSettings", ToggleVisibility);
        }

        private void OnDisable() {
            SaveSettings();
            Genesis.Core.EventBus.Unsubscribe("OnToggleAudioSettings", ToggleVisibility);
        }

        private void LoadSettings() {
            float master = PlayerPrefs.GetFloat("vol_master", 0.5f);
            float music = PlayerPrefs.GetFloat("vol_music", 0.5f);
            float sfx = PlayerPrefs.GetFloat("vol_sfx", 0.5f);
            float ambient = PlayerPrefs.GetFloat("vol_ambient", 0.5f);

            if (_sliderMaster != null) _sliderMaster.value = master;
            if (_sliderMusic != null) _sliderMusic.value = music;
            if (_sliderSFX != null) _sliderSFX.value = sfx;
            if (_sliderAmbient != null) _sliderAmbient.value = ambient;

            UpdateLabel(_valueMaster, master);
            UpdateLabel(_valueMusic, music);
            UpdateLabel(_valueSFX, sfx);
            UpdateLabel(_valueAmbient, ambient);
        }

        private void SaveSettings() {
            if (_sliderMaster != null) PlayerPrefs.SetFloat("vol_master", _sliderMaster.value);
            if (_sliderMusic != null) PlayerPrefs.SetFloat("vol_music", _sliderMusic.value);
            if (_sliderSFX != null) PlayerPrefs.SetFloat("vol_sfx", _sliderSFX.value);
            if (_sliderAmbient != null) PlayerPrefs.SetFloat("vol_ambient", _sliderAmbient.value);
            PlayerPrefs.Save();
        }

        private void OnMasterChanged(float value) {
            AudioManager.Instance?.SetMasterVolume(value);
            PlayerPrefs.SetFloat("vol_master", value);
            UpdateLabel(_valueMaster, value);
        }

        private void OnMusicChanged(float value) {
            AudioManager.Instance?.SetMusicVolume(value);
            PlayerPrefs.SetFloat("vol_music", value);
            UpdateLabel(_valueMusic, value);
        }

        private void OnSFXChanged(float value) {
            AudioManager.Instance?.SetSFXVolume(value);
            PlayerPrefs.SetFloat("vol_sfx", value);
            UpdateLabel(_valueSFX, value);
        }

        private void OnAmbientChanged(float value) {
            AudioManager.Instance?.SetAmbientVolume(value);
            PlayerPrefs.SetFloat("vol_ambient", value);
            UpdateLabel(_valueAmbient, value);
        }

        private void UpdateLabel(Label label, float value) {
            if (label != null)
                label.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        public void ToggleVisibility() {
            if (_isVisible) Hide();
            else Show();
        }

        public void Show() {
            if (_window == null) return;
            _window.style.display = DisplayStyle.Flex;
            _isVisible = true;
        }

        public void Hide() {
            if (_window == null) return;
            _window.style.display = DisplayStyle.None;
            _isVisible = false;
            SaveSettings();
        }
    }
}

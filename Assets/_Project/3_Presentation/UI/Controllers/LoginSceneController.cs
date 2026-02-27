using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Genesis.Core;
using Genesis.Core.Networking;
using Genesis.Core.Persistence;
using Genesis.Data;
// Audio handled locally via AudioSource (no AudioManager in Login scene)
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Genesis.Presentation.UI {

    /// <summary>
    /// Main controller for the Login scene. Manages Login → Register → CharacterCreation → Loading flow.
    /// Lives in the Login scene (index 0), loads Bootstrap scene additively on success.
    /// </summary>
    public class LoginSceneController : MonoBehaviour {

        public static bool IsActive { get; private set; }

        private enum State { Login, Register, CharacterCreation, Loading }

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private List<ClassData> availableClasses;

        [Header("Audio")]
        [SerializeField] private AudioClip _loginMusic;
        [SerializeField] private AudioClip _enterWorldSFX;
        [SerializeField] private AudioSource _audioSource;

        private VisualElement _root;

        // Panels
        private VisualElement _loginPanel;
        private VisualElement _registerPanel;
        private VisualElement _charCreationPanel;
        private VisualElement _loadingPanel;

        // Login elements
        private TextField _loginUsername;
        private TextField _loginPassword;
        private Button _btnLogin;
        private Label _loginStatus;
        private Button _btnGoRegister;

        // Register elements
        private TextField _registerUsername;
        private TextField _registerPassword;
        private TextField _registerPasswordConfirm;
        private Button _btnRegister;
        private Label _registerStatus;
        private Button _btnGoLogin;

        // Character creation elements
        private ScrollView _ccClassListScroll;
        private VisualElement _ccDetailPanel;
        private VisualElement _ccClassIconLarge;
        private Label _ccClassNameLabel;
        private Label _ccClassDescription;
        private VisualElement _ccAbilitiesRow;
        private Label _ccStatHealth, _ccStatMana, _ccStatHPRegen, _ccStatMPRegen, _ccStatHPLevel, _ccStatMPLevel;
        private TextField _ccNameInput;
        private Button _ccBtnCitizen, _ccBtnPK;
        private Button _btnCreateCharacter;
        private Label _ccStatus;

        // Tooltip
        private VisualElement _abilityTooltip;
        private Label _tooltipName, _tooltipCategory, _tooltipDescription;
        private Label _tooltipMana, _tooltipCooldown, _tooltipCastTime, _tooltipRange, _tooltipDamage, _tooltipHeal;

        // Loading elements
        private VisualElement _loadingBarFill;
        private Label _loadingDots;
        private Label _loadingTip;

        // State
        private State _currentState = State.Login;
        private int _selectedClassIndex = 0;
        private int _selectedFactionIndex = 0;
        private List<VisualElement> _classEntries = new List<VisualElement>();
        private bool _isLoading;
        private bool _isProcessing; // prevents double-click

        private static readonly string[] LOADING_TIPS = {
            "Full loot means full risk. Choose your fights wisely.",
            "Party up to increase your chances of survival.",
            "Citizens can trade freely. PKs live on the edge.",
            "Explore the world to find hidden loot chests.",
            "Keep an eye on your mana during long fights.",
            "The Citizen path offers safety. The PK path offers glory.",
        };

        // ═══════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════

        void Awake() {
            LoginData.LoginRequired = true;
        }

        void Update() {
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) {
                if (_isProcessing) return;

                switch (_currentState) {
                    case State.Login:
                        if (_btnLogin != null && _btnLogin.enabledSelf) OnLoginClicked();
                        break;
                    case State.Register:
                        if (_btnRegister != null && _btnRegister.enabledSelf) OnRegisterClicked();
                        break;
                    case State.CharacterCreation:
                        if (_btnCreateCharacter != null && _btnCreateCharacter.enabledSelf) OnCreateCharacterClicked();
                        break;
                }
            }
        }

        void Start() {
            AutoFindClasses();

            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) { Debug.LogError("[LoginScene] No UIDocument found!"); return; }

            uiDocument.sortingOrder = 100;
            _root = uiDocument.rootVisualElement;
            if (_root == null) return;

            QueryElements();
            SetupCallbacks();
            SetPlaceholders();
            SwitchState(State.Login);

            IsActive = true;

            // Play login music via local AudioSource (no AudioManager in this scene)
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource != null && _loginMusic != null) {
                _audioSource.clip = _loginMusic;
                _audioSource.loop = true;
                _audioSource.volume = 0.5f;
                _audioSource.Play();
            }

            Debug.Log("[LoginScene] Initialized.");
        }

        void OnDestroy() {
            IsActive = false;
            LoginData.LoginRequired = false;
        }

        // ═══════════════════════════════════════════════════════
        // QUERY & SETUP
        // ═══════════════════════════════════════════════════════

        private void QueryElements() {
            // Panels
            _loginPanel = _root.Q<VisualElement>("LoginPanel");
            _registerPanel = _root.Q<VisualElement>("RegisterPanel");
            _charCreationPanel = _root.Q<VisualElement>("CharCreationPanel");
            _loadingPanel = _root.Q<VisualElement>("LoadingPanel");

            // Login
            _loginUsername = _root.Q<TextField>("LoginUsername");
            _loginPassword = _root.Q<TextField>("LoginPassword");
            _btnLogin = _root.Q<Button>("BtnLogin");
            _loginStatus = _root.Q<Label>("LoginStatus");
            _btnGoRegister = _root.Q<Button>("BtnGoRegister");

            // Register
            _registerUsername = _root.Q<TextField>("RegisterUsername");
            _registerPassword = _root.Q<TextField>("RegisterPassword");
            _registerPasswordConfirm = _root.Q<TextField>("RegisterPasswordConfirm");
            _btnRegister = _root.Q<Button>("BtnRegister");
            _registerStatus = _root.Q<Label>("RegisterStatus");
            _btnGoLogin = _root.Q<Button>("BtnGoLogin");

            // Character Creation
            _ccClassListScroll = _root.Q<ScrollView>("CCClassListScroll");
            _ccDetailPanel = _root.Q<VisualElement>("CCDetailPanel");
            _ccClassIconLarge = _root.Q<VisualElement>("CCClassIconLarge");
            _ccClassNameLabel = _root.Q<Label>("CCClassNameLabel");
            _ccClassDescription = _root.Q<Label>("CCClassDescription");
            _ccAbilitiesRow = _root.Q<VisualElement>("CCAbilitiesRow");
            _ccStatHealth = _root.Q<Label>("CCStatHealth");
            _ccStatMana = _root.Q<Label>("CCStatMana");
            _ccStatHPRegen = _root.Q<Label>("CCStatHPRegen");
            _ccStatMPRegen = _root.Q<Label>("CCStatMPRegen");
            _ccStatHPLevel = _root.Q<Label>("CCStatHPLevel");
            _ccStatMPLevel = _root.Q<Label>("CCStatMPLevel");
            _ccNameInput = _root.Q<TextField>("CCNameInput");
            _ccBtnCitizen = _root.Q<Button>("CCBtnCitizen");
            _ccBtnPK = _root.Q<Button>("CCBtnPK");
            _btnCreateCharacter = _root.Q<Button>("BtnCreateCharacter");
            _ccStatus = _root.Q<Label>("CCStatus");

            // Tooltip
            _abilityTooltip = _root.Q<VisualElement>("AbilityTooltip");
            _tooltipName = _root.Q<Label>("TooltipName");
            _tooltipCategory = _root.Q<Label>("TooltipCategory");
            _tooltipDescription = _root.Q<Label>("TooltipDescription");
            _tooltipMana = _root.Q<Label>("TooltipMana");
            _tooltipCooldown = _root.Q<Label>("TooltipCooldown");
            _tooltipCastTime = _root.Q<Label>("TooltipCastTime");
            _tooltipRange = _root.Q<Label>("TooltipRange");
            _tooltipDamage = _root.Q<Label>("TooltipDamage");
            _tooltipHeal = _root.Q<Label>("TooltipHeal");

            // Loading
            _loadingBarFill = _root.Q<VisualElement>("LoadingBarFill");
            _loadingDots = _root.Q<Label>("LoadingDots");
            _loadingTip = _root.Q<Label>("LoadingTip");
        }

        private void SetupCallbacks() {
            // Login
            _btnLogin?.RegisterCallback<ClickEvent>(_ => OnLoginClicked());
            _btnGoRegister?.RegisterCallback<ClickEvent>(_ => SwitchState(State.Register));

            // Register
            _btnRegister?.RegisterCallback<ClickEvent>(_ => OnRegisterClicked());
            _btnGoLogin?.RegisterCallback<ClickEvent>(_ => SwitchState(State.Login));

            // Character Creation
            _btnCreateCharacter?.RegisterCallback<ClickEvent>(_ => OnCreateCharacterClicked());
            _ccBtnCitizen?.RegisterCallback<ClickEvent>(_ => SelectFaction(0));
            _ccBtnPK?.RegisterCallback<ClickEvent>(_ => SelectFaction(1));

            // Hide tooltip on click
            _root.RegisterCallback<PointerDownEvent>(_ => HideAbilityTooltip());
        }

        private void SetPlaceholders() {
            // UI Toolkit TextFields don't have native placeholder; we use label as hint
            if (_loginUsername != null) _loginUsername.label = "";
            if (_loginPassword != null) _loginPassword.label = "";
            if (_registerUsername != null) _registerUsername.label = "";
            if (_registerPassword != null) _registerPassword.label = "";
            if (_registerPasswordConfirm != null) _registerPasswordConfirm.label = "";
        }

        private void AutoFindClasses() {
            if (availableClasses != null && availableClasses.Count > 0) return;
#if UNITY_EDITOR
            availableClasses = new List<ClassData>();
            string[] guids = AssetDatabase.FindAssets("t:ClassData");
            foreach (string guid in guids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ClassData>(path);
                if (asset != null)
                    availableClasses.Add(asset);
            }
            availableClasses = availableClasses.OrderBy(c => c.ClassName).ToList();
            Debug.Log($"[LoginScene] Auto-found {availableClasses.Count} classes.");
#else
            Debug.LogWarning("[LoginScene] No classes assigned! Assign ClassData assets in the Inspector.");
#endif
        }

        // ═══════════════════════════════════════════════════════
        // STATE MACHINE
        // ═══════════════════════════════════════════════════════

        private void SwitchState(State state) {
            _currentState = state;
            _loginPanel.style.display = state == State.Login ? DisplayStyle.Flex : DisplayStyle.None;
            _registerPanel.style.display = state == State.Register ? DisplayStyle.Flex : DisplayStyle.None;
            _charCreationPanel.style.display = state == State.CharacterCreation ? DisplayStyle.Flex : DisplayStyle.None;
            _loadingPanel.style.display = state == State.Loading ? DisplayStyle.Flex : DisplayStyle.None;

            // Clear status labels when switching
            if (_loginStatus != null) _loginStatus.text = "";
            if (_registerStatus != null) _registerStatus.text = "";
            if (_ccStatus != null) _ccStatus.text = "";

            if (state == State.CharacterCreation) {
                BuildClassList();
                if (availableClasses != null && availableClasses.Count > 0)
                    SelectClass(0);
            }

            if (state == State.Loading) {
                ShowRandomTip();
                _isLoading = true;
                StartCoroutine(AnimateLoadingDots());
                StartCoroutine(AnimateLoadingBar());
            }
        }

        // ═══════════════════════════════════════════════════════
        // LOGIN
        // ═══════════════════════════════════════════════════════

        private async void OnLoginClicked() {
            if (_isProcessing) return;

            string username = _loginUsername?.value?.Trim() ?? "";
            string password = _loginPassword?.value ?? "";

            if (username.Length < 2) {
                SetStatus(_loginStatus, "Username must be at least 2 characters.", false);
                return;
            }
            if (password.Length < 6) {
                SetStatus(_loginStatus, "Password must be at least 6 characters.", false);
                return;
            }

            _isProcessing = true;
            _btnLogin?.SetEnabled(false);
            SetStatus(_loginStatus, "Connecting...", true);

            var result = await NakamaAuthClient.LoginAsync(username, password);

            if (result.Success) {
                LoginData.Username = username;
                LoginData.AuthToken = result.AuthToken;
                LoginData.RefreshToken = result.RefreshToken;
                LoginData.UserId = result.UserId;

                if (result.Character != null) {
                    // Existing character: go straight to loading
                    LoginData.PlayerName = result.Character.playerName;
                    LoginData.ClassIndex = result.Character.classIndex;
                    LoginData.FactionIndex = result.Character.faction;
                    LoginData.IsNewCharacter = false;
                    LoginData.IsSet = true;

                    SetStatus(_loginStatus, $"Welcome back, {result.Character.playerName}!", true);
                    TransitionToLoading();
                } else {
                    // No character yet: go to character creation
                    LoginData.IsNewCharacter = true;
                    SetStatus(_loginStatus, "Account found. Create your character.", true);
                    _isProcessing = false;
                    _btnLogin?.SetEnabled(true);
                    SwitchState(State.CharacterCreation);
                }
            } else {
                SetStatus(_loginStatus, result.Error, false);
                _isProcessing = false;
                _btnLogin?.SetEnabled(true);
            }
        }

        // ═══════════════════════════════════════════════════════
        // REGISTER
        // ═══════════════════════════════════════════════════════

        private async void OnRegisterClicked() {
            if (_isProcessing) return;

            string username = _registerUsername?.value?.Trim() ?? "";
            string password = _registerPassword?.value ?? "";
            string passwordConfirm = _registerPasswordConfirm?.value ?? "";

            if (username.Length < 2) {
                SetStatus(_registerStatus, "Username must be at least 2 characters.", false);
                return;
            }
            if (password.Length < 6) {
                SetStatus(_registerStatus, "Password must be at least 6 characters.", false);
                return;
            }
            if (password != passwordConfirm) {
                SetStatus(_registerStatus, "Passwords do not match.", false);
                return;
            }

            _isProcessing = true;
            _btnRegister?.SetEnabled(false);
            SetStatus(_registerStatus, "Creating account...", true);

            var result = await NakamaAuthClient.RegisterAsync(username, password);

            if (result.Success) {
                LoginData.Username = username;
                LoginData.AuthToken = result.AuthToken;
                LoginData.RefreshToken = result.RefreshToken;
                LoginData.UserId = result.UserId;
                LoginData.IsNewCharacter = true;

                SetStatus(_registerStatus, "Account created! Create your character.", true);
                _isProcessing = false;
                _btnRegister?.SetEnabled(true);
                SwitchState(State.CharacterCreation);
            } else {
                SetStatus(_registerStatus, result.Error, false);
                _isProcessing = false;
                _btnRegister?.SetEnabled(true);
            }
        }

        // ═══════════════════════════════════════════════════════
        // CHARACTER CREATION
        // ═══════════════════════════════════════════════════════

        private void OnCreateCharacterClicked() {
            string charName = _ccNameInput?.value?.Trim() ?? "";
            if (charName.Length < 2) {
                SetStatus(_ccStatus, "Character name must be at least 2 characters.", false);
                return;
            }

            LoginData.PlayerName = charName;
            LoginData.ClassIndex = _selectedClassIndex;
            LoginData.FactionIndex = _selectedFactionIndex;
            LoginData.IsSet = true;

            TransitionToLoading();
        }

        private void TransitionToLoading() {
            SwitchState(State.Loading);
            StartCoroutine(LoadBootstrapAndWaitForSpawn());
        }

        // ═══════════════════════════════════════════════════════
        // LOADING & BOOTSTRAP
        // ═══════════════════════════════════════════════════════

        private IEnumerator LoadBootstrapAndWaitForSpawn() {
            // Wait a frame so loading UI renders
            yield return null;

            // Load Bootstrap scene additively
            Debug.Log("[LoginScene] Loading Bootstrap scene additively...");
            var asyncOp = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Additive);
            if (asyncOp == null) {
                Debug.LogError("[LoginScene] Failed to load Bootstrap scene! Is it in Build Settings?");
                SwitchState(State.Login);
                yield break;
            }

            // Wait for Bootstrap to load
            while (!asyncOp.isDone) {
                if (_loadingBarFill != null)
                    _loadingBarFill.style.width = Length.Percent(asyncOp.progress * 60f); // 0-60%
                yield return null;
            }

            Debug.Log("[LoginScene] Bootstrap scene loaded.");
            if (_loadingBarFill != null)
                _loadingBarFill.style.width = Length.Percent(65f);

            // Set Bootstrap as active scene so all runtime objects belong to it (not Login)
            var bootstrapScene = SceneManager.GetSceneByName("Bootstrap");
            if (bootstrapScene.IsValid() && bootstrapScene.isLoaded)
                SceneManager.SetActiveScene(bootstrapScene);

            // Disable Login scene AudioListener and Camera to avoid conflicts with Bootstrap
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) {
                if (listener.gameObject.scene == gameObject.scene)
                    listener.enabled = false;
            }
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) {
                if (cam.gameObject.scene == gameObject.scene)
                    cam.enabled = false;
            }

            // Wait a frame for Bootstrap to initialize
            yield return null;

            // Start FishNet connection
            var bootstrap = Object.FindFirstObjectByType<NetworkBootstrap>();
            if (bootstrap == null) {
                Debug.LogError("[LoginScene] NetworkBootstrap not found in Bootstrap scene!");
                SwitchState(State.Login);
                yield break;
            }

#if UNITY_EDITOR
            bool isClone = System.IO.File.Exists(
                System.IO.Path.Combine(Application.dataPath.Replace("/Assets", ""), ".clone"));
            if (isClone) {
                bootstrap.StartClientLocal();
            } else {
                // In editor: server may need to be started + client
                bootstrap.StartClientLocal();
            }
#else
            bootstrap.StartClient();
#endif

            Debug.Log($"[LoginScene] Connecting as '{LoginData.PlayerName}', class {LoginData.ClassIndex}");
            if (_loadingBarFill != null)
                _loadingBarFill.style.width = Length.Percent(75f);

            // Wait for player spawn (LostArkCamera gets target)
            float timeout = 30f;
            float elapsed = 0f;
            while ((LostArkCamera.Instance == null || LostArkCamera.Instance.target == null) && elapsed < timeout) {
                elapsed += Time.deltaTime;
                if (_loadingBarFill != null) {
                    float p = 75f + (elapsed / timeout) * 20f;
                    _loadingBarFill.style.width = Length.Percent(Mathf.Min(p, 95f));
                }
                yield return null;
            }

            if (LostArkCamera.Instance == null || LostArkCamera.Instance.target == null) {
                Debug.LogError("[LoginScene] Timed out waiting for player spawn!");
                SwitchState(State.Login);
                yield break;
            }

            Debug.Log("[LoginScene] Player spawned! Waiting for world to load...");
            if (_loadingBarFill != null)
                _loadingBarFill.style.width = Length.Percent(95f);

            // Wait a bit for chunks to load so the player doesn't see darkness
            yield return new WaitForSeconds(2f);

            Debug.Log("[LoginScene] Transitioning to game...");

            // Release keyboard focus from Login TextFields before hiding
            _root?.focusController?.focusedElement?.Blur();

            // Immediately hide UI and disable Login scene rendering
            _isLoading = false;
            IsActive = false;
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (uiDocument != null) uiDocument.enabled = false;

            // Stop login music, play enter world SFX via local AudioSource
            if (_audioSource != null) {
                _audioSource.Stop();
                if (_enterWorldSFX != null)
                    _audioSource.PlayOneShot(_enterWorldSFX);
            }

            // Re-enable player input (Login UI TextFields may have captured keyboard focus)
            if (InputManager.Instance != null)
                InputManager.Instance.SetPlayerControlsEnabled(true);

            // Unload Login scene
            var loginScene = gameObject.scene;
            Debug.Log("[LoginScene] Unloading Login scene...");
            SceneManager.UnloadSceneAsync(loginScene);
        }

        // ═══════════════════════════════════════════════════════
        // CLASS LIST (Character Creation)
        // ═══════════════════════════════════════════════════════

        private void BuildClassList() {
            if (_ccClassListScroll == null || availableClasses == null) return;

            var container = _ccClassListScroll.contentContainer;
            container.Clear();
            _classEntries.Clear();

            for (int i = 0; i < availableClasses.Count; i++) {
                var classData = availableClasses[i];
                int index = i;

                var entry = new VisualElement();
                entry.AddToClassList("cc-class-entry");

                var icon = new VisualElement();
                icon.AddToClassList("cc-class-icon");
                if (classData.ClassIcon != null)
                    icon.style.backgroundImage = new StyleBackground(classData.ClassIcon);

                var nameLabel = new Label(classData.ClassName);
                nameLabel.AddToClassList("cc-class-name");

                entry.Add(icon);
                entry.Add(nameLabel);
                entry.RegisterCallback<ClickEvent>(_ => SelectClass(index));

                container.Add(entry);
                _classEntries.Add(entry);
            }
        }

        private void SelectClass(int index) {
            if (availableClasses == null || index < 0 || index >= availableClasses.Count) return;
            _selectedClassIndex = index;

            for (int i = 0; i < _classEntries.Count; i++)
                _classEntries[i].EnableInClassList("cc-class-entry--selected", i == index);

            var classData = availableClasses[index];
            PopulateClassDetail(classData);
            PopulateAbilities(classData);
        }

        private void PopulateClassDetail(ClassData classData) {
            if (_ccClassIconLarge != null && classData.ClassIcon != null)
                _ccClassIconLarge.style.backgroundImage = new StyleBackground(classData.ClassIcon);

            if (_ccClassNameLabel != null)
                _ccClassNameLabel.text = classData.ClassName;

            if (_ccClassDescription != null) {
                _ccClassDescription.text = string.IsNullOrEmpty(classData.Description)
                    ? "A brave adventurer ready to face the unknown."
                    : classData.Description;
            }

            SetStatLabel(_ccStatHealth, classData.MaxHealth);
            SetStatLabel(_ccStatMana, classData.MaxMana);
            SetStatLabel(_ccStatHPRegen, classData.HealthRegenPerSecond);
            SetStatLabel(_ccStatMPRegen, classData.ManaRegenPerSecond);
            SetStatLabel(_ccStatHPLevel, "+" + classData.HealthPerLevel);
            SetStatLabel(_ccStatMPLevel, "+" + classData.ManaPerLevel);
        }

        private void PopulateAbilities(ClassData classData) {
            if (_ccAbilitiesRow == null) return;
            _ccAbilitiesRow.Clear();

            if (classData.InitialAbilities == null) return;

            foreach (var ability in classData.InitialAbilities) {
                if (ability == null) continue;

                var slot = new VisualElement();
                slot.AddToClassList("cc-ability-slot");

                var icon = new VisualElement();
                icon.AddToClassList("cc-ability-icon");
                if (ability.Icon != null)
                    icon.style.backgroundImage = new StyleBackground(ability.Icon);

                slot.Add(icon);

                var abilityRef = ability;
                slot.RegisterCallback<MouseEnterEvent>(evt => ShowAbilityTooltip(abilityRef, evt.mousePosition));
                slot.RegisterCallback<MouseLeaveEvent>(_ => HideAbilityTooltip());

                _ccAbilitiesRow.Add(slot);
            }
        }

        private void SelectFaction(int index) {
            _selectedFactionIndex = index;
            _ccBtnCitizen?.EnableInClassList("cc-faction-button--selected", index == 0);
            _ccBtnPK?.EnableInClassList("cc-faction-button--selected", index == 1);
        }

        // ═══════════════════════════════════════════════════════
        // TOOLTIP
        // ═══════════════════════════════════════════════════════

        private void ShowAbilityTooltip(AbilityData ability, Vector2 mousePos) {
            if (_abilityTooltip == null) return;

            if (_tooltipName != null) _tooltipName.text = ability.Name;
            if (_tooltipCategory != null) _tooltipCategory.text = $"{ability.Category} · {ability.CastType}";
            if (_tooltipDescription != null) _tooltipDescription.text = ability.Description ?? "";

            SetTooltipStat(_tooltipMana, "Mana", ability.ManaCost, ability.ManaCost > 0);
            SetTooltipStat(_tooltipCooldown, "Cooldown", ability.Cooldown, ability.Cooldown > 0, "s");
            SetTooltipStat(_tooltipCastTime, "Cast Time", ability.CastTime, ability.CastTime > 0, "s");
            SetTooltipStat(_tooltipRange, "Range", ability.Range, ability.Range > 0, "m");

            if (_tooltipDamage != null) {
                _tooltipDamage.text = ability.BaseDamage > 0 ? $"Damage: {ability.BaseDamage}" : "";
                _tooltipDamage.style.display = ability.BaseDamage > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_tooltipHeal != null) {
                _tooltipHeal.text = ability.BaseHeal > 0 ? $"Heal: {ability.BaseHeal}" : "";
                _tooltipHeal.style.display = ability.BaseHeal > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            _abilityTooltip.style.display = DisplayStyle.Flex;
            _abilityTooltip.style.left = mousePos.x + 12;
            _abilityTooltip.style.top = mousePos.y - 10;
        }

        private void HideAbilityTooltip() {
            if (_abilityTooltip != null)
                _abilityTooltip.style.display = DisplayStyle.None;
        }

        // ═══════════════════════════════════════════════════════
        // LOADING ANIMATIONS
        // ═══════════════════════════════════════════════════════

        private IEnumerator AnimateLoadingDots() {
            string[] states = { ".", ". .", ". . ." };
            int i = 0;
            while (_isLoading) {
                if (_loadingDots != null)
                    _loadingDots.text = states[i++ % states.Length];
                yield return new WaitForSeconds(0.45f);
            }
        }

        private IEnumerator AnimateLoadingBar() {
            // Gentle idle pulsing until real progress kicks in
            float t = 0f;
            while (_isLoading && _loadingBarFill != null) {
                // Only do idle animation if width is still 0 (before scene load starts)
                t += Time.deltaTime;
                yield return null;
            }
        }

        private void ShowRandomTip() {
            if (_loadingTip != null && LOADING_TIPS.Length > 0)
                _loadingTip.text = LOADING_TIPS[Random.Range(0, LOADING_TIPS.Length)];
        }

        // ═══════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════

        private void SetStatus(Label label, string text, bool isSuccess) {
            if (label == null) return;
            label.text = text;
            label.EnableInClassList("status-label--success", isSuccess);
        }

        private void SetStatLabel(Label label, float value) {
            if (label != null)
                label.text = value % 1 == 0 ? ((int)value).ToString() : value.ToString("F1");
        }

        private void SetStatLabel(Label label, string value) {
            if (label != null) label.text = value;
        }

        private void SetTooltipStat(Label label, string prefix, float value, bool show, string suffix = "") {
            if (label == null) return;
            if (show) {
                label.text = value % 1 == 0
                    ? $"{prefix}: {(int)value}{suffix}"
                    : $"{prefix}: {value:F1}{suffix}";
                label.style.display = DisplayStyle.Flex;
            } else {
                label.style.display = DisplayStyle.None;
            }
        }
    }
}

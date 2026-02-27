using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Genesis.Core.Networking;
using Genesis.Data;
using Genesis.Presentation.Audio;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Genesis.Presentation.UI {

    public class LoginController : MonoBehaviour {

        public static bool IsActive { get; private set; }

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private List<ClassData> availableClasses;

        [Header("Audio")]
        [SerializeField] private AudioClip _loginMusic;
        [SerializeField] private AudioClip _enterWorldSFX;

        private VisualElement _root;
        private VisualElement _classListScroll;
        private VisualElement _classDetailPanel;
        private VisualElement _classIconLarge;
        private Label _classNameLabel;
        private Label _classDescriptionLabel;
        private VisualElement _abilitiesRow;
        private VisualElement _abilityTooltip;
        private TextField _nameInput;
        private Button _connectButton;
        private Label _statusLabel;

        private VisualElement _loadingOverlay;
        private Label _loadingDotsLabel;
        private bool _isLoading;

        // Stat labels
        private Label _statHealth, _statMana, _statHPRegen, _statMPRegen, _statHPLevel, _statMPLevel;

        // Tooltip labels
        private Label _tooltipName, _tooltipCategory, _tooltipDescription;
        private Label _tooltipMana, _tooltipCooldown, _tooltipCastTime, _tooltipRange, _tooltipDamage, _tooltipHeal;
        private VisualElement _tooltipStats;

        private int _selectedClassIndex = 0;
        private int _selectedFactionIndex = 0;
        private List<VisualElement> _classEntries = new List<VisualElement>();
        private Button _btnCitizen;
        private Button _btnPK;

        void Awake() {
            LoginData.LoginRequired = true;
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
            // Sort by name for consistent ordering
            availableClasses = availableClasses.OrderBy(c => c.ClassName).ToList();
            Debug.Log($"[LoginController] Auto-found {availableClasses.Count} classes.");
#else
            Debug.LogWarning("[LoginController] No classes assigned! Assign ClassData assets in the Inspector.");
#endif
        }

        void Update() {
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) {
                if (_connectButton != null && _connectButton.enabledSelf) {
                    OnConnectClicked();
                }
            }
        }

        void Start() {
            AutoFindClasses();

            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (uiDocument == null) {
                Debug.LogError("[LoginController] No UIDocument found!");
                return;
            }

            uiDocument.sortingOrder = 100;

            _root = uiDocument.rootVisualElement;
            if (_root == null) return;

            QueryElements();
            BuildLoadingOverlay();
            BuildClassList();

            if (availableClasses != null && availableClasses.Count > 0)
                SelectClass(0);

            _connectButton?.RegisterCallback<ClickEvent>(_ => OnConnectClicked());
            _btnCitizen?.RegisterCallback<ClickEvent>(_ => SelectFaction(0));
            _btnPK?.RegisterCallback<ClickEvent>(_ => SelectFaction(1));

            // Hide tooltip on any click
            _root.RegisterCallback<PointerDownEvent>(_ => HideAbilityTooltip());

            IsActive = true;
            Debug.Log("[LoginController] Login screen initialized.");

            if (_loginMusic != null)
                AudioManager.Instance?.PlayMusic(_loginMusic, 2f);
        }

        private void QueryElements() {
            // Class list
            var scrollView = _root.Q<ScrollView>("ClassListScroll");
            _classListScroll = scrollView?.contentContainer;

            // Class detail
            _classDetailPanel = _root.Q<VisualElement>("ClassDetailPanel");
            _classIconLarge = _root.Q<VisualElement>("ClassIconLarge");
            _classNameLabel = _root.Q<Label>("ClassNameLabel");
            _classDescriptionLabel = _root.Q<Label>("ClassDescriptionLabel");
            _abilitiesRow = _root.Q<VisualElement>("AbilitiesRow");

            // Stats
            _statHealth = _root.Q<Label>("StatHealth");
            _statMana = _root.Q<Label>("StatMana");
            _statHPRegen = _root.Q<Label>("StatHPRegen");
            _statMPRegen = _root.Q<Label>("StatMPRegen");
            _statHPLevel = _root.Q<Label>("StatHPLevel");
            _statMPLevel = _root.Q<Label>("StatMPLevel");

            // Bottom
            _nameInput = _root.Q<TextField>("NameInput");
            _connectButton = _root.Q<Button>("ConnectButton");
            _statusLabel = _root.Q<Label>("StatusLabel");

            // Faction
            _btnCitizen = _root.Q<Button>("BtnCitizen");
            _btnPK = _root.Q<Button>("BtnPK");

            // Tooltip
            _abilityTooltip = _root.Q<VisualElement>("AbilityTooltip");
            _tooltipName = _root.Q<Label>("TooltipName");
            _tooltipCategory = _root.Q<Label>("TooltipCategory");
            _tooltipDescription = _root.Q<Label>("TooltipDescription");
            _tooltipStats = _root.Q<VisualElement>("TooltipStats");
            _tooltipMana = _root.Q<Label>("TooltipMana");
            _tooltipCooldown = _root.Q<Label>("TooltipCooldown");
            _tooltipCastTime = _root.Q<Label>("TooltipCastTime");
            _tooltipRange = _root.Q<Label>("TooltipRange");
            _tooltipDamage = _root.Q<Label>("TooltipDamage");
            _tooltipHeal = _root.Q<Label>("TooltipHeal");
        }

        private void BuildClassList() {
            if (_classListScroll == null || availableClasses == null) return;

            _classListScroll.Clear();
            _classEntries.Clear();

            for (int i = 0; i < availableClasses.Count; i++) {
                var classData = availableClasses[i];
                int index = i; // capture for closure

                var entry = new VisualElement();
                entry.AddToClassList("class-list-entry");

                var icon = new VisualElement();
                icon.AddToClassList("class-list-icon");
                if (classData.ClassIcon != null)
                    icon.style.backgroundImage = new StyleBackground(classData.ClassIcon);

                var nameLabel = new Label(classData.ClassName);
                nameLabel.AddToClassList("class-list-name");

                entry.Add(icon);
                entry.Add(nameLabel);

                entry.RegisterCallback<ClickEvent>(_ => SelectClass(index));

                _classListScroll.Add(entry);
                _classEntries.Add(entry);
            }
        }

        private void SelectClass(int index) {
            if (availableClasses == null || index < 0 || index >= availableClasses.Count) return;

            _selectedClassIndex = index;

            // Update list selection visuals
            for (int i = 0; i < _classEntries.Count; i++) {
                _classEntries[i].EnableInClassList("class-list-entry--selected", i == index);
            }

            var classData = availableClasses[index];
            PopulateDetail(classData);
            PopulateAbilities(classData);
        }

        private void PopulateDetail(ClassData classData) {
            if (_classIconLarge != null && classData.ClassIcon != null)
                _classIconLarge.style.backgroundImage = new StyleBackground(classData.ClassIcon);

            if (_classNameLabel != null)
                _classNameLabel.text = classData.ClassName;

            if (_classDescriptionLabel != null) {
                string desc = string.IsNullOrEmpty(classData.Description)
                    ? GetFallbackDescription(classData.ClassName)
                    : classData.Description;
                _classDescriptionLabel.text = desc;
            }

            // Stats
            SetStatLabel(_statHealth, classData.MaxHealth);
            SetStatLabel(_statMana, classData.MaxMana);
            SetStatLabel(_statHPRegen, classData.HealthRegenPerSecond);
            SetStatLabel(_statMPRegen, classData.ManaRegenPerSecond);
            SetStatLabel(_statHPLevel, "+" + classData.HealthPerLevel);
            SetStatLabel(_statMPLevel, "+" + classData.ManaPerLevel);
        }

        private void SetStatLabel(Label label, float value) {
            if (label != null)
                label.text = value % 1 == 0 ? ((int)value).ToString() : value.ToString("F1");
        }

        private void SetStatLabel(Label label, string value) {
            if (label != null)
                label.text = value;
        }

        private string GetFallbackDescription(string className) {
            return className switch {
                "Mage" => "Masters of the arcane arts, wielding devastating spells from afar. Fragile but immensely powerful.",
                "Warrior" => "Fearless melee combatants who thrive in the heat of battle. Resilient and relentless.",
                _ => "A brave adventurer ready to face the unknown."
            };
        }

        private void PopulateAbilities(ClassData classData) {
            if (_abilitiesRow == null) return;
            _abilitiesRow.Clear();

            if (classData.InitialAbilities == null) return;

            foreach (var ability in classData.InitialAbilities) {
                if (ability == null) continue;

                var slot = new VisualElement();
                slot.AddToClassList("ability-slot-login");

                var icon = new VisualElement();
                icon.AddToClassList("ability-icon-login");
                if (ability.Icon != null)
                    icon.style.backgroundImage = new StyleBackground(ability.Icon);

                slot.Add(icon);

                // Tooltip events
                var abilityRef = ability; // capture for closure
                slot.RegisterCallback<MouseEnterEvent>(evt => {
                    ShowAbilityTooltip(abilityRef, evt.mousePosition);
                });
                slot.RegisterCallback<MouseLeaveEvent>(_ => {
                    HideAbilityTooltip();
                });

                _abilitiesRow.Add(slot);
            }
        }

        private void ShowAbilityTooltip(AbilityData ability, Vector2 mousePos) {
            if (_abilityTooltip == null) return;

            if (_tooltipName != null) _tooltipName.text = ability.Name;
            if (_tooltipCategory != null) _tooltipCategory.text = ability.Category.ToString() + " · " + ability.CastType.ToString();

            if (_tooltipDescription != null) _tooltipDescription.text = ability.Description ?? "";

            // Stats
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

            // Position tooltip near mouse, clamped to panel
            _abilityTooltip.style.display = DisplayStyle.Flex;
            _abilityTooltip.style.left = mousePos.x + 12;
            _abilityTooltip.style.top = mousePos.y - 10;
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

        private void HideAbilityTooltip() {
            if (_abilityTooltip != null)
                _abilityTooltip.style.display = DisplayStyle.None;
        }

        private void SelectFaction(int index) {
            _selectedFactionIndex = index;
            _btnCitizen?.EnableInClassList("faction-button--selected", index == 0);
            _btnPK?.EnableInClassList("faction-button--selected", index == 1);
        }

        private void BuildLoadingOverlay() {
            _loadingOverlay = new VisualElement();
            _loadingOverlay.style.position = Position.Absolute;
            _loadingOverlay.style.top = 0;
            _loadingOverlay.style.bottom = 0;
            _loadingOverlay.style.left = 0;
            _loadingOverlay.style.right = 0;
            _loadingOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.97f);
            _loadingOverlay.style.alignItems = Align.Center;
            _loadingOverlay.style.justifyContent = Justify.Center;
            _loadingOverlay.style.display = DisplayStyle.None;
            _loadingOverlay.pickingMode = PickingMode.Position;

            var title = new Label("VENERA FATUM");
            title.style.fontSize = 48;
            title.style.color = new Color(220f / 255f, 180f / 255f, 100f / 255f, 1f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 6;
            title.style.marginBottom = 24;

            _loadingDotsLabel = new Label(". . .");
            _loadingDotsLabel.style.fontSize = 28;
            _loadingDotsLabel.style.color = new Color(220f / 255f, 180f / 255f, 100f / 255f, 0.8f);
            _loadingDotsLabel.style.letterSpacing = 8;

            _loadingOverlay.Add(title);
            _loadingOverlay.Add(_loadingDotsLabel);
            _root.Add(_loadingOverlay);
        }

        private void ShowLoadingOverlay() {
            _isLoading = true;
            if (_loadingOverlay != null)
                _loadingOverlay.style.display = DisplayStyle.Flex;
        }

        private void HideLoadingOverlay() {
            _isLoading = false;
            if (_loadingOverlay != null)
                _loadingOverlay.style.display = DisplayStyle.None;
        }

        private IEnumerator AnimateLoadingDots() {
            string[] states = { ".", ". .", ". . ." };
            int i = 0;
            while (_isLoading) {
                if (_loadingDotsLabel != null)
                    _loadingDotsLabel.text = states[i++ % states.Length];
                yield return new WaitForSeconds(0.45f);
            }
        }

        private void OnConnectClicked() {
            UISoundPlayer.PlayClick();
            string playerName = _nameInput?.value?.Trim() ?? "";

            if (playerName.Length < 2) {
                if (_statusLabel != null)
                    _statusLabel.text = "Name must be at least 2 characters.";
                return;
            }

            LoginData.PlayerName = playerName;
            LoginData.ClassIndex = _selectedClassIndex;
            LoginData.FactionIndex = _selectedFactionIndex;
            LoginData.IsSet = true;

            _connectButton?.SetEnabled(false);
            _nameInput?.SetEnabled(false);

            StartCoroutine(ConnectWithLoadingScreen());
        }

        private IEnumerator ConnectWithLoadingScreen() {
            // 1. Show overlay and start dot animation
            ShowLoadingOverlay();
            StartCoroutine(AnimateLoadingDots());

            // 2. Animate visibly for ~0.5s BEFORE the FishNet stall
            //    (coroutines freeze during main-thread blocking; at least user sees it running first)
            yield return new WaitForSeconds(0.5f);

            // 3. Start FishNet (freeze occurs here, but overlay is already visible)
            var bootstrap = Object.FindFirstObjectByType<NetworkBootstrap>();
            if (bootstrap == null) {
                HideLoadingOverlay();
                ReEnableControls();
                if (_statusLabel != null) _statusLabel.text = "Error: NetworkBootstrap not found.";
                yield break;
            }

#if UNITY_EDITOR
            bool isClone = System.IO.File.Exists(
                System.IO.Path.Combine(Application.dataPath.Replace("/Assets", ""), ".clone"));
            if (isClone) {
                bootstrap.StartClientLocal();
            } else {
                // Server was pre-warmed in NetworkBootstrap.Start() — only connect the client now.
                bootstrap.StartClientLocal();
            }
#else
            bootstrap.StartClient();
#endif

            Debug.Log($"[LoginController] Connecting as '{LoginData.PlayerName}', class {LoginData.ClassIndex}");

            // 4. Wait for player spawn
            while (LostArkCamera.Instance == null || LostArkCamera.Instance.target == null)
                yield return null;

            // 5. Player is in the world — play enter SFX and fade out login music
            if (_enterWorldSFX != null)
                AudioManager.Instance?.PlaySFX(_enterWorldSFX);
            AudioManager.Instance?.StopMusic(2f);

            yield return new WaitForSeconds(2f);

            // 6. Hide everything
            HideLoadingOverlay();
            _root.style.display = DisplayStyle.None;
            IsActive = false;
            Debug.Log("[LoginController] Player spawned, hiding login screen.");
        }

        private void ReEnableControls() {
            _connectButton?.SetEnabled(true);
            _nameInput?.SetEnabled(true);
        }

        void OnDestroy() {
            IsActive = false;
            LoginData.LoginRequired = false;
        }
    }
}

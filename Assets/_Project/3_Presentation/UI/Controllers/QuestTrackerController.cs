using UnityEngine;
using UnityEngine.UIElements;
using Genesis.Core;
using Genesis.Data;
using Genesis.Simulation;
using FishNet.Object;

namespace Genesis.Presentation.UI {

    public class QuestTrackerController : MonoBehaviour {

        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _panel;
        private Label _questTitle;
        private VisualElement _objectivesContainer;

        private void Awake() {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable() {
            EventBus.Subscribe("OnQuestLogChanged", OnQuestLogChanged);
        }

        private void OnDisable() {
            EventBus.Unsubscribe("OnQuestLogChanged", OnQuestLogChanged);
        }

        private void Start() {
            InitializeUI();
        }

        private void InitializeUI() {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;

            _panel = root.Q<VisualElement>("QuestTrackerPanel");
            _questTitle = root.Q<Label>("QuestTitle");
            _objectivesContainer = root.Q<VisualElement>("ObjectivesContainer");

            if (_panel != null)
                _panel.style.display = DisplayStyle.None;
        }

        private void OnQuestLogChanged() {
            RefreshTracker();
        }

        private void RefreshTracker() {
            var questMgr = FindLocalQuestManager();
            if (questMgr == null || _panel == null) return;

            if (questMgr.QuestLog.Count == 0) {
                _panel.style.display = DisplayStyle.None;
                return;
            }

            // Show first active quest
            var active = questMgr.QuestLog[0];
            QuestData data = QuestDatabase.Instance?.GetQuest(active.QuestID);
            if (data == null) {
                _panel.style.display = DisplayStyle.None;
                return;
            }

            _panel.style.display = DisplayStyle.Flex;
            _questTitle.text = data.QuestName;

            _objectivesContainer.Clear();

            for (int i = 0; i < data.Objectives.Length; i++) {
                var obj = data.Objectives[i];
                int current = active.GetProgress(i);
                int required = obj.RequiredCount;
                bool done = current >= required;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom = 3;

                var check = new Label(done ? "\u2713" : "\u25CB");
                check.style.color = done ? new Color(0.4f, 1f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
                check.style.fontSize = 12;
                check.style.width = 18;
                check.style.unityFontStyleAndWeight = FontStyle.Bold;

                string progressText = required > 1 ? $" ({current}/{required})" : "";
                var desc = new Label($"{obj.Description}{progressText}");
                desc.style.color = done ? new Color(0.6f, 0.6f, 0.6f) : Color.white;
                desc.style.fontSize = 12;
                desc.style.whiteSpace = WhiteSpace.Normal;
                desc.style.flexShrink = 1;

                row.Add(check);
                row.Add(desc);
                _objectivesContainer.Add(row);
            }

            // Show ready-to-complete indicator
            if (active.State == QuestState.ObjectivesComplete) {
                var turnIn = new Label("Return to turn in quest");
                turnIn.style.color = new Color(1f, 0.85f, 0.3f);
                turnIn.style.fontSize = 11;
                turnIn.style.unityFontStyleAndWeight = FontStyle.Italic;
                turnIn.style.marginTop = 4;
                _objectivesContainer.Add(turnIn);
            }
        }

        private PlayerQuestManager FindLocalQuestManager() {
            var all = Object.FindObjectsByType<PlayerQuestManager>(FindObjectsSortMode.None);
            foreach (var mgr in all) {
                if (mgr.IsOwner) return mgr;
            }
            return null;
        }
    }
}

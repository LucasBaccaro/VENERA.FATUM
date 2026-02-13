using UnityEngine;
using UnityEngine.UIElements;
using Genesis.Core;
using Genesis.Data;
using Genesis.Simulation;
using Genesis.Items;
using FishNet.Object;
using System.Collections.Generic;

namespace Genesis.Presentation.UI {

    public class DialoguePanelController : MonoBehaviour {

        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _overlay;
        private VisualElement _window;
        private Label _npcNameLabel;
        private Label _dialogueText;
        private VisualElement _rewardsContainer;
        private VisualElement _rewardsList;
        private Button _acceptButton;
        private Button _completeButton;
        private Button _closeButton;

        private string _currentNpcId;
        private QuestData _pendingQuest;

        private void Awake() {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable() {
            EventBus.Subscribe<string, string>("OnNpcDialogueOpen", OnNpcDialogueOpen);
        }

        private void OnDisable() {
            EventBus.Unsubscribe<string, string>("OnNpcDialogueOpen", OnNpcDialogueOpen);
        }

        private void Start() {
            InitializeUI();
        }

        private void InitializeUI() {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;

            root.pickingMode = PickingMode.Ignore;
            _overlay = root.Q<VisualElement>("DialogueOverlay");
            _window = root.Q<VisualElement>("DialogueWindow");
            _npcNameLabel = root.Q<Label>("NpcNameLabel");
            _dialogueText = root.Q<Label>("DialogueText");
            _rewardsContainer = root.Q<VisualElement>("RewardsContainer");
            _rewardsList = root.Q<VisualElement>("RewardsList");
            _acceptButton = root.Q<Button>("AcceptButton");
            _completeButton = root.Q<Button>("CompleteButton");
            _closeButton = root.Q<Button>("CloseButton");

            if (_acceptButton != null) _acceptButton.clicked += OnAcceptClicked;
            if (_completeButton != null) _completeButton.clicked += OnCompleteClicked;
            if (_closeButton != null) _closeButton.clicked += OnCloseClicked;

            Hide();
        }

        private void OnNpcDialogueOpen(string npcId, string displayName) {
            _currentNpcId = npcId;

            var questMgr = FindLocalQuestManager();
            if (questMgr == null) return;

            _npcNameLabel.text = displayName;

            // Priority 1: Quest ready to turn in at this NPC
            QuestData turnInQuest = FindTurnInQuest(questMgr, npcId);
            if (turnInQuest != null) {
                ShowTurnIn(turnInQuest);
                return;
            }

            // Priority 2: New quest available from this NPC
            QuestData availableQuest = FindAvailableQuest(questMgr, npcId);
            if (availableQuest != null) {
                ShowOffer(availableQuest);
                return;
            }

            // Priority 3: Quest in progress
            QuestData inProgressQuest = FindInProgressQuest(questMgr, npcId);
            if (inProgressQuest != null) {
                ShowInProgress(inProgressQuest);
                return;
            }

            // Priority 4: Quest locked by level (informative)
            QuestData lockedQuest = FindLevelLockedQuest(questMgr, npcId);
            if (lockedQuest != null) {
                var attrs = FindLocalPlayerAttributes();
                int playerLevel = attrs != null ? attrs.Level : 1;
                ShowLevelLocked(lockedQuest, playerLevel);
                return;
            }

            // Priority 5: Idle dialogue
            ShowIdle(npcId);
        }

        private void ShowOffer(QuestData quest) {
            _pendingQuest = quest;
            _dialogueText.text = quest.DialogueOffer;
            ShowRewards(quest);

            _acceptButton.style.display = DisplayStyle.Flex;
            _completeButton.style.display = DisplayStyle.None;

            Show();
        }

        private void ShowTurnIn(QuestData quest) {
            _pendingQuest = quest;
            _dialogueText.text = quest.DialogueComplete;
            ShowRewards(quest);

            _acceptButton.style.display = DisplayStyle.None;
            _completeButton.style.display = DisplayStyle.Flex;

            Show();
        }

        private void ShowInProgress(QuestData quest) {
            _pendingQuest = null;
            _dialogueText.text = quest.DialogueInProgress;
            HideRewards();

            _acceptButton.style.display = DisplayStyle.None;
            _completeButton.style.display = DisplayStyle.None;

            Show();
        }

        private void ShowLevelLocked(QuestData quest, int playerLevel) {
            _pendingQuest = null;
            _dialogueText.text = $"I have a mission for you, but you're not ready yet.\n\n" +
                $"\"{quest.QuestName}\" requires Level {quest.RequiredLevel}.\n" +
                $"Your current level: {playerLevel}.\n\n" +
                $"Come back when you're stronger.";
            HideRewards();

            _acceptButton.style.display = DisplayStyle.None;
            _completeButton.style.display = DisplayStyle.None;

            Show();
        }

        private void ShowIdle(string npcId) {
            _pendingQuest = null;

            // Try to find NpcData for idle dialogue
            NpcData npcData = FindNpcData(npcId);
            _dialogueText.text = npcData != null && !string.IsNullOrEmpty(npcData.IdleDialogue)
                ? npcData.IdleDialogue
                : "...";
            HideRewards();

            _acceptButton.style.display = DisplayStyle.None;
            _completeButton.style.display = DisplayStyle.None;

            Show();
        }

        private void ShowRewards(QuestData quest) {
            if (_rewardsList == null || _rewardsContainer == null) return;

            _rewardsList.Clear();
            
            // Set rewards list to wrap for grid-like behavior
            _rewardsList.style.flexDirection = FlexDirection.Row;
            _rewardsList.style.flexWrap = Wrap.Wrap;
            
            bool hasRewards = false;

            foreach (var reward in quest.Rewards) {
                if (reward.ItemID <= 0) continue;
                hasRewards = true;

                var itemData = ItemDatabase.Instance?.GetItem(reward.ItemID);
                if (itemData == null) continue;

                // Create reward entry container (horizontal: icon + text)
                var rewardEntry = new VisualElement();
                rewardEntry.style.flexDirection = FlexDirection.Row;
                rewardEntry.style.alignItems = Align.Center;
                rewardEntry.style.width = 140; // Fixed width for uniform boxes
                rewardEntry.style.marginRight = 8;
                rewardEntry.style.marginBottom = 6;

                // Item icon
                var iconContainer = new VisualElement();
                iconContainer.style.width = 36;
                iconContainer.style.height = 36;
                iconContainer.style.marginRight = 6;
                iconContainer.style.flexShrink = 0; // Don't shrink icon
                iconContainer.style.backgroundImage = new StyleBackground(itemData.Icon);
                iconContainer.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 0.8f));
                iconContainer.style.borderTopLeftRadius = 4;
                iconContainer.style.borderTopRightRadius = 4;
                iconContainer.style.borderBottomLeftRadius = 4;
                iconContainer.style.borderBottomRightRadius = 4;

                // Add rarity border
                var rarityColor = GetRarityColor(reward.Rarity);
                iconContainer.style.borderLeftColor = rarityColor;
                iconContainer.style.borderRightColor = rarityColor;
                iconContainer.style.borderTopColor = rarityColor;
                iconContainer.style.borderBottomColor = rarityColor;
                iconContainer.style.borderLeftWidth = 2;
                iconContainer.style.borderRightWidth = 2;
                iconContainer.style.borderTopWidth = 2;
                iconContainer.style.borderBottomWidth = 2;

                // Add tooltip events
                iconContainer.RegisterCallback<MouseEnterEvent>(evt => {
                    if (ItemTooltipController.Instance != null) {
                        ItemTooltipController.Instance.Show(itemData, reward.Rarity);
                    }
                });
                iconContainer.RegisterCallback<MouseLeaveEvent>(evt => {
                    ItemTooltipController.Instance?.Hide();
                });

                // Quantity badge (if > 1)
                if (reward.ItemQuantity > 1) {
                    var qtyLabel = new Label($"x{reward.ItemQuantity}");
                    qtyLabel.style.fontSize = 9;
                    qtyLabel.style.color = Color.white;
                    qtyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    qtyLabel.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.9f));
                    qtyLabel.style.paddingLeft = 3;
                    qtyLabel.style.paddingRight = 3;
                    qtyLabel.style.paddingTop = 1;
                    qtyLabel.style.paddingBottom = 1;
                    qtyLabel.style.borderTopLeftRadius = 2;
                    qtyLabel.style.borderTopRightRadius = 2;
                    qtyLabel.style.borderBottomLeftRadius = 2;
                    qtyLabel.style.borderBottomRightRadius = 2;
                    qtyLabel.style.position = Position.Absolute;
                    qtyLabel.style.bottom = 1;
                    qtyLabel.style.right = 1;
                    
                    iconContainer.Add(qtyLabel);
                }

                // Item name label (fixed width for alignment)
                var nameLabel = new Label(itemData.ItemName);
                nameLabel.style.fontSize = 12;
                nameLabel.style.color = rarityColor;
                nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                nameLabel.style.width = 94; // Fixed width (140 - 36 icon - 6 margin - 4 padding)
                nameLabel.style.whiteSpace = WhiteSpace.Normal; // Allow wrapping
                nameLabel.style.overflow = Overflow.Hidden;
                nameLabel.style.textOverflow = TextOverflow.Ellipsis;

                rewardEntry.Add(iconContainer);
                rewardEntry.Add(nameLabel);
                _rewardsList.Add(rewardEntry);
            }

            _rewardsContainer.style.display = hasRewards ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void HideRewards() {
            if (_rewardsContainer != null)
                _rewardsContainer.style.display = DisplayStyle.None;
        }

        // ═══════════════════════════════════════════════════════
        // BUTTON HANDLERS
        // ═══════════════════════════════════════════════════════

        private void OnAcceptClicked() {
            if (_pendingQuest == null) return;

            var questMgr = FindLocalQuestManager();
            if (questMgr != null) {
                questMgr.CmdAcceptQuest(_pendingQuest.QuestID);
            }

            Hide();
        }

        private void OnCompleteClicked() {
            if (_pendingQuest == null) return;

            var questMgr = FindLocalQuestManager();
            if (questMgr != null) {
                questMgr.CmdTurnInQuest(_pendingQuest.QuestID);
            }

            Hide();
        }

        private void OnCloseClicked() {
            Hide();
        }

        // ═══════════════════════════════════════════════════════
        // SHOW / HIDE
        // ═══════════════════════════════════════════════════════

        private void Show() {
            if (_overlay != null) _overlay.style.display = DisplayStyle.Flex;
            if (_window != null) _window.style.display = DisplayStyle.Flex;
        }

        private void Hide() {
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            if (_window != null) _window.style.display = DisplayStyle.None;
            _pendingQuest = null;
        }

        // ═══════════════════════════════════════════════════════
        // QUEST LOOKUP
        // ═══════════════════════════════════════════════════════

        private QuestData FindTurnInQuest(PlayerQuestManager mgr, string npcId) {
            for (int i = 0; i < mgr.QuestLog.Count; i++) {
                var progress = mgr.QuestLog[i];
                if (progress.State != QuestState.ObjectivesComplete) continue;

                QuestData data = QuestDatabase.Instance?.GetQuest(progress.QuestID);
                if (data != null && data.TurnInNpcID == npcId)
                    return data;
            }
            return null;
        }

        private QuestData FindAvailableQuest(PlayerQuestManager mgr, string npcId) {
            List<QuestData> npcQuests = QuestDatabase.Instance?.GetQuestsForNpc(npcId);
            if (npcQuests == null) return null;

            var attrs = FindLocalPlayerAttributes();
            int playerLevel = attrs != null ? attrs.Level : 1;

            foreach (var quest in npcQuests) {
                if (mgr.IsQuestActive(quest.QuestID) || mgr.IsQuestCompleted(quest.QuestID))
                    continue;
                if (playerLevel < quest.RequiredLevel) continue;
                if (quest.PrerequisiteQuest != null && !mgr.IsQuestCompleted(quest.PrerequisiteQuest.QuestID))
                    continue;

                return quest;
            }
            return null;
        }

        private QuestData FindInProgressQuest(PlayerQuestManager mgr, string npcId) {
            for (int i = 0; i < mgr.QuestLog.Count; i++) {
                var progress = mgr.QuestLog[i];
                QuestData data = QuestDatabase.Instance?.GetQuest(progress.QuestID);
                if (data != null && (data.GiverNpcID == npcId || data.TurnInNpcID == npcId))
                    return data;
            }
            return null;
        }

        private QuestData FindLevelLockedQuest(PlayerQuestManager mgr, string npcId) {
            List<QuestData> npcQuests = QuestDatabase.Instance?.GetQuestsForNpc(npcId);
            if (npcQuests == null) return null;

            var attrs = FindLocalPlayerAttributes();
            int playerLevel = attrs != null ? attrs.Level : 1;

            foreach (var quest in npcQuests) {
                if (mgr.IsQuestActive(quest.QuestID) || mgr.IsQuestCompleted(quest.QuestID))
                    continue;
                if (quest.PrerequisiteQuest != null && !mgr.IsQuestCompleted(quest.PrerequisiteQuest.QuestID))
                    continue;
                // Prerequisite met but level too low
                if (playerLevel < quest.RequiredLevel)
                    return quest;
            }
            return null;
        }

        private NpcData FindNpcData(string npcId) {
            var npcControllers = Object.FindObjectsByType<NpcController>(FindObjectsSortMode.None);
            foreach (var npc in npcControllers) {
                if (npc.NpcID == npcId) {
                    return npc.NpcData;
                }
            }
            return null;
        }

        // ═══════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════

        private PlayerQuestManager FindLocalQuestManager() {
            var all = Object.FindObjectsByType<PlayerQuestManager>(FindObjectsSortMode.None);
            foreach (var mgr in all) {
                if (mgr.IsOwner) return mgr;
            }
            return null;
        }

        private PlayerAttributes FindLocalPlayerAttributes() {
            var all = Object.FindObjectsByType<PlayerAttributes>(FindObjectsSortMode.None);
            foreach (var attrs in all) {
                if (attrs.IsOwner) return attrs;
            }
            return null;
        }

        private Color GetRarityColor(ItemRarity rarity) {
            return rarity switch {
                ItemRarity.Uncommon => new Color(0.3f, 1f, 0.3f),
                ItemRarity.Rare => new Color(0.3f, 0.5f, 1f),
                ItemRarity.Epic => new Color(0.7f, 0.3f, 1f),
                _ => Color.white
            };
        }
    }
}

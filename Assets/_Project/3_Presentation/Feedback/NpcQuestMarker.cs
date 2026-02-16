using UnityEngine;
using TMPro;
using Genesis.Data;
using Genesis.Simulation;
using System.Collections.Generic;

namespace Genesis.Presentation.Feedback {

    /// <summary>
    /// Displays MMO-style quest markers (! and ?) above NPC heads.
    /// Client-side only — each player sees markers based on their own quest state.
    /// Polls quest state periodically to avoid EventBus coupling issues.
    /// </summary>
    public class NpcQuestMarker : MonoBehaviour {

        [Header("Marker Settings")]
        [SerializeField] private Vector3 _offset = new Vector3(0, 2.5f, 0);
        [SerializeField] private float _fontSize = 8f;
        [SerializeField] private float _bobSpeed = 2f;
        [SerializeField] private float _bobAmount = 0.15f;

        private TextMeshPro _textMesh;
        private Transform _markerTransform;
        private NpcController _npcController;
        private string _npcId;

        private PlayerQuestManager _cachedQuestMgr;
        private PlayerAttributes _cachedPlayerAttrs;

        private enum MarkerState { None, Available, InProgress, TurnIn }
        private MarkerState _currentState = MarkerState.None;

        private float _refreshTimer;
        private const float RefreshInterval = 0.5f;

        private static readonly Color ColorYellow = new Color(1f, 0.9f, 0.2f);
        private static readonly Color ColorGreen = new Color(0.2f, 1f, 0.4f);
        private static readonly Color ColorGray = new Color(0.6f, 0.6f, 0.6f);

        private void Awake() {
            _npcController = GetComponent<NpcController>();
            if (_npcController == null) {
                enabled = false;
                return;
            }

            CreateMarker();
        }

        private void Start() {
            _npcId = _npcController.NpcID;
        }

        private void CreateMarker() {
            var markerGO = new GameObject("QuestMarker");
            _markerTransform = markerGO.transform;
            _markerTransform.SetParent(transform, false);
            _markerTransform.localPosition = _offset;

            _textMesh = markerGO.AddComponent<TextMeshPro>();
            _textMesh.alignment = TextAlignmentOptions.Center;
            _textMesh.fontSize = _fontSize;
            _textMesh.fontStyle = FontStyles.Bold;
            _textMesh.enableWordWrapping = false;
            _textMesh.text = "";
            _textMesh.enabled = false;

            var renderer = _textMesh.GetComponent<MeshRenderer>();
            if (renderer != null) {
                renderer.sortingOrder = 100;
            }
        }

        private void LateUpdate() {
            // Try to cache local player references
            if (_cachedQuestMgr == null) {
                TryCacheLocalPlayer();
            }

            // Periodic refresh
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f) {
                _refreshTimer = RefreshInterval;
                Refresh();
            }

            // Billboard + bobbing (only when visible)
            if (_markerTransform == null || !_textMesh.enabled) return;

            var cam = Camera.main;
            if (cam != null) {
                _markerTransform.rotation = cam.transform.rotation;
            }

            float bob = Mathf.Sin(Time.time * _bobSpeed) * _bobAmount;
            _markerTransform.localPosition = _offset + new Vector3(0, bob, 0);
        }

        private void TryCacheLocalPlayer() {
            var allQm = Object.FindObjectsByType<PlayerQuestManager>(FindObjectsSortMode.None);
            foreach (var mgr in allQm) {
                if (mgr.IsOwner) {
                    _cachedQuestMgr = mgr;
                    _cachedPlayerAttrs = mgr.GetComponent<PlayerAttributes>();
                    return;
                }
            }
        }

        private void Refresh() {
            if (_cachedQuestMgr == null || string.IsNullOrEmpty(_npcId)) {
                ApplyState(MarkerState.None);
                return;
            }

            // Validate cached reference is still alive
            if (_cachedQuestMgr.gameObject == null) {
                _cachedQuestMgr = null;
                _cachedPlayerAttrs = null;
                ApplyState(MarkerState.None);
                return;
            }

            // Priority: TurnIn > Available > InProgress
            if (HasTurnInQuest()) {
                ApplyState(MarkerState.TurnIn);
            } else if (HasAvailableQuest()) {
                ApplyState(MarkerState.Available);
            } else if (HasInProgressQuest()) {
                ApplyState(MarkerState.InProgress);
            } else {
                ApplyState(MarkerState.None);
            }
        }

        private void ApplyState(MarkerState state) {
            if (_currentState == state) return;
            _currentState = state;

            if (state == MarkerState.None) {
                _textMesh.enabled = false;
                return;
            }

            switch (state) {
                case MarkerState.Available:
                    _textMesh.text = "!";
                    _textMesh.color = ColorYellow;
                    break;
                case MarkerState.TurnIn:
                    _textMesh.text = "?";
                    _textMesh.color = ColorGreen;
                    break;
                case MarkerState.InProgress:
                    _textMesh.text = "?";
                    _textMesh.color = ColorGray;
                    break;
            }

            _textMesh.enabled = true;
        }

        // ═══════════════════════════════════════════════════════
        // QUEST CHECKS (mirrors DialoguePanelController logic)
        // ═══════════════════════════════════════════════════════

        private bool HasTurnInQuest() {
            var questLog = _cachedQuestMgr.QuestLog;
            for (int i = 0; i < questLog.Count; i++) {
                var progress = questLog[i];
                if (progress.State != QuestState.ObjectivesComplete) continue;

                QuestData data = QuestDatabase.Instance?.GetQuest(progress.QuestID);
                if (data != null && data.TurnInNpcID == _npcId)
                    return true;
            }
            return false;
        }

        private bool HasAvailableQuest() {
            List<QuestData> npcQuests = QuestDatabase.Instance?.GetQuestsForNpc(_npcId);
            if (npcQuests == null || npcQuests.Count == 0) return false;

            int playerLevel = _cachedPlayerAttrs != null ? _cachedPlayerAttrs.Level : 1;

            foreach (var quest in npcQuests) {
                if (_cachedQuestMgr.IsQuestActive(quest.QuestID) || _cachedQuestMgr.IsQuestCompleted(quest.QuestID))
                    continue;
                if (playerLevel < quest.RequiredLevel) continue;
                if (quest.PrerequisiteQuest != null && !_cachedQuestMgr.IsQuestCompleted(quest.PrerequisiteQuest.QuestID))
                    continue;

                return true;
            }
            return false;
        }

        private bool HasInProgressQuest() {
            var questLog = _cachedQuestMgr.QuestLog;
            for (int i = 0; i < questLog.Count; i++) {
                var progress = questLog[i];
                QuestData data = QuestDatabase.Instance?.GetQuest(progress.QuestID);
                if (data != null && (data.GiverNpcID == _npcId || data.TurnInNpcID == _npcId))
                    return true;
            }
            return false;
        }
    }
}

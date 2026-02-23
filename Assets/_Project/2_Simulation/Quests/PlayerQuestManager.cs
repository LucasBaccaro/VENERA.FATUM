using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using Genesis.Core;
using Genesis.Core.Persistence;
using Genesis.Data;
using Genesis.Items;
using System;

namespace Genesis.Simulation {

    [Serializable]
    public struct QuestProgress {
        public string QuestID;
        public QuestState State;
        public int Progress0;
        public int Progress1;
        public int Progress2;

        public int GetProgress(int index) {
            switch (index) {
                case 0: return Progress0;
                case 1: return Progress1;
                case 2: return Progress2;
                default: return 0;
            }
        }

        public void SetProgress(int index, int value) {
            switch (index) {
                case 0: Progress0 = value; break;
                case 1: Progress1 = value; break;
                case 2: Progress2 = value; break;
            }
        }
    }

    [RequireComponent(typeof(NetworkObject))]
    public class PlayerQuestManager : NetworkBehaviour {

        private readonly SyncList<QuestProgress> _questLog = new SyncList<QuestProgress>();
        private readonly SyncList<string> _completedQuests = new SyncList<string>();

        public SyncList<QuestProgress> QuestLog => _questLog;
        public SyncList<string> CompletedQuests => _completedQuests;

        public override void OnStartNetwork() {
            base.OnStartNetwork();
            _questLog.OnChange += OnQuestLogChanged;
            _completedQuests.OnChange += OnCompletedQuestsChanged;
        }

        public override void OnStartServer() {
            base.OnStartServer();
            EventBus.Subscribe<NetworkObject, string>("OnEnemyMobKilled", OnEnemyMobKilled);
        }

        public override void OnStopServer() {
            base.OnStopServer();
            EventBus.Unsubscribe<NetworkObject, string>("OnEnemyMobKilled", OnEnemyMobKilled);
        }

        // ═══════════════════════════════════════════════════════
        // SERVER LOGIC
        // ═══════════════════════════════════════════════════════

        [Server]
        public bool AcceptQuest(string questId) {
            if (IsQuestActive(questId) || IsQuestCompleted(questId)) return false;

            QuestData data = QuestDatabase.Instance?.GetQuest(questId);
            if (data == null) return false;

            // Check level requirement
            var attrs = GetComponent<PlayerAttributes>();
            if (attrs != null && attrs.Level < data.RequiredLevel) return false;

            // Check prerequisite
            if (data.PrerequisiteQuest != null && !IsQuestCompleted(data.PrerequisiteQuest.QuestID))
                return false;

            var progress = new QuestProgress {
                QuestID = questId,
                State = QuestState.Active,
                Progress0 = 0,
                Progress1 = 0,
                Progress2 = 0
            };

            _questLog.Add(progress);

            TargetNotifyQuestAccepted(base.Owner, data.QuestName);
            Debug.Log($"[PlayerQuestManager] {gameObject.name} accepted quest: {data.QuestName}");
            return true;
        }

        [Server]
        public void AdvanceObjective(string questId, int objectiveIndex, int amount = 1) {
            int idx = FindQuestIndex(questId);
            if (idx < 0) return;

            var progress = _questLog[idx];
            if (progress.State != QuestState.Active) return;

            QuestData data = QuestDatabase.Instance?.GetQuest(questId);
            if (data == null || objectiveIndex >= data.Objectives.Length) return;

            int current = progress.GetProgress(objectiveIndex);
            int required = data.Objectives[objectiveIndex].RequiredCount;
            int newVal = Mathf.Min(current + amount, required);
            progress.SetProgress(objectiveIndex, newVal);

            // Check if all objectives are complete
            bool allDone = true;
            for (int i = 0; i < data.Objectives.Length; i++) {
                if (progress.GetProgress(i) < data.Objectives[i].RequiredCount) {
                    allDone = false;
                    break;
                }
            }

            if (allDone) {
                progress.State = QuestState.ObjectivesComplete;
            }

            _questLog[idx] = progress;

            TargetNotifyObjectiveProgress(base.Owner, data.QuestName,
                data.Objectives[objectiveIndex].Description, newVal, required);
        }

        [Server]
        public void CompleteQuest(string questId) {
            int idx = FindQuestIndex(questId);
            if (idx < 0) return;

            var progress = _questLog[idx];
            if (progress.State != QuestState.ObjectivesComplete) return;

            QuestData data = QuestDatabase.Instance?.GetQuest(questId);
            if (data == null) return;

            // Grant rewards (resolve class-appropriate items)
            var inventory = GetComponent<PlayerInventory>();
            if (inventory != null) {
                string playerClass = GetComponent<PlayerClassManager>()?.GetCurrentClassName() ?? "";
                foreach (var reward in data.Rewards) {
                    if (reward.ItemID > 0) {
                        int resolvedID = ResolveRewardItemID(reward.ItemID, playerClass);
                        inventory.AddItem(resolvedID, reward.ItemQuantity, reward.Tier, reward.Rarity);
                    }
                }
            }

            // XP reward via EventBus (XPRewardSystem listens)
            string xpType = "standard";
            if (data.Rewards.Length > 0 && !string.IsNullOrEmpty(data.Rewards[0].XPType)) {
                xpType = data.Rewards[0].XPType;
            }
            EventBus.Trigger("OnQuestCompleted", base.NetworkObject, xpType);

            // Move to completed
            _questLog.RemoveAt(idx);
            _completedQuests.Add(questId);

            TargetNotifyQuestCompleted(base.Owner, data.QuestName);

            // Instant save after quest completion
            if (ServiceLocator.Instance.TryGet<IPersistenceService>(out var persistence)) {
                _ = persistence.SavePlayerNow(base.NetworkObject);
            }

            Debug.Log($"[PlayerQuestManager] {gameObject.name} completed quest: {data.QuestName}");
        }

        // ═══════════════════════════════════════════════════════
        // EVENT HANDLERS (Server)
        // ═══════════════════════════════════════════════════════

        private void OnEnemyMobKilled(NetworkObject killer, string enemyTag) {
            if (killer == null || killer != base.NetworkObject) return;

            for (int q = 0; q < _questLog.Count; q++) {
                var progress = _questLog[q];
                if (progress.State != QuestState.Active) continue;

                QuestData data = QuestDatabase.Instance?.GetQuest(progress.QuestID);
                if (data == null) continue;

                for (int o = 0; o < data.Objectives.Length; o++) {
                    if (data.Objectives[o].Type == QuestObjectiveType.Kill &&
                        data.Objectives[o].TargetID == enemyTag) {
                        AdvanceObjective(progress.QuestID, o, 1);
                        break;
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // SERVER RPCs (Client → Server)
        // ═══════════════════════════════════════════════════════

        [ServerRpc]
        public void CmdAcceptQuest(string questId) {
            AcceptQuest(questId);
        }

        [ServerRpc]
        public void CmdTurnInQuest(string questId) {
            CompleteQuest(questId);
        }

        // ═══════════════════════════════════════════════════════
        // TARGET RPCs (Server → Client)
        // ═══════════════════════════════════════════════════════

        [TargetRpc]
        private void TargetNotifyQuestAccepted(NetworkConnection conn, string questName) {
            EventBus.Trigger("OnQuestAccepted", questName);
            Debug.Log($"[PlayerQuestManager] Quest accepted: {questName}");
        }

        [TargetRpc]
        private void TargetNotifyObjectiveProgress(NetworkConnection conn, string questName,
            string objectiveDesc, int current, int required) {
            EventBus.Trigger("OnQuestObjectiveProgress", questName, objectiveDesc, current, required);
        }

        [TargetRpc]
        private void TargetNotifyQuestCompleted(NetworkConnection conn, string questName) {
            EventBus.Trigger("OnQuestTurnedIn", questName);
            Debug.Log($"[PlayerQuestManager] Quest completed: {questName}");
        }

        // ═══════════════════════════════════════════════════════
        // SYNC CALLBACKS
        // ═══════════════════════════════════════════════════════

        private void OnQuestLogChanged(SyncListOperation op, int index,
            QuestProgress oldItem, QuestProgress newItem, bool asServer) {
            if (base.IsOwner) {
                EventBus.Trigger("OnQuestLogChanged");
            }
        }

        private void OnCompletedQuestsChanged(SyncListOperation op, int index,
            string oldItem, string newItem, bool asServer) {
            if (base.IsOwner) {
                EventBus.Trigger("OnQuestLogChanged");
            }
        }

        // ═══════════════════════════════════════════════════════
        // PERSISTENCE HYDRATION
        // ═══════════════════════════════════════════════════════

        [Server]
        public void HydrateFromSave(SerializedQuestProgress[] quests, string[] completed) {
            _questLog.Clear();
            if (quests != null) {
                foreach (var sq in quests) {
                    _questLog.Add(new QuestProgress {
                        QuestID = sq.questId,
                        State = (QuestState)sq.state,
                        Progress0 = sq.progress0,
                        Progress1 = sq.progress1,
                        Progress2 = sq.progress2
                    });
                }
            }
            _completedQuests.Clear();
            if (completed != null) {
                foreach (var id in completed) {
                    _completedQuests.Add(id);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // QUERIES
        // ═══════════════════════════════════════════════════════

        public bool IsQuestActive(string questId) {
            for (int i = 0; i < _questLog.Count; i++) {
                if (_questLog[i].QuestID == questId) return true;
            }
            return false;
        }

        public bool IsQuestCompleted(string questId) {
            for (int i = 0; i < _completedQuests.Count; i++) {
                if (_completedQuests[i] == questId) return true;
            }
            return false;
        }

        public QuestProgress? GetQuestProgress(string questId) {
            for (int i = 0; i < _questLog.Count; i++) {
                if (_questLog[i].QuestID == questId) return _questLog[i];
            }
            return null;
        }

        public QuestState GetQuestState(string questId) {
            if (IsQuestCompleted(questId)) return QuestState.Completed;
            for (int i = 0; i < _questLog.Count; i++) {
                if (_questLog[i].QuestID == questId) return _questLog[i].State;
            }
            return QuestState.NotStarted;
        }

        private int FindQuestIndex(string questId) {
            for (int i = 0; i < _questLog.Count; i++) {
                if (_questLog[i].QuestID == questId) return i;
            }
            return -1;
        }

        // ═══════════════════════════════════════════════════════
        // REWARD RESOLUTION
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Resolves a reward item ID to the class-appropriate equivalent.
        /// If the original item is equipment for a different class, finds the
        /// equivalent item (same slot) for the player's class.
        /// </summary>
        private int ResolveRewardItemID(int originalID, string playerClass) {
            if (string.IsNullOrEmpty(playerClass)) return originalID;

            var equipment = ItemDatabase.Instance?.GetEquipment(originalID);
            if (equipment == null) return originalID; // Not equipment, keep original
            if (string.IsNullOrEmpty(equipment.RequiredClass)) return originalID; // No class restriction
            if (equipment.RequiredClass == playerClass) return originalID; // Already matches

            // Find equivalent for player's class
            var equivalent = ItemDatabase.Instance.FindEquipmentBySlotAndClass(equipment.Slot, playerClass);
            if (equivalent != null) {
                Debug.Log($"[PlayerQuestManager] Resolved reward {originalID} ({equipment.RequiredClass}) → {equivalent.ItemID} ({playerClass})");
                return equivalent.ItemID;
            }

            return originalID; // Fallback to original
        }

        // ═══════════════════════════════════════════════════════
        // COLLECT OBJECTIVE HELPER
        // ═══════════════════════════════════════════════════════

        [Server]
        public void NotifyItemCollected(string itemTag) {
            for (int q = 0; q < _questLog.Count; q++) {
                var progress = _questLog[q];
                if (progress.State != QuestState.Active) continue;

                QuestData data = QuestDatabase.Instance?.GetQuest(progress.QuestID);
                if (data == null) continue;

                for (int o = 0; o < data.Objectives.Length; o++) {
                    if (data.Objectives[o].Type == QuestObjectiveType.Collect &&
                        data.Objectives[o].TargetID == itemTag) {
                        AdvanceObjective(progress.QuestID, o, 1);
                        break;
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // TALK OBJECTIVE HELPER
        // ═══════════════════════════════════════════════════════

        [Server]
        public void NotifyTalkedToNpc(string npcId) {
            for (int q = 0; q < _questLog.Count; q++) {
                var progress = _questLog[q];
                if (progress.State != QuestState.Active) continue;

                QuestData data = QuestDatabase.Instance?.GetQuest(progress.QuestID);
                if (data == null) continue;

                for (int o = 0; o < data.Objectives.Length; o++) {
                    if (data.Objectives[o].Type == QuestObjectiveType.TalkToNPC &&
                        data.Objectives[o].TargetID == npcId) {
                        AdvanceObjective(progress.QuestID, o, data.Objectives[o].RequiredCount);
                        break;
                    }
                }
            }
        }
    }
}

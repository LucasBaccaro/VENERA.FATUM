using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Data {

    [CreateAssetMenu(fileName = "QuestDatabase", menuName = "VENERA.FATUM/Databases/Quest Database", order = 1)]
    public class QuestDatabase : ScriptableObject {

        private static QuestDatabase _instance;

        public static QuestDatabase Instance {
            get {
                if (_instance == null) {
                    _instance = Resources.Load<QuestDatabase>("Databases/QuestDatabase");
                    if (_instance == null) {
                        Debug.LogError("[QuestDatabase] No QuestDatabase found in Resources/Databases folder!");
                    }
                }
                return _instance;
            }
        }

        [Header("Quest Registry")]
        [SerializeField] private List<QuestData> _allQuests = new List<QuestData>();

        private Dictionary<string, QuestData> _questDictionary;
        private Dictionary<string, List<QuestData>> _questsByNpc;

        private void OnEnable() {
            BuildDictionaries();
        }

        private void BuildDictionaries() {
            _questDictionary = new Dictionary<string, QuestData>();
            _questsByNpc = new Dictionary<string, List<QuestData>>();

            foreach (var quest in _allQuests) {
                if (quest == null) continue;

                if (string.IsNullOrEmpty(quest.QuestID)) {
                    Debug.LogWarning($"[QuestDatabase] Quest '{quest.name}' has empty QuestID, skipping.");
                    continue;
                }

                if (_questDictionary.ContainsKey(quest.QuestID)) {
                    Debug.LogError($"[QuestDatabase] Duplicate QuestID '{quest.QuestID}'!");
                    continue;
                }

                _questDictionary[quest.QuestID] = quest;

                // Index by giver NPC
                if (!string.IsNullOrEmpty(quest.GiverNpcID)) {
                    if (!_questsByNpc.ContainsKey(quest.GiverNpcID))
                        _questsByNpc[quest.GiverNpcID] = new List<QuestData>();
                    _questsByNpc[quest.GiverNpcID].Add(quest);
                }
            }

            Debug.Log($"[QuestDatabase] Loaded {_questDictionary.Count} quests.");
        }

        public QuestData GetQuest(string questId) {
            if (_questDictionary == null || _questDictionary.Count == 0)
                BuildDictionaries();

            if (_questDictionary.TryGetValue(questId, out QuestData quest))
                return quest;

            Debug.LogWarning($"[QuestDatabase] Quest '{questId}' not found!");
            return null;
        }

        public List<QuestData> GetQuestsForNpc(string npcId) {
            if (_questsByNpc == null || _questsByNpc.Count == 0)
                BuildDictionaries();

            if (_questsByNpc.TryGetValue(npcId, out List<QuestData> quests))
                return quests;

            return new List<QuestData>();
        }

        public List<QuestData> GetAllQuests() {
            return _allQuests;
        }

#if UNITY_EDITOR
        [ContextMenu("Auto-Find All Quests")]
        private void AutoFindAllQuests() {
            _allQuests.Clear();

            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:QuestData");
            foreach (string guid in guids) {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                QuestData quest = UnityEditor.AssetDatabase.LoadAssetAtPath<QuestData>(path);
                if (quest != null) _allQuests.Add(quest);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[QuestDatabase] Auto-found {_allQuests.Count} quests.");
            BuildDictionaries();
        }
#endif
    }
}

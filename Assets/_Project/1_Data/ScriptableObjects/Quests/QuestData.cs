using UnityEngine;

namespace Genesis.Data {

    [CreateAssetMenu(fileName = "NewQuest", menuName = "VENERA.FATUM/Quests/Quest Data", order = 0)]
    public class QuestData : ScriptableObject {

        [Header("Identity")]
        public string QuestID;
        public string QuestName;
        [TextArea] public string Description;

        [Header("Requirements")]
        public int RequiredLevel = 1;
        public QuestData PrerequisiteQuest;

        [Header("NPCs")]
        public string GiverNpcID;
        public string TurnInNpcID;

        [Header("Dialogue")]
        [TextArea] public string DialogueOffer;
        [TextArea] public string DialogueInProgress;
        [TextArea] public string DialogueComplete;
        [TextArea] public string DialogueAfterComplete;

        [Header("Objectives")]
        public QuestObjective[] Objectives;

        [Header("Rewards")]
        public QuestReward[] Rewards;
    }
}

using System;

namespace Genesis.Data {

    public enum QuestState {
        NotStarted,
        Active,
        ObjectivesComplete,
        Completed
    }

    public enum QuestObjectiveType {
        TalkToNPC,
        Kill,
        Collect
    }

    [Serializable]
    public struct QuestObjective {
        public QuestObjectiveType Type;
        public string Description;
        public string TargetID;
        public int RequiredCount;
    }

    [Serializable]
    public struct QuestReward {
        public int ItemID;
        public int ItemQuantity;
        public Genesis.Items.ItemTier Tier;
        public Genesis.Items.ItemRarity Rarity;
        [UnityEngine.Tooltip("simple, standard, or epic — maps to XPRewardConfig")]
        public string XPType;
    }
}

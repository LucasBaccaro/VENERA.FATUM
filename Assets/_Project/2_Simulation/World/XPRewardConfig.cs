using UnityEngine;

namespace Genesis.Simulation.World {

    /// <summary>
    /// Configuration for XP rewards from kills, quests, etc.
    /// </summary>
    [CreateAssetMenu(fileName = "XPRewardConfig", menuName = "Genesis/System/XP Reward Config")]
    public class XPRewardConfig : ScriptableObject {
        [Header("Kill XP")]
        public float BaseEnemyKillXP = 50f;
        public float EliteEnemyKillXP = 150f;
        public float BossKillXP = 500f;
        public float PlayerKillXP = 100f;

        [Header("Quest XP")]
        public float SimpleQuestXP = 200f;
        public float StandardQuestXP = 500f;
        public float EpicQuestXP = 1000f;
    }
}

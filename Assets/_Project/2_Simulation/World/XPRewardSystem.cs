using UnityEngine;
using FishNet.Object;
using Genesis.Core;
using Genesis.Data;

namespace Genesis.Simulation.World {

    /// <summary>
    /// Server-side system that grants XP to players based on game events.
    /// Listens to EventBus for kills, quests, etc.
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

namespace Genesis.Simulation {

    /// <summary>
    /// MonoBehaviour server component that listens for events and distributes XP.
    /// Attach to a persistent manager GameObject in the Bootstrap scene.
    /// </summary>
    public class XPRewardSystem : MonoBehaviour {

        [SerializeField] private Genesis.Simulation.World.XPRewardConfig _config;

        private void OnEnable() {
            EventBus.Subscribe<NetworkObject, NetworkObject>("OnEnemyKilled", OnEnemyKilled);
            EventBus.Subscribe<NetworkObject, string>("OnQuestCompleted", OnQuestCompleted);
        }

        private void OnDisable() {
            EventBus.Unsubscribe<NetworkObject, NetworkObject>("OnEnemyKilled", OnEnemyKilled);
            EventBus.Unsubscribe<NetworkObject, string>("OnQuestCompleted", OnQuestCompleted);
        }

        private void OnEnemyKilled(NetworkObject killer, NetworkObject enemy) {
            if (killer == null || _config == null) return;

            PlayerAttributes attrs = killer.GetComponent<PlayerAttributes>();
            if (attrs == null) return;

            float xpAmount = _config.BaseEnemyKillXP;
            attrs.GainXP(xpAmount);

            Debug.Log($"[XPRewardSystem] {killer.name} gained {xpAmount} XP for killing {enemy?.name ?? "unknown"}");
        }

        private void OnQuestCompleted(NetworkObject player, string questType) {
            if (player == null || _config == null) return;

            PlayerAttributes attrs = player.GetComponent<PlayerAttributes>();
            if (attrs == null) return;

            float xpAmount = questType switch {
                "simple" => _config.SimpleQuestXP,
                "standard" => _config.StandardQuestXP,
                "epic" => _config.EpicQuestXP,
                _ => _config.SimpleQuestXP
            };

            attrs.GainXP(xpAmount);
            Debug.Log($"[XPRewardSystem] {player.name} gained {xpAmount} XP for completing {questType} quest");
        }
    }
}

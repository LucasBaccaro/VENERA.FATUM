using UnityEngine;
using FishNet.Object;
using Genesis.Core;
using Genesis.Data;
using Genesis.Simulation.World;

namespace Genesis.Simulation {

    /// <summary>
    /// MonoBehaviour server component that listens for events and distributes XP.
    /// Attach to a persistent manager GameObject in the Bootstrap scene.
    /// </summary>
    public class XPRewardSystem : MonoBehaviour {

        [SerializeField] private XPRewardConfig _config;

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

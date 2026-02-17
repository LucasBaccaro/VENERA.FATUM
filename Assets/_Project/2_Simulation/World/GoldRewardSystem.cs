using UnityEngine;
using FishNet.Object;
using Genesis.Core;

namespace Genesis.Simulation {

    /// <summary>
    /// MonoBehaviour server component that listens for enemy kills and distributes gold.
    /// Attach to a persistent manager GameObject in the Bootstrap scene.
    /// </summary>
    public class GoldRewardSystem : MonoBehaviour {

        private void OnEnable() {
            EventBus.Subscribe<NetworkObject, NetworkObject>("OnEnemyKilled", OnEnemyKilled);
        }

        private void OnDisable() {
            EventBus.Unsubscribe<NetworkObject, NetworkObject>("OnEnemyKilled", OnEnemyKilled);
        }

        private void OnEnemyKilled(NetworkObject killer, NetworkObject enemy) {
            if (killer == null || enemy == null) return;

            PlayerAttributes attrs = killer.GetComponent<PlayerAttributes>();
            if (attrs == null) return;

            EnemyMob mob = enemy.GetComponent<EnemyMob>();
            if (mob == null) return;

            int goldAmount = mob.GoldReward;
            if (goldAmount <= 0) return;

            attrs.GainGold(goldAmount);

            Debug.Log($"[GoldRewardSystem] {killer.name} gained {goldAmount} gold for killing {enemy.name}");
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using FishNet;
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

            Vector3 enemyPos = enemy != null ? enemy.transform.position : killer.transform.position;

            // Party XP sharing
            if (ServiceLocator.Instance.TryGet<PartyManager>(out var partyManager)) {
                var party = partyManager.GetParty(killer.OwnerId);
                if (party != null && party.MemberClientIds.Count > 1) {
                    var qualifying = new List<PlayerAttributes>();
                    foreach (int memberId in party.MemberClientIds) {
                        if (InstanceFinder.NetworkManager.ServerManager.Clients.TryGetValue(memberId, out var conn)
                            && conn.FirstObject != null) {
                            float dist = Vector3.Distance(conn.FirstObject.transform.position, enemyPos);
                            if (dist <= 30f) {
                                var memberAttrs = conn.FirstObject.GetComponent<PlayerAttributes>();
                                if (memberAttrs != null) qualifying.Add(memberAttrs);
                            }
                        }
                    }

                    if (qualifying.Count > 0) {
                        float totalXP = _config.BaseEnemyKillXP * (1f + 0.05f * (qualifying.Count - 1));
                        float xpPerMember = totalXP / qualifying.Count;
                        foreach (var memberAttrs in qualifying) {
                            memberAttrs.GainXP(xpPerMember);
                        }
                        Debug.Log($"[XPRewardSystem] Party XP: {qualifying.Count} members each got {xpPerMember:F2} XP");
                        return;
                    }
                }
            }

            // Solo player
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

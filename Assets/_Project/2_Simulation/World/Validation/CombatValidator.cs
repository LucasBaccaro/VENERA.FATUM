using FishNet.Object;
using UnityEngine;

namespace Genesis.Simulation.World
{
    public static class CombatValidator
    {
        [Server]
        public static bool CanApplyDamage(NetworkObject victim, NetworkObject attacker, out string reason)
        {
            reason = null;

            if (victim == null)
            {
                reason = "Invalid victim";
                Debug.LogWarning($"[CombatValidator] ❌ {reason}");
                return false;
            }

            PlayerState victimState = victim.GetComponent<PlayerState>();
            if (victimState != null)
            {
                Debug.Log($"[CombatValidator] Victim {victim.name} safe zone state: {victimState.IsInSafeZone}");

                if (victimState.IsInSafeZone)
                {
                    reason = "Target is in a safe zone";
                    Debug.Log($"<color=red>[CombatValidator] ❌ BLOCKED: {reason}</color>");
                    return false;
                }
            }
            else
            {
                Debug.LogWarning($"[CombatValidator] Victim {victim.name} has no PlayerState component!");
            }

            if (attacker != null)
            {
                PlayerState attackerState = attacker.GetComponent<PlayerState>();
                if (attackerState != null)
                {
                    Debug.Log($"[CombatValidator] Attacker {attacker.name} safe zone state: {attackerState.IsInSafeZone}");

                    if (attackerState.IsInSafeZone)
                    {
                        reason = "Cannot attack from safe zone";
                        Debug.Log($"<color=red>[CombatValidator] ❌ BLOCKED: {reason}</color>");
                        return false;
                    }
                }
                else
                {
                    Debug.LogWarning($"[CombatValidator] Attacker {attacker.name} has no PlayerState component!");
                }
            }

            Debug.Log($"<color=green>[CombatValidator] ✅ ALLOWED: Damage can be applied</color>");
            return true;
        }
    }
}

using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.Collections;
using System.Collections.Generic;
using Genesis.Core;
using Genesis.Core.Networking;
using Genesis.Data;

namespace Genesis.Simulation
{
    [RequireComponent(typeof(NetworkObject))]
    public class TeleportPortal : NetworkBehaviour, IInteractable
    {
        [Header("Portal Data")]
        [SerializeField] private PortalData _portalData;

        // Cooldowns: keyed by ConnectionId
        private readonly Dictionary<int, float> _cooldowns = new Dictionary<int, float>();

        public string PortalID => _portalData != null ? _portalData.PortalID : "";
        public string DisplayName => _portalData != null ? _portalData.DisplayName : "Portal";

        // ═══════════════════════════════════════════════════════
        // IInteractable
        // ═══════════════════════════════════════════════════════

        public void Interact(NetworkObject player)
        {
            if (!base.IsServerInitialized || player == null || _portalData == null) return;

            var conn = player.Owner;
            if (!conn.IsValid) return;

            // Cooldown check
            int connId = conn.ClientId;
            if (_cooldowns.TryGetValue(connId, out float lastUse))
            {
                if (Time.time - lastUse < _portalData.Cooldown)
                {
                    Debug.Log($"[TeleportPortal] {player.name} on cooldown ({_portalData.Cooldown}s)");
                    return;
                }
            }

            // Level check
            var attributes = player.GetComponent<PlayerAttributes>();
            if (attributes != null && attributes.Level < _portalData.MinLevel)
            {
                Debug.Log($"[TeleportPortal] {player.name} level {attributes.Level} < required {_portalData.MinLevel}");
                return;
            }

            // Combat check
            if (_portalData.RequireOutOfCombat)
            {
                var stats = player.GetComponent<PlayerStats>();
                if (stats != null && stats.IsInCombat)
                {
                    Debug.Log($"[TeleportPortal] {player.name} is in combat, cannot teleport");
                    return;
                }
            }

            // Death check
            var playerStats = player.GetComponent<PlayerStats>();
            if (playerStats != null && playerStats.IsDead)
            {
                Debug.Log($"[TeleportPortal] {player.name} is dead, cannot teleport");
                return;
            }

            // Set cooldown and start sequence
            _cooldowns[connId] = Time.time;
            StartCoroutine(TeleportSequence(player, conn));
        }

        public bool CanInteract(NetworkObject player)
        {
            if (_portalData == null || player == null) return false;

            // Dead check
            var stats = player.GetComponent<PlayerStats>();
            if (stats != null && stats.IsDead) return false;

            return true;
        }

        public string GetInteractionPrompt()
        {
            return _portalData != null ? $"Enter {_portalData.DisplayName}" : "Enter Portal";
        }

        // ═══════════════════════════════════════════════════════
        // TELEPORT SEQUENCE (Server)
        // ═══════════════════════════════════════════════════════

        [Server]
        private IEnumerator TeleportSequence(NetworkObject player, NetworkConnection conn)
        {
            float halfDuration = _portalData.TransitionDuration * 0.5f;

            // 1. Fade out on client
            TargetFadeOut(conn, halfDuration);

            // 2. Wait for fade to complete
            yield return new WaitForSeconds(halfDuration);

            // 3. Move player on server
            Vector3 destPos = _portalData.DestinationPosition;
            Vector3 destRot = _portalData.DestinationRotation;

            // Disable CC, teleport, re-enable CC (server-side)
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = destPos;
            player.transform.rotation = Quaternion.Euler(destRot);

            if (cc != null) cc.enabled = true;

            // 4. Move to correct chunk scene
            ChunkCoordinate destChunk = ChunkCoordinate.FromWorldPosition(destPos);
            var sceneHandler = FindObjectOfType<ServerSceneHandler>();
            if (sceneHandler != null)
            {
                sceneHandler.MovePlayerToChunkScene(player, destChunk);
            }

            // 5. Tell client owner to also teleport (fixes prediction desync)
            var motor = player.GetComponent<PlayerMotorMultiplayer>();
            if (motor != null)
            {
                motor.RpcTeleportTo(conn, destPos, destRot);
            }

            // 6. Fade in on client
            TargetFadeIn(conn, halfDuration);
        }

        // ═══════════════════════════════════════════════════════
        // RPCs
        // ═══════════════════════════════════════════════════════

        [TargetRpc]
        private void TargetFadeOut(NetworkConnection conn, float duration)
        {
            EventBus.Trigger(PortalEvents.TELEPORT_FADE_OUT, duration);
            EventBus.Trigger("OnPortalSound", true);
        }

        [TargetRpc]
        private void TargetFadeIn(NetworkConnection conn, float duration)
        {
            EventBus.Trigger(PortalEvents.TELEPORT_FADE_IN, duration);
            EventBus.Trigger("OnPortalSound", false);

            // Resync audio state based on the zone at the destination
            var player = conn.FirstObject;
            if (player != null)
            {
                var playerState = player.GetComponent<Genesis.Simulation.World.PlayerState>();
                if (playerState != null)
                {
                    // Use EventBus to decoupling Simulation from Presentation/Audio
                    EventBus.Trigger("OnPlayerZoneChanged", playerState.IsInSafeZone);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // GIZMOS
        // ═══════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_portalData == null) return;

            // Draw line from portal to destination
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, _portalData.DestinationPosition);
            Gizmos.DrawWireSphere(_portalData.DestinationPosition, 0.5f);

            // Draw linked portal connection
            if (_portalData.LinkedPortal != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(_portalData.DestinationPosition, _portalData.LinkedPortal.DestinationPosition);
            }
        }
#endif
    }
}
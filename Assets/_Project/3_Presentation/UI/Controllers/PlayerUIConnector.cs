using UnityEngine;
using FishNet.Object;
using Genesis.Simulation;

namespace Genesis.Presentation.UI {

    /// <summary>
    /// Conecta automáticamente el Debug UI cuando el jugador local se spawnea.
    /// Este componente debe ir en el PREFAB del jugador.
    /// </summary>
    [RequireComponent(typeof(PlayerCombat))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerUIConnector : NetworkBehaviour {

        public override void OnStartClient() {
            base.OnStartClient();

            // Solo ejecutar para el jugador local
            if (!base.IsOwner) return;

            Debug.Log("[PlayerUIConnector] Jugador local spawneado, conectando UIs...");

            // Obtener referencias del jugador
            PlayerCombat combat = GetComponent<PlayerCombat>();
            PlayerStats stats = GetComponent<PlayerStats>();

            // Buscar los controladores de UI en la escena
            HUDController hudController = Object.FindFirstObjectByType<HUDController>();
            AbilityBarDebugController debugController = Object.FindFirstObjectByType<AbilityBarDebugController>();
            AbilityBarController abilityBarController = Object.FindFirstObjectByType<AbilityBarController>();

            // Conectar HUD principal (si existe)
            if (hudController != null && stats != null) {
                hudController.SetPlayerStats(stats);
                Debug.Log("[PlayerUIConnector] ✅ HUD conectado");
            } else {
                Debug.LogWarning("[PlayerUIConnector] ⚠️ No se encontró HUDController o PlayerStats");
            }

            // Conectar Debug UI (si existe)
            if (debugController != null && combat != null) {
                debugController.SetPlayerCombat(combat);
                Debug.Log("[PlayerUIConnector] ✅ Debug UI conectado");
            } else {
                if (debugController == null) {
                    Debug.LogWarning("[PlayerUIConnector] ⚠️ No se encontró AbilityBarDebugController");
                }
                if (combat == null) {
                    Debug.LogWarning("[PlayerUIConnector] ⚠️ No se encontró PlayerCombat");
                }
            }

            // Conectar Ability Bar permanente (si existe)
            if (abilityBarController != null && combat != null) {
                abilityBarController.SetPlayerCombat(combat);
                Debug.Log("[PlayerUIConnector] ✅ Ability Bar conectado");
            } else {
                if (abilityBarController == null) {
                    Debug.LogWarning("[PlayerUIConnector] ⚠️ No se encontró AbilityBarController");
                }
            }
        }
    }
}

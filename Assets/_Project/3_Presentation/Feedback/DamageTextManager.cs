using UnityEngine;
using Genesis.Data;
using Genesis.Core;

namespace Genesis.Presentation.Feedback {
    /// <summary>
    /// Manager centralizado para spawnear textos de combate.
    /// </summary>
    public class DamageTextManager : MonoBehaviour {
        public static DamageTextManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private FloatingTextConfig config;
        [SerializeField] private GameObject floatingTextPrefab;

        void OnEnable() {
            EventBus.Subscribe<FloatingTextData>("OnShowFloatingText", OnShowFloatingText);
        }

        void OnDisable() {
            EventBus.Unsubscribe<FloatingTextData>("OnShowFloatingText", OnShowFloatingText);
        }

        private void OnShowFloatingText(FloatingTextData data) {
            Color color = Color.white;
            switch (data.type.ToLower()) {
                case "damage": color = GetColorForDamage(); break;
                case "heal": color = GetColorForHeal(); break;
                case "shield": color = GetColorForShield(); break;
                case "critical": color = GetColorForCritical(); break;
                case "mana": color = GetColorForMana(); break;
            }
            Spawn(data.position, data.text, color, data.isCritical);
        }

        void Awake() {
            if (Instance == null) {
                Instance = this;
            } else {
                Destroy(gameObject);
            }
        }

        public void Spawn(Vector3 position, string text, Color color, bool isCritical = false) {
            if (floatingTextPrefab == null) {
                Debug.LogWarning("[DamageTextManager] No hay prefab de texto asignado.");
                return;
            }
            if (config == null) {
                Debug.LogWarning("[DamageTextManager] No hay configuración asignada.");
                return;
            }

            Debug.Log($"[DamageTextManager] Spawning text: {text} at {position}");
            GameObject obj = Instantiate(floatingTextPrefab, position, Quaternion.identity);
            FloatingText fText = obj.GetComponent<FloatingText>();
            
            if (fText != null) {
                fText.Initialize(text, color, config, isCritical);
            }
        }

        public Color GetColorForDamage() => config != null ? config.damageColor : Color.red;
        public Color GetColorForHeal() => config != null ? config.healColor : Color.green;
        public Color GetColorForShield() => config != null ? config.shieldColor : Color.cyan;
        public Color GetColorForCritical() => config != null ? config.criticalColor : Color.yellow;
        public Color GetColorForMana() => config != null ? config.manaColor : Color.blue;
    }
}

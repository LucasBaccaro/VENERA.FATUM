using UnityEngine;
using System.Collections.Generic;

namespace Genesis.Data {
    [CreateAssetMenu(fileName = "Class_New", menuName = "Genesis/System/Class")]
    public class ClassData : ScriptableObject {
        [Header("Identity")]
        public string ClassName;
        public Sprite ClassIcon;

        [Header("Base Stats")]
        public float MaxHealth = 100f;
        public float MaxMana = 100f;
        public float ManaRegenPerSecond = 5f;
        [Tooltip("Base HP regen per second (out of combat)")]
        public float HealthRegenPerSecond = 2f;

        [Header("Per-Level Growth")]
        [Tooltip("Flat HP gained per level")]
        public float HealthPerLevel = 10f;
        [Tooltip("Flat Mana gained per level")]
        public float ManaPerLevel = 5f;

        [Header("Visuals")]
        public RuntimeAnimatorController AnimatorController;
        public GameObject ModelPrefab;

        [Header("Initial Abilities")]
        public List<AbilityData> InitialAbilities;
    }
}

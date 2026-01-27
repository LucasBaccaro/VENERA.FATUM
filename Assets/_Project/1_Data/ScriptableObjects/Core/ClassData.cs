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

        [Header("Visuals")]
        public RuntimeAnimatorController AnimatorController;
        public GameObject ModelPrefab;

        [Header("Initial Abilities")]
        public List<AbilityData> InitialAbilities;
    }
}

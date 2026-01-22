using UnityEngine;

namespace Genesis.Data {

    [CreateAssetMenu(fileName = "Effect_New", menuName = "Genesis/Combat/Status Effect")]
    public class StatusEffectData : ScriptableObject {
        
        [Header("Core Info")]
        public int ID;
        public string Name;
        public Sprite Icon;
        [TextArea] public string Description;
        
        [Header("Behavior")]
        public EffectType Type;
        public bool IsBuff; // True = Bueno, False = Malo (Debuff)
        
        [Header("Duration & Stacking")]
        [Tooltip("Duración en segundos. 0 = Permanente (hasta ser removido explícitamente)")]
        public float Duration;
        
        [Tooltip("¿Se puede acumular?")]
        public bool IsStackable;
        
        [Tooltip("Máximo de acumulaciones")]
        [Min(1)] public int MaxStacks = 1;
        
        [Header("Magnitude (Values)")]
        [Tooltip("Porcentaje de Slow/Haste (0.5 = 50%)")]
        [Range(0f, 1f)]
        public float PercentageValue; 
        
        [Tooltip("Valor plano para Shield, Heal por tick, Daño por tick")]
        public float FlatValue;
        
        [Header("Periodic (DoT/HoT)")]
        public float TickInterval = 1f;

        [Header("Visuals")]
        public GameObject VFXPrefab; // Aura visual
        public AudioClip ApplySound;
    }
}

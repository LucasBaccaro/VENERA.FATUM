using UnityEngine;

namespace Genesis.Data {

    [CreateAssetMenu(fileName = "Ability_New", menuName = "Genesis/Combat/Ability")]
    public class AbilityData : ScriptableObject {
        
        [Header("Identity")]
        public int ID;
        public string Name;
        public Sprite Icon;
        [TextArea] public string Description;
        
        [Header("Logic Binding")]
        [Tooltip("ScriptableObject que contiene la lógica de ejecución (Projectile, Melee, etc)")]
        public AbilityLogic Logic;

        [Header("Requirements")]
        public float ManaCost;
        public float Cooldown;
        [Tooltip("Global Cooldown (tiempo de espera global)")]
        public float GCD = 1.0f;
        
        [Header("Casting")]
        public CastingType CastType;
        public float CastTime;
        public bool CanMoveWhileCasting;
        
        [Header("Targeting")]
        public TargetType TargetingMode;
        public IndicatorType IndicatorType; // NEW: Tipo de indicador visual
        public float Range;
        [Tooltip("Radio de efecto (para AoE) o ancho (para lineas)")]
        public float Radius;
        [Tooltip("Ángulo del cono (solo para IndicatorType.Cone)")]
        public float Angle = 60f;
        
        [Header("Combat Values")]
        public AbilityCategory Category;
        public int BaseDamage; // Usamos int para daño RPG clásico, float si prefieres
        public int BaseHeal;
        
        [Header("Status Effects")]
        public StatusEffectData[] ApplyToTarget;
        public StatusEffectData[] ApplyToSelf;
        
        [Header("Projectiles (Si aplica)")]
        public GameObject ProjectilePrefab;
        public float ProjectileSpeed = 20f;
        
        [Header("Visuals & Audio")]
        public GameObject CastVFX;
        public GameObject ImpactVFX;
        public AudioClip CastSound;
        public AudioClip ImpactSound;
        public string AnimationTrigger = "Cast"; // Trigger en el Animator
    }

    /// <summary>
    /// Tipo de indicador visual para la habilidad
    /// </summary>
    public enum IndicatorType {
        None,      // Targeted abilities (sistema legacy - no requiere indicador)
        Line,      // Skillshot direccional + Channel (LineIndicator)
        Circle,    // AOE circular - ground o self (CircleIndicator)
        Cone,      // Área cónica frontal (ConeIndicator)
        Arrow,     // Dash/Charge - movimiento (ArrowIndicator)
        Trap       // Trampa persistente (TrapIndicator)
    }
}

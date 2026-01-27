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

        [Header("Channeling (Solo si CastType = Channeling)")]
        [Tooltip("Tiempo entre ticks de daño/efecto durante channeling (segundos). Default: 0.1s")]
        public float ChannelTickRate = 0.1f;
        [Tooltip("Si es true, el channel daña a TODOS los enemigos en la línea. Si es false, solo al primero.")]
        public bool ChannelHitAllTargets = true;
        [Tooltip("Duración máxima del channel (segundos). 0 = sin límite.")]
        public float ChannelMaxDuration = 0f;
        
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
        
        [Header("Status Effects (Execution)")]
        [Tooltip("Si es true, los efectos se aplican al INICIAR el cast/channel (antes de la barra). Si es false, al FINALIZAR.")]
        public bool ApplyEffectsInstant = false;
        
        [Header("Status Effects")]
        public StatusEffectData[] ApplyToTarget;
        public StatusEffectData[] ApplyToSelf;
        
        [Header("Projectiles (Si aplica)")]
        public GameObject ProjectilePrefab;
        public float ProjectileSpeed = 20f;

[Header("Indicator Visuals")]
[Tooltip("Material del indicador visual (Decal / Mesh). Permite un look distinto por habilidad.")]
public Material IndicatorMaterial;

        
        [Header("Visuals & Audio")]
        public GameObject CastVFX;
        public GameObject ImpactVFX;
        public AudioClip CastSound;
        public AudioClip ImpactSound;
        public string StartCastAnimationTrigger = "StartCast"; // Trigger al INICIAR el cast
        public string AnimationTrigger = "Cast"; // Trigger al FINALIZAR el cast
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

using UnityEngine;
using Genesis.Simulation;

namespace Genesis.Data {

    [CreateAssetMenu(fileName = "NewEnemy", menuName = "VENERA.FATUM/Enemies/Enemy Data", order = 0)]
    public class EnemyData : ScriptableObject {
        [Header("Identity")]
        public string EnemyTag;
        public string DisplayName;

        [Header("Stats")]
        public float MaxHealth = 100f;
        public float MinDamage = 5f;
        public float MaxDamage = 11f;
        public float AttackRange = 2f;
        public float AttackCooldown = 2f;

        [Header("AI")]
        public float DetectionRange = 8f;
        public float MoveSpeed = 3f;

        [Header("Ranged Attack")]
        public bool IsRanged = false;
        public GameObject ProjectilePrefab;
        public float ProjectileSpeed = 12f;
        public float PreferredDistance = 8f;

        [Header("Role & Archetype")]
        public EnemyRole Role = EnemyRole.Grunt;
        public EnemyArchetype Archetype = EnemyArchetype.Melee;

        [Header("Leash & Patrol")]
        public float LeashRange = 20f;
        public float PatrolRadius = 0f;
        public float PatrolWaitTime = 3f;

        [Header("Telegraph")]
        public float AnticipationDuration = 0f;
        public float RecoveryDuration = 0f;

        [Header("Kiting (Ranged/Support)")]
        public float KiteThreshold = 3f;
        public float KiteDistance = 6f;
        public GameObject KitingProjectilePrefab;
        public float KitingProjectileSpeed = 12f;

        [Header("Support")]
        public float SupportScanRange = 12f;
        public float HealAmount = 20f;
        public float HealCooldown = 4f;

        [Header("Knockback (Tank)")]
        public float KnockbackForce = 4f;
        public float KnockbackDuration = 0.3f;

        [Header("Audio")]
        public AudioClip HitSound;
        public AudioClip DeathSound;
        [Tooltip("Sound when the enemy swings/fires (played at attack start).")]
        public AudioClip AttackSound;
        [Tooltip("Sound when a melee hit connects (played at damage frame).")]
        public AudioClip AttackImpactSound;

        [Header("Rewards")]
        [Tooltip("Gold dropped on death")]
        public int GoldReward = 10;

        [Tooltip("Loot table for item drops on death")]
        public LootTable LootTable;
    }
}

using UnityEngine;

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
    }
}

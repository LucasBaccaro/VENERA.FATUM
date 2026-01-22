using UnityEngine;

namespace Genesis.Core {

    /// <summary>
    /// Constantes de Layers y LayerMasks para todo el proyecto.
    /// IMPORTANTE: Configura estos layers manualmente en Project Settings > Tags and Layers.
    /// </summary>
    public static class Layers {

        // ═══════════════════════════════════════════════════════
        // LAYER INDICES
        // ═══════════════════════════════════════════════════════

        public const int Default = 0;
        public const int Player = 3;
        public const int Enemy = 6;
        public const int Projectile = 7;
        public const int Environment = 8;
        public const int SafeZone = 9;
        public const int Loot = 10;
        public const int Interactable = 11;
        public const int IgnoreRaycast = 31;

        // ═══════════════════════════════════════════════════════
        // LAYER MASKS (Combinadas para queries de Physics)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Todas las entidades que pueden recibir daño (Player + Enemy)
        /// </summary>
        public static readonly int Damageable = (1 << Player) | (1 << Enemy);

        /// <summary>
        /// Todo lo que bloquea movimiento (Environment)
        /// </summary>
        public static readonly int Walkable = (1 << Environment);

        /// <summary>
        /// Máscara para targeting (click en enemigos)
        /// </summary>
        public static readonly int TargetingMask = Damageable;

        /// <summary>
        /// Máscara para ground targeting (AoE en el suelo)
        /// </summary>
        public static readonly int GroundMask = (1 << Environment);

        /// <summary>
        /// Todo lo que puede colisionar con proyectiles
        /// </summary>
        public static readonly int ProjectileCollisionMask = Damageable | (1 << Environment);

        // ═══════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Convierte un layer index a LayerMask
        /// </summary>
        public static int ToMask(int layerIndex) {
            return 1 << layerIndex;
        }

        /// <summary>
        /// Verifica si un GameObject está en un layer específico
        /// </summary>
        public static bool IsInLayer(GameObject obj, int layerIndex) {
            return obj.layer == layerIndex;
        }

        /// <summary>
        /// Verifica si un layer está incluido en una máscara
        /// </summary>
        public static bool IsInLayerMask(int layer, int layerMask) {
            return ((1 << layer) & layerMask) != 0;
        }
    }
}

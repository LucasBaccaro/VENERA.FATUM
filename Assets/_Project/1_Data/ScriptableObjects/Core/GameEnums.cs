namespace Genesis.Data {
    
    // Tipos de Casteo
    public enum CastingType {
        Instant,        // Se ejecuta inmediatamente
        Casting,        // Requiere tiempo de casteo (barra estática)
        Channeling,     // Efecto continuo mientras se mantiene
        Movement        // Permite movimiento durante cast (casteo móvil)
    }

    // Tipos de Objetivo
    public enum TargetType {
        None,           // No requiere target (ej: Buff self, Shout)
        Enemy,          // Requiere enemigo seleccionado
        Ally,           // Requiere aliado seleccionado
        Ground,         // Click en suelo (AoE posicional)
        EnemyOrGround,  // Híbrido (ej: Meteoro dirigido o posicional)
        Self            // Siempre se aplica al caster
    }

    // Categoría de Habilidad
    public enum AbilityCategory {
        Physical,
        Magical,
        Utility
    }

    // Tipos de Efectos (Flags para combinaciones)
    [System.Flags]
    public enum EffectType {
        None         = 0,
        Stun         = 1 << 0,  // Bloquea movimiento y acciones
        Root         = 1 << 1,  // Bloquea solo movimiento
        Silence      = 1 << 2,  // Bloquea habilidades mágicas
        Slow         = 1 << 3,  // Reduce velocidad movimiento
        Shield       = 1 << 4,  // Absorbe daño
        Reflect      = 1 << 5,  // Refleja proyectiles
        Invulnerable = 1 << 6,  // Inmune a daño
        Poison       = 1 << 7,  // DoT (Damage over Time)
        Regen        = 1 << 8,  // HoT (Heal over Time)
        Haste        = 1 << 9,  // Aumenta velocidad ataque/cast
        Speed        = 1 << 10  // Aumenta velocidad movimiento
    }
}

│ Plan to implement                                                                                                                                                                                            │
│                                                                                                                                                                                                              │
│ Plan: Sistema de Atributos, Tiers y Progresion (GDD v1.1)                                                                                                                                                    │
│                                                                                                                                                                                                              │
│ Contexto                                                                                                                                                                                                     │
│                                                                                                                                                                                                              │
│ El proyecto VENERA.FATUM tiene un framework RPG multiplayer funcional con: items/equipment (9 slots, 4 rarezas, T0), 2 clases (Guerrero, Mago), stats basicos (HP/Mana/SpellPower), combate hibrido          │
│ (targeted+skillshot), y UI completa. Falta el sistema de atributos primarios, leveling/XP, sub-stats, combat identity (crit/overpower), slots de anillos, y el flujo tutorial niveles 1-5.                   │
│                                                                                                                                                                                                              │
│ ---                                                                                                                                                                                                          │
│ Fase 0: Enums y Estructuras de Datos (Foundation)                                                                                                                                                            │
│                                                                                                                                                                                                              │
│ Modificar: Assets/_Project/1_Data/ScriptableObjects/Items/ItemEnums.cs                                                                                                                                       │
│                                                                                                                                                                                                              │
│ - Extender EquipmentSlot con Ring1, Ring2 (9 -> 11 slots)                                                                                                                                                    │
│ - Extender StatType con:                                                                                                                                                                                     │
│   - Atributos primarios: Strength, Agility, Intelligence, Wisdom, Constitution                                                                                                                               │
│   - Sub-stats combate: Haste, LifeSteal, Penetration, Block                                                                                                                                                  │
│   - Sub-stats mundo: LootLuck, Lockpicking, Perception, MoveSpeed                                                                                                                                            │
│ - Extender StatModifier.ToString() para los nuevos tipos                                                                                                                                                     │
│                                                                                                                                                                                                              │
│ Crear: Assets/_Project/1_Data/Definitions/CombatResult.cs                                                                                                                                                    │
│                                                                                                                                                                                                              │
│ - Enum DamageResultType: Normal, Critical, Overpower, CriticalHeal, Blocked, Evaded                                                                                                                          │
│ - Struct CombatResult: FinalDamage, ResultType, WasCritical, WasOverpower, LifeStealAmount                                                                                                                   │
│                                                                                                                                                                                                              │
│ Crear: Assets/_Project/1_Data/ScriptableObjects/Core/AttributeConfig.cs                                                                                                                                      │
│                                                                                                                                                                                                              │
│ - ScriptableObject con todas las formulas de escalado:                                                                                                                                                       │
│   - STR: PhysicalDamagePerPoint (0.5%), BlockValuePerPoint                                                                                                                                                   │
│   - AGI: CritChancePerPoint (0.15%), EvasionPerPoint (0.1%)                                                                                                                                                  │
│   - INT: MagicDamagePerPoint (0.5%), SpellCritPerPoint (0.15%)                                                                                                                                               │
│   - WIS: HealingPowerPerPoint (0.5%), ManaRegenPerPoint (0.1/s)                                                                                                                                              │
│   - CON: HealthPerPoint (5 HP), HPRegenPerPoint (0.05 HP/s OOC)                                                                                                                                              │
│   - Overpower: BaseChance, ChancePerStr, ArmorIgnore (50%), DamageMultiplier (1.5x)                                                                                                                          │
│   - Critical: DamageMultiplier (2.0x), CritHealMultiplier (2.0x)                                                                                                                                             │
│   - Leveling: MaxLevel (50), PointsPerLevel (5), BaseXP, XP curve                                                                                                                                            │
│                                                                                                                                                                                                              │
│ ---                                                                                                                                                                                                          │
│ Fase 1: PlayerAttributes (Core Runtime)                                                                                                                                                                      │
│                                                                                                                                                                                                              │
│ Crear: Assets/_Project/2_Simulation/Entities/Player/PlayerAttributes.cs                                                                                                                                      │
│                                                                                                                                                                                                              │
│ - NetworkBehaviour con SyncVar para:                                                                                                                                                                         │
│   - Level, CurrentXP, XPToNextLevel, UnspentPoints                                                                                                                                                           │
│   - STR, AGI, INT, WIS, CON (SyncVar cada uno)                                                                                                                                                               │
│   - Stats derivados calculados: PhysicalDamageBonus, MagicDamageBonus, CritChance, SpellCritChance, HealingPowerBonus, EvasionChance, OverpowerChance, BonusHealth, BonusManaRegen, HealthRegenOOC           │
│   - Bonuses de equipment (set via EquipmentManager): EquipStr, EquipAgi, etc + Haste, LifeSteal, Penetration, Block, etc                                                                                     │
│ - Metodos server:                                                                                                                                                                                            │
│   - GainXP(float): Acumula XP, dispara LevelUp si corresponde                                                                                                                                                │
│   - LevelUp(): Incrementa nivel, otorga 5 puntos, recalcula XP needed                                                                                                                                        │
│   - CmdAllocatePoint(int attrIndex): ServerRpc, valida puntos disponibles, asigna +1                                                                                                                         │
│   - RecalculateDerivedStats(): Lee config SO, calcula todo desde (base + equip) attributes                                                                                                                   │
│   - SetEquipmentBonuses(...): Llamado por EquipmentManager.RecalculateStats()                                                                                                                                │
│   - ResetAttributes(): Para item de reset futuro                                                                                                                                                             │
│ - OnChange callbacks -> EventBus para UI                                                                                                                                                                     │
│                                                                                                                                                                                                              │
│ Modificar: Assets/_Project/2_Simulation/Entities/Player/PlayerStats.cs                                                                                                                                       │
│                                                                                                                                                                                                              │
│ - Agregar ref a PlayerAttributes (GetComponent en Awake)                                                                                                                                                     │
│ - InitializeFromClass(): Tambien llamar attributes.RecalculateDerivedStats()                                                                                                                                 │
│ - Mana regen en Update: manaRegenPerSecond + _attributes.BonusManaRegen                                                                                                                                      │
│ - MaxHealth incluye CON bonus: se actualiza via EquipmentManager.RecalculateStats() que ya notifica PlayerStats.SetMaxHealth()                                                                               │
│                                                                                                                                                                                                              │
│ Modificar: Assets/_Project/2_Simulation/Entities/Player/PlayerClassManager.cs                                                                                                                                │
│                                                                                                                                                                                                              │
│ - Despues de SetClass, notificar PlayerAttributes para recalcular                                                                                                                                            │
│                                                                                                                                                                                                              │
│ ---                                                                                                                                                                                                          │
│ Fase 2: Motor de Calculo de Combate                                                                                                                                                                          │
│                                                                                                                                                                                                              │
│ Crear: Assets/_Project/2_Simulation/Combat/Core/CombatCalculator.cs                                                                                                                                          │
│                                                                                                                                                                                                              │
│ - Clase estatica server-side con:                                                                                                                                                                            │
│   - CalculateDamage(caster, target, baseDamage, category, config) -> CombatResult                                                                                                                            │
│       i. Aplica scaling de atributo primario (STR -> phys, INT -> magic)                                                                                                                                     │
│     ii. Aplica SpellPower de equipment (sistema existente)                                                                                                                                                   │
│     iii. Check Evasion del target (AGI)                                                                                                                                                                      │
│     iv. Check Overpower (Guerrero: STR) vs Critical (otros: AGI/INT) - mutuamente excluyentes                                                                                                                │
│     v. Calcula LifeSteal                                                                                                                                                                                     │
│   - CalculateHeal(caster, baseHeal, config) -> CombatResult                                                                                                                                                  │
│       i. Aplica WIS healing power                                                                                                                                                                            │
│     ii. Check Critical Heal (INT del Sacerdote)                                                                                                                                                              │
│                                                                                                                                                                                                              │
│ Modificar: Todos los AbilityLogic (patron uniforme)                                                                                                                                                          │
│                                                                                                                                                                                                              │
│ Archivos a modificar:                                                                                                                                                                                        │
│ - Assets/_Project/2_Simulation/Combat/Abilities/Logic/TargetedLogic.cs                                                                                                                                       │
│ - Assets/_Project/2_Simulation/Combat/Abilities/Logic/AOELogic.cs                                                                                                                                            │
│ - Assets/_Project/2_Simulation/Combat/Abilities/Logic/SkillshotLogic.cs                                                                                                                                      │
│ - Assets/_Project/2_Simulation/Combat/Abilities/Logic/ChannelLogic.cs                                                                                                                                        │
│ - Assets/_Project/2_Simulation/Combat/Abilities/Logic/ConeLogic.cs                                                                                                                                           │
│ - Assets/_Project/2_Simulation/Combat/Abilities/Logic/SelfAOELogic.cs                                                                                                                                        │
│ - Assets/_Project/2_Simulation/Combat/Projectiles/ProjectileController.cs                                                                                                                                    │
│ - Assets/_Project/2_Simulation/Combat/Projectiles/TargetedProjectile.cs                                                                                                                                      │
│                                                                                                                                                                                                              │
│ Patron:                                                                                                                                                                                                      │
│ // ANTES: damageable.TakeDamage(data.BaseDamage, caster);                                                                                                                                                    │
│ // DESPUES:                                                                                                                                                                                                  │
│ CombatResult result = CombatCalculator.CalculateDamage(caster, target, data.BaseDamage, data.Category, config);                                                                                              │
│ if (result.ResultType != DamageResultType.Evaded) {                                                                                                                                                          │
│     damageable.TakeDamage(result.FinalDamage, caster, result.ResultType);                                                                                                                                    │
│     if (result.LifeStealAmount > 0) caster.GetComponent<PlayerStats>()?.Heal(result.LifeStealAmount);                                                                                                        │
│ }                                                                                                                                                                                                            │
│                                                                                                                                                                                                              │
│ Modificar: Assets/_Project/2_Simulation/Entities/Player/PlayerStats.cs                                                                                                                                       │
│                                                                                                                                                                                                              │
│ - TakeDamage: agregar overload con DamageResultType para floating text correcto                                                                                                                              │
│ - TargetShowDamageText: pasar tipo (critical/overpower/evade) para colores diferenciados                                                                                                                     │
│                                                                                                                                                                                                              │
│ Modificar: Assets/_Project/3_Presentation/Feedback/DamageTextManager.cs                                                                                                                                      │
│                                                                                                                                                                                                              │
│ - Agregar colores: Overpower (naranja), Evade (gris), CritHeal (verde brillante)                                                                                                                             │
│ - Agregar escala mayor para critical/overpower text                                                                                                                                                          │
│                                                                                                                                                                                                              │
│ Modificar: Assets/_Project/2_Simulation/Entities/Player/PlayerCombat.cs                                                                                                                                      │
│                                                                                                                                                                                                              │
│ - Deprecar CalculateFinalDamage() (reemplazado por CombatCalculator)                                                                                                                                         │
│                                                                                                                                                                                                              │
│ ---                                                                                                                                                                                                          │
│ Fase 3: Extension de Equipment (Ring Slots + Sub-stats)                                                                                                                                                      │
│                                                                                                                                                                                                              │
│ Modificar: Assets/_Project/2_Simulation/Items/EquipmentManager.cs                                                                                                                                            │
│                                                                                                                                                                                                              │
│ - Agregar SyncVar<ItemSlot> _ring1Slot, _ring2Slot con OnChange callbacks                                                                                                                                    │
│ - Actualizar: OnStartServer, EquipItem switch, UnequipSlot switch, GetEquipmentSlot, GetAllEquipment, ClearAllEquipment, ValidateEquipmentForClass (rings son class-agnostic)                                │
│ - Overhaul RecalculateStats():                                                                                                                                                                               │
│   - Agregar agregacion de TODOS los StatType (no solo MaxHP/Mana/SpellPower)                                                                                                                                 │
│   - Llamar PlayerAttributes.SetEquipmentBonuses(...) con totales de sub-stats                                                                                                                                │
│   - Procesar 11 slots (incluye Ring1, Ring2)                                                                                                                                                                 │
│                                                                                                                                                                                                              │
│ Modificar: Assets/_Project/3_Presentation/UI/Controllers/CharacterPanelDebugController.cs y produccion                                                                                                       │
│                                                                                                                                                                                                              │
│ - Agregar elementos UI para Ring1, Ring2                                                                                                                                                                     │
│ - Agregar seccion de atributos primarios (STR/AGI/INT/WIS/CON) con valores actuales                                                                                                                          │
│ - Agregar seccion de stats derivados del combate: Crit Chance, Spell Crit, Overpower Chance, Evasion, Phys Dmg Bonus, Magic Dmg Bonus, Healing Power                                                         │
│ - Agregar seccion de sub-stats de combate: Haste, LifeSteal, Penetration, Block                                                                                                                              │
│ - Agregar seccion de sub-stats de mundo: LootLuck, Lockpicking, Perception, MoveSpeed                                                                                                                        │
│ - Todas las secciones se actualizan via EventBus cuando cambian atributos o equipment                                                                                                                        │
│                                                                                                                                                                                                              │
│ Modificar: UXML correspondientes para character panel                                                                                                                                                        │
│                                                                                                                                                                                                              │
│ - Agregar layout de ring slots                                                                                                                                                                               │
│ - Agregar seccion "Atributos Primarios" con 5 filas                                                                                                                                                          │
│ - Agregar seccion "Stats de Combate" con sub-stats de combate                                                                                                                                                │
│ - Agregar seccion "Stats de Mundo" con sub-stats de utilidad                                                                                                                                                 │
│                                                                                                                                                                                                              │
│ ---                                                                                                                                                                                                          │
│ Fase 4: Sistema de XP y Nivel                                                                                                                                                                                │
│                                                                                                                                                                                                              │
│ En PlayerAttributes.cs (ya creado en Fase 1):                                                                                                                                                                │
│                                                                                                                                                                                                              │
│ - GainXP(): Loop de levelup, formula XP = BaseXP * 1.15^(level-1)                                                                                                                                            │
│ - LevelUp(): +5 puntos, recalcular XP needed, EventBus "OnLevelUp"                                                                                                                                           │
│ - RpcOnLevelUp para feedback visual                                                                                                                                                                          │
│                                                                                                                                                                                                              │
│ Crear: Assets/_Project/2_Simulation/World/XPRewardSystem.cs                                                                                                                                                  │
│                                                                                                                                                                                                              │
│ - Componente server-side simple que otorga XP por eventos                                                                                                                                                    │
│ - Escucha EventBus "OnEnemyKilled", "OnQuestCompleted"                                                                                                                                                       │
│ - Configurable via SO: XP por tipo de enemigo/quest                                                                                                                                                          │
│                                                                                                                                                                                                              │
│ Modificar: HUD existente                                                                                                                                                                                     │
│                                                                                                                                                                                                              │
│ - Conectar XP bar y level label a EventBus events ("OnXPChanged", "OnLevelUp")                                                                                                                               │
│                                                                                                                                                                                                              │
│ ---                                                                                                                                                                                                          │
│ Fase 5: UI de Asignacion de Atributos                                                                                                                                                                        │
│                                                                                                                                                                                                              │
│ Crear: Assets/_Project/3_Presentation/UI/Controllers/AttributePanelController.cs                                                                                                                             │
│                                                                                                                                                                                                              │
│ - Panel accesible desde Character Panel o hotkey dedicado                                                                                                                                                    │
│ - Muestra: STR/AGI/INT/WIS/CON con valores actuales                                                                                                                                                          │
│ - Muestra: puntos sin gastar                                                                                                                                                                                 │
│ - Botones "+" por atributo (llama CmdAllocatePoint)                                                                                                                                                          │
│ - Preview de stats derivados al hover                                                                                                                                                                        │
│ - EventBus listeners para actualizacion en tiempo real                                                                                                                                                       │
│                                                                                                                                                                                                              │
│ Crear: Assets/_Project/3_Presentation/UI/Views/AttributePanel.uxml                                                                                                                                           │
│                                                                                                                                                                                                              │
│ - 5 filas (una por atributo): icono, nombre, valor, boton +, stats derivados                                                                                                                                 │
│                                                                                                                                                                                                              │
│ Crear: Assets/_Project/3_Presentation/UI/Styles/AttributePanel.uss                                                                                                                                           │
│                                                                                                                                                                                                              │
│ - Estilos consistentes con UI existente                                                                                                                                                                      │
│                                                                                                                                                                                                              │
│ ---                                                                                                                                                                                                          │
│ Orden de Implementacion                                                                                                                                                                                      │
│                                                                                                                                                                                                              │
│ Fase 0 (Enums/Structs) ──> Fase 1 (PlayerAttributes)                                                                                                                                                         │
│        |                          |                                                                                                                                                                          │
│        |                          ├──> Fase 2 (CombatCalculator)                                                                                                                                             │
│        |                          |                                                                                                                                                                          │
│        |                          ├──> Fase 4 (XP/Nivel)                                                                                                                                                     │
│        |                          |          |                                                                                                                                                               │
│        |                          |          └──> Fase 5 (Attribute UI)                                                                                                                                      │
│        |                          |                                                                                                                                                                          │
│        └──> Fase 3 (Ring Slots + Sub-stats)                                                                                                                                                                  │
│                                                                                                                                                                                                              │
│ Fases 0 y 1 son secuenciales. Fases 2, 3, 4 pueden hacerse en paralelo despues de Fase 1. Fase 5 depende de 1 y 4.                                                                                           │
│                                                                                                                                                                                                              │
│ ---                                                                                                                                                                                                          │
│ Archivos a Crear (9)                                                                                                                                                                                         │
│ ┌───────────────────────────────────────────────────────────┬──────────────┐                                                                                                                                 │
│ │                          Archivo                          │     Capa     │                                                                                                                                 │
│ ├───────────────────────────────────────────────────────────┼──────────────┤                                                                                                                                 │
│ │ 1_Data/Definitions/CombatResult.cs                        │ Data         │                                                                                                                                 │
│ ├───────────────────────────────────────────────────────────┼──────────────┤                                                                                                                                 │
│ │ 1_Data/ScriptableObjects/Core/AttributeConfig.cs          │ Data         │                                                                                                                                 │
│ ├───────────────────────────────────────────────────────────┼──────────────┤                                                                                                                                 │
│ │ 2_Simulation/Entities/Player/PlayerAttributes.cs          │ Simulation   │                                                                                                                                 │
│ ├───────────────────────────────────────────────────────────┼──────────────┤                                                                                                                                 │
│ │ 2_Simulation/Combat/Core/CombatCalculator.cs              │ Simulation   │                                                                                                                                 │
│ ├───────────────────────────────────────────────────────────┼──────────────┤                                                                                                                                 │
│ │ 2_Simulation/World/XPRewardSystem.cs                      │ Simulation   │                                                                                                                                 │
│ ├───────────────────────────────────────────────────────────┼──────────────┤                                                                                                                                 │
│ │ 3_Presentation/UI/Controllers/AttributePanelController.cs │ Presentation │                                                                                                                                 │
│ ├───────────────────────────────────────────────────────────┼──────────────┤                                                                                                                                 │
│ │ 3_Presentation/UI/Views/AttributePanel.uxml               │ Presentation │                                                                                                                                 │
│ ├───────────────────────────────────────────────────────────┼──────────────┤                                                                                                                                 │
│ │ 3_Presentation/UI/Styles/AttributePanel.uss               │ Presentation │                                                                                                                                 │
│ └───────────────────────────────────────────────────────────┴──────────────┘                                                                                                                                 │
│ Archivos a Modificar (~15)                                                                                                                                                                                   │
│ ┌─────────────────────────┬───────────────────────────────────────────────────────┐                                                                                                                          │
│ │         Archivo         │                        Cambios                        │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ ItemEnums.cs            │ EquipmentSlot +Ring1/Ring2, StatType +13 entries      │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ PlayerStats.cs          │ Integrar PlayerAttributes, TakeDamage con result type │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ PlayerClassManager.cs   │ Notificar PlayerAttributes                            │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ EquipmentManager.cs     │ +2 ring SyncVars, RecalculateStats overhaul           │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ PlayerCombat.cs         │ Deprecar CalculateFinalDamage                         │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ TargetedLogic.cs        │ Usar CombatCalculator                                 │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ AOELogic.cs             │ Usar CombatCalculator                                 │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ SkillshotLogic.cs       │ Usar CombatCalculator                                 │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ ChannelLogic.cs         │ Usar CombatCalculator                                 │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ ConeLogic.cs            │ Usar CombatCalculator                                 │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ SelfAOELogic.cs         │ Usar CombatCalculator                                 │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ ProjectileController.cs │ Usar CombatCalculator                                 │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ TargetedProjectile.cs   │ Usar CombatCalculator                                 │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ DamageTextManager.cs    │ Colores overpower/evade/critheal                      │                                                                                                                          │
│ ├─────────────────────────┼───────────────────────────────────────────────────────┤                                                                                                                          │
│ │ CharacterPanel UI       │ Ring slots + atributos                                │                                                                                                                          │
│ └─────────────────────────┴───────────────────────────────────────────────────────┘                                                                                                                          │
│ Diferido (No incluido en este plan)                                                                                                                                                                          │
│                                                                                                                                                                                                              │
│ - Clases Hunter y Priest (requieren modelos/animaciones)                                                                                                                                                     │
│ - Sistema NPC/Quest completo                                                                                                                                                                                 │
│ - Flujo tutorial scripted (requiere NPC system)                                                                                                                                                              │
│ - World sub-stats funcionales (LootLuck, Lockpicking, Perception - no hay sistemas que los consuman)                                                                                                         │
│ - Sistema de armadura/resistencia en NPCs                                                                                                                                                                    │
│ - HP regen fuera de combate (requiere tracking de combat state)                                                                                                                                              │
│ - Item de Reset de atributos (UI de tienda)                                                                                                                                                                  │
│                                                                                                                                                                                                              │
│ Verificacion                                                                                                                                                                                                 │
│                                                                                                                                                                                                              │
│ 1. Compilar despues de cada fase - check Unity console via read_console                                                                                                                                      │
│ 2. Fase 0: Verificar que enums compilan y no rompen items existentes                                                                                                                                         │
│ 3. Fase 1: Agregar PlayerAttributes al prefab de player, verificar SyncVars funcionan                                                                                                                        │
│ 4. Fase 2: Testear combate entre 2 jugadores - verificar que criticos/overpower aparecen correctamente                                                                                                       │
│ 5. Fase 3: Crear ring SO de prueba, equipar/desequipar, verificar stats se recalculan                                                                                                                        │
│ 6. Fase 4: Usar debug para dar XP, verificar levelup y puntos                                                                                                                                                │
│ 7. Fase 5: Abrir panel de atributos, asignar puntos, verificar stats derivados cambian        







Implementation Summary                                                                                                                                                                                        
                                                                                                                                                                                                                
  Files Created (8)                                                                                                                                                                                             
  ┌───────────────────────────────────────────────────────────┬──────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
  │                           File                            │                                                   Purpose                                                    │                                  
  ├───────────────────────────────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ 1_Data/Definitions/CombatResult.cs                        │ DamageResultType enum + CombatResult struct                                                                  │
  ├───────────────────────────────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ 1_Data/ScriptableObjects/Core/AttributeConfig.cs          │ ScriptableObject with all scaling formulas (STR/AGI/INT/WIS/CON, overpower, crit, leveling)                  │
  ├───────────────────────────────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ 2_Simulation/Entities/Player/PlayerAttributes.cs          │ NetworkBehaviour with SyncVars for level, XP, 5 primary attributes, derived stats, equipment bonuses         │
  ├───────────────────────────────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ 2_Simulation/Combat/Core/CombatCalculator.cs              │ Static server-side calculator for damage (attribute scaling, evasion, crit/overpower, lifesteal) and healing │
  ├───────────────────────────────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ 2_Simulation/World/XPRewardSystem.cs                      │ XPRewardConfig SO + XPRewardSystem MonoBehaviour for event-driven XP distribution                            │
  ├───────────────────────────────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ 3_Presentation/UI/Controllers/AttributePanelController.cs │ Attribute allocation panel (toggle with N key), +/- buttons, XP bar, derived stats preview                   │
  ├───────────────────────────────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ 3_Presentation/UI/Views/AttributePanel.uxml               │ UXML layout for attribute panel                                                                              │
  ├───────────────────────────────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ 3_Presentation/UI/Styles/AttributePanel.uss               │ Styles for attribute panel                                                                                   │
  └───────────────────────────────────────────────────────────┴──────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
  Files Modified (15)
  ┌──────────────────────────────────┬──────────────────────────────────────────────────────────────────────────────────────────────────────┐
  │               File               │                                               Changes                                                │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ ItemEnums.cs                     │ +Ring1/Ring2 slots, +13 StatType entries, extended StatModifier.ToString()                           │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ PlayerStats.cs                   │ Added PlayerAttributes ref, TakeDamage overload with DamageResultType, mana regen includes WIS bonus │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ PlayerClassManager.cs            │ Notifies PlayerAttributes.RecalculateDerivedStats() on class change                                  │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ EquipmentManager.cs              │ +Ring1/Ring2 SyncVars, full RecalculateStats overhaul aggregating all 17 stat types across 11 slots  │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ PlayerCombat.cs                  │ Deprecated CalculateFinalDamage() in favor of CombatCalculator                                       │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ TargetedLogic.cs                 │ Uses CombatCalculator for damage/heal with crit/overpower/lifesteal                                  │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ AOELogic.cs                      │ Uses CombatCalculator for all AOE hits                                                               │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ ChannelLogic.cs                  │ Uses CombatCalculator for channel tick damage                                                        │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ ConeLogic.cs                     │ Uses CombatCalculator for cone hits                                                                  │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ SelfAOELogic.cs                  │ Uses CombatCalculator for self-AOE hits                                                              │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ ProjectileController.cs          │ Uses CombatCalculator, added AbilityCategory tracking                                                │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ SkillshotLogic.cs                │ Passes AbilityCategory to ProjectileController                                                       │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ DamageTextManager.cs             │ Added overpower (orange), evade (grey), critheal (bright green) colors                               │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ CharacterPanelController.cs      │ Ring1/Ring2 slots, attribute labels, derived stats, sub-stats display                                │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ CharacterPanelUI.uxml            │ Ring slots, Primary Attributes section, Combat Stats section, Sub-Stats section                      │
  ├──────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ CharacterPanelDebugController.cs │ Added Ring1/Ring2 to debug panel                                                                     │
  └──────────────────────────────────┴──────────────────────────────────────────────────────────────────────────────────────────────────────┘
  Next Steps (in Unity Editor)

  1. Create an AttributeConfig ScriptableObject asset (Genesis > Core > Attribute Config)
  2. Assign it to the PlayerAttributes component on the player prefab
  3. Add PlayerAttributes component to the player prefab
  4. Create a XPRewardConfig asset if needed
  5. Set up a UIDocument for the AttributePanel UXML
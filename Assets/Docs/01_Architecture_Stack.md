## 1. ARQUITECTURA DE SOFTWARE

### 1.1 Principios Arquitectónicos

**DOGMAS INNEGOCIABLES:**

*   ✅ **Server Authority:** El servidor es la única fuente de verdad.
*   ✅ **No DontDestroyOnLoad:** Usamos Bootstrap Scene persistente.
*   ✅ **Assembly Definitions:** Aislamiento de dependencias y compilación incremental.
*   ✅ **Data-Driven Design:** ScriptableObjects + JSON para balanceo.
*   ✅ **Event-Driven Communication:** EventBus para desacoplar sistemas.
*   ✅ **Object Pooling:** OBLIGATORIO para proyectiles, VFX, entidades.

### 1.2 Estructura de Carpetas y Assemblies

```text
Assets/_Project/
├── 0_Core/                          [Genesis.Core.asmdef]
│   ├── Architecture/
│   │   ├── ServiceLocator.cs        // Registro global de managers
│   │   ├── EventBus.cs              // Pub/Sub pattern para eventos
│   │   ├── FSM/                     // Finite State Machine genérica
│   │   │   ├── StateMachine.cs
│   │   │   └── IState.cs
│   │   └── Patterns/
│   │       ├── Singleton.cs
│   │       └── ObjectPool.cs        // Generic pool<T>
│   │
│   ├── Networking/
│   │   ├── NetworkBootstrap.cs      // Inicializa FishNet
│   │   ├── ServerManager.cs         // Lógica de servidor (spawn, tick)
│   │   ├── ClientManager.cs         // Lógica de cliente (conexión)
│   │   └── RPCs/
│   │       ├── CombatRPC.cs         // Métodos [ServerRpc] de combate
│   │       └── MovementRPC.cs
│   │
│   ├── Persistence/
│   │   ├── NakamaManager.cs         // Conexión y Auth
│   │   ├── DataSerializer.cs        // JSON <-> Game Objects
│   │   └── SaveSystem.cs            // Auto-save loop
│   │
│   └── Utils/
│       ├── MathUtils.cs             // Distancias, ángulos
│       ├── Extensions.cs            // Vector3, Transform, etc.
│       └── LayerMasks.cs            // Constantes de capas
│
├── 1_Data/                          [Genesis.Data.asmdef]
│   ├── ScriptableObjects/
│   │   ├── Core/
│   │   │   ├── AbilityData.cs       // Definición de habilidades
│   │   │   ├── ClassData.cs         // Stats base por clase
│   │   │   ├── ItemData.cs          // Armas, armaduras, consumibles
│   │   │   └── StatusEffectData.cs  // Buffs/Debuffs
│   │   │
│   │   └── Configurations/
│   │       ├── GameSettings.cs      // Balance general
│   │       ├── ServerSettings.cs    // Tick rate, timeouts
│   │       └── NetworkSettings.cs   // Lag compensation params
│   │
│   ├── Databases/
│   │   ├── AbilityDatabase.cs       // Registry de todas las habilidades
│   │   ├── ItemDatabase.cs
│   │   └── NPCDatabase.cs
│   │
│   └── JSON/                        // Exportado desde Google Sheets
│       ├── abilities.json
│       ├── items.json
│       └── spawn_tables.json
│
├── 2_Simulation/                    [Genesis.Simulation.asmdef]
│   ├── Entities/
│   │   ├── Player/
│   │   │   ├── PlayerController.cs  // Movimiento + Input
│   │   │   ├── PlayerStats.cs       // HP, Mana, Shields [SyncVar]
│   │   │   ├── PlayerCombat.cs      // Lógica de habilidades
│   │   │   ├── PlayerInventory.cs   // Gestión de items
│   │   │   └── PlayerAnimator.cs    // Control de Animator
│   │   │
│   │   ├── NPC/
│   │   │   ├── NPCController.cs     // IA básica
│   │   │   └── NPCStats.cs
│   │   │
│   │   └── Shared/
│   │       ├── IDamageable.cs       // Interface
│   │       └── IInteractable.cs
│   │
│   ├── Combat/
│   │   ├── Core/
│   │   │   ├── AbilitySystem.cs     // Executor de habilidades
│   │   │   ├── DamageCalculator.cs  // Fórmulas de daño
│   │   │   └── CombatValidator.cs   // Chequeos server-side
│   │   │
│   │   ├── Abilities/               // Strategy Pattern
│   │   │   ├── AbilityLogic.cs      // Abstract base
│   │   │   ├── ProjectileAbility.cs
│   │   │   ├── MeleeAbility.cs
│   │   │   ├── AoEAbility.cs
│   │   │   ├── HealAbility.cs
│   │   │   └── BuffAbility.cs
│   │   │
│   │   ├── Projectiles/
│   │   │   ├── ProjectileController.cs  // Física + SphereCast
│   │   │   └── ProjectilePool.cs
│   │   │
│   │   └── StatusEffects/
│   │       ├── StatusEffectSystem.cs    // Manager por entidad
│   │       ├── EffectTypes.cs           // Enum + bitflags
│   │       └── Effects/
│   │           ├── StunEffect.cs
│   │           ├── SlowEffect.cs
│   │           ├── ShieldEffect.cs
│   │           └── ReflectEffect.cs
│   │
│   ├── Targeting/
│   │   ├── TargetingSystem.cs       // Raycast + validación
│   │   ├── CursorController.cs      // Cruz para ground targeting
│   │   └── TargetIndicator.cs       // Anillo visual en los pies
│   │
│   ├── Loot/
│   │   ├── LootSystem.cs            // Drop tables
│   │   ├── LootBag.cs               // Contenedor en el suelo
│   │   └── CorpseController.cs      // Full loot logic
│   │
│   └── World/
│       ├── ZoneController.cs        // SafeZones, PvP zones
│       ├── SpawnPoint.cs
│       └── TriggerLogic.cs
│
├── 3_Presentation/                  [Genesis.Presentation.asmdef]
│   ├── UI/
│   │   ├── Controllers/
│   │   │   ├── HUDController.cs     // Bind stats a UI Toolkit
│   │   │   ├── InventoryUIController.cs
│   │   │   ├── CastBarController.cs
│   │   │   └── FloatingTextController.cs
│   │   │
│   │   ├── Views/                   // .uxml files
│   │   │   ├── HUD.uxml
│   │   │   ├── Inventory.uxml
│   │   │   └── TargetFrame.uxml
│   │   │
│   │   └── Styles/                  // .uss files
│   │       └── MainStyle.uss
│   │
│   ├── VFX/
│   │   ├── ParticleControllers/
│   │   │   ├── SpellVFXController.cs
│   │   │   └── ImpactVFXController.cs
│   │   │
│   │   └── Shaders/                 // Shader Graph assets
│   │       ├── MagicAura.shadergraph
│   │       └── Dissolve.shadergraph
│   │
│   └── Audio/
│       ├── AudioManager.cs
│       └── SFXController.cs
│
├── 4_Bootstrap/                     [Genesis.Bootstrap.asmdef]
│   └── EntryPoint.cs                // Inicializa todo el juego
│
└── 5_Content/                       // NO tiene .asmdef (assets)
    ├── Prefabs/
    │   ├── Player/
    │   │   ├── Guerrero.prefab
    │   │   ├── Mago.prefab
    │   │   ├── Cazador.prefab
    │   │   └── Sacerdote.prefab
    │   │
    │   ├── Projectiles/
    │   │   ├── Fireball.prefab
    │   │   ├── Arrow.prefab
    │   │   └── IceBolt.prefab
    │   │
    │   └── VFX/
    │       ├── Explosion.prefab
    │       ├── HealingAura.prefab
    │       └── BuffShield.prefab
    │
    ├── Models/
    ├── Textures/
    ├── Audio/
    └── Resources/                   // Solo para data runtime
        └── Abilities/               // AbilityData assets
```

---

## 2. STACK TECNOLÓGICO

### 2.1 Core Stack

| Componente | Tecnología | Versión | Razón |
| :--- | :--- | :--- | :--- |
| **Engine** | Unity | 6.3 LTS | Estabilidad a largo plazo |
| **Networking** | FishNet | 4.x (Free) | Server-auth nativo, bajo garbage, comunidad activa |
| **Transport** | Tugboat (UDP) | Built-in | Incluido en FishNet, optimizado para MMO |
| **Persistence** | Nakama | 3.x (Self-Hosted) | Auth, Storage, Leaderboards |
| **UI** | UI Toolkit | Built-in U6 | Runtime performance superior a UGUI |
| **VFX** | Particle System | Built-in | VFX Graph es overkill para este scope |
| **Rendering** | URP | 17.x | Mobile-friendly, buen balance performance/quality |

### 2.2 Dependencias Externas

```json
// Packages/manifest.json (relevantes)
{
  "dependencies": {
    "com.unity.render-pipelines.universal": "17.0.3",
    "com.unity.textmeshpro": "3.2.0-pre.7",
    "com.unity.addressables": "2.0.8",
    "com.heroiclabs.nakama-unity": "3.10.0"
  }
}
```

**FishNet:** Importar desde Asset Store o GitHub Release.

### 2.3 Configuración de Proyecto

**Physics Settings:**
```text
Fixed Timestep: 0.02 (50Hz - para server tick)
Default Contact Offset: 0.01
Queries Hit Triggers: OFF (evitar colisiones con zonas)
```

**Quality Settings (Preset: Medium para target):**
```text
V-Sync: OFF (cliente maneja su propio framerate)
Anti-Aliasing: SMAA
Shadow Distance: 50m
Shadow Cascades: 2
LOD Bias: 1.5
```

**Layers (32 disponibles - usar estos):**
```text
0:  Default
3:  Player
6:  Enemy
7:  Projectile
8:  Environment
9:  SafeZone
10: Loot
11: Interactable
31: IgnoreRaycast (UI)
```

**Collision Matrix (LayerMasks.cs):**
```csharp
public static class Layers {
    public const int Player = 3;
    public const int Enemy = 6;
    public const int Projectile = 7;
    public const int Environment = 8;
    public const int SafeZone = 9;
    public const int Loot = 10;
    
    // Máscaras combinadas
    public static readonly int Damageable = (1 << Player) | (1 << Enemy);
    public static readonly int Walkable = (1 << Environment);
    public static readonly int TargetingMask = Damageable;
}
```

**Desactivar colisiones:**
*   Player <-> Player (evitar push, se maneja con controller)
*   Projectile <-> Projectile
*   SafeZone <-> Todo (es solo trigger)

---

## 3. JERARQUÍA DE RUNTIME

### 3.1 Scene: `Bootstrap` (Persistent - NEVER UNLOAD)

```text
[BOOTSTRAP SCENE]
├── [===MANAGERS===]
│   ├── NetworkManager               [FishNet.NetworkManager]
│   │   ├── ServerManager            (Component)
│   │   ├── ClientManager            (Component)
│   │   ├── TransportManager         (Tugboat)
│   │   └── TimeManager              (Built-in)
│   │
│   ├── GameManager                  [ServiceLocator host]
│   │   └── (Registra todos los servicios)
│   │
│   ├── NakamaManager                [Persistence]
│   │   └── (Login, Storage, Auto-save)
│   │
│   ├── AudioManager                 [FMOD o Unity Audio]
│   │
│   └── ObjectPoolManager            [Pools centralizados]
│       ├── ProjectilePool<Fireball>
│       ├── ProjectilePool<Arrow>
│       ├── VFXPool<Explosion>
│       └── FloatingTextPool
│
├── [===UI ROOT===]
│   ├── UIDocument_Main              [UI Toolkit - fullscreen]
│   │   └── (Contiene HUD, Inventory, Chat)
│   │
│   ├── WorldSpace_Canvas            [Canvas - WorldSpace]
│   │   └── (Para HealthBars y FloatingText sobre entidades)
│   │
│   └── EventSystem                  [Input System]
│
├── [===WORLD CONTAINER===]
│   └── (Aquí FishNet carga escenas aditivas - Vacío en edit mode)
│
└── MainCamera                       [Cinemachine Brain]
    └── (Follow del jugador local)
```

**REGLAS DE ORO:**
*   ❌ NUNCA uses `DontDestroyOnLoad` manualmente.
*   ✅ Todo manager vive en Bootstrap.
*   ✅ Las escenas de mapa se cargan ADITIVAS.

### 3.2 Scene: `Map_OpenWorld_01` (Additive - Spawned by Server)

```text
[MAP_OPENWORLD_01 SCENE]
├── [===ENVIRONMENT===]              (Static Batching ON)
│   ├── Terrain
│   ├── Trees_LODGroup
│   ├── Rocks_StaticBatch
│   ├── Buildings
│   └── Lighting
│       ├── DirectionalLight
│       └── ReflectionProbes
│
├── [===LOGIC===]
│   ├── SpawnPoints                  (Empty con NetworkTransform)
│   │   ├── PlayerSpawn_1
│   │   ├── PlayerSpawn_2
│   │   └── ...
│   │
│   ├── Zones
│   │   ├── SafeZone_Town            [BoxCollider - isTrigger]
│   │   │   └── ZoneController.cs
│   │   │
│   │   └── PvPZone_Arena            [BoxCollider - isTrigger]
│   │       └── ZoneController.cs
│   │
│   └── NPCSpawners
│       ├── Spawner_Wolves           [Spawn logic server-only]
│       └── Spawner_Bandits
│
└── [===DYNAMIC===]                  (VACÍO en editor)
    └── (Jugadores y NPCs spawneados por FishNet en runtime)
```
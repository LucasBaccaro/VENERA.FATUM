# Mobs & Enemy Animation System

## Enemy Architecture

### Core Components (on root GameObject)
- `NetworkObject` (FishNet)
- `EnemyMob` — AI + combat logic, references `EnemyData` SO via `_data` field
- `NavMeshAgent` — pathfinding
- `NetworkTransform` — position sync
- `TargetingSystem` — target acquisition
- `CapsuleCollider` — radius 0.37, height 2, center Y=1
- Layer: **6 ("Enemy")**

### EnemyMob.cs (`2_Simulation/Entities/Enemy/EnemyMob.cs`)
- Server-authoritative AI: Idle → Patrol → Aggro → Attack → Kiting → Return
- Archetypes: `Melee`, `Ranged`, `Support` (enum `EnemyArchetype`)
- Roles: `Grunt`, `Tank`, `Boss` (enum `EnemyRole`)
- Telegraph system: Anticipation → AttackAnim (damage at specific frame) → Recovery
- Per-prefab attack config: `_damageFrame1`, `_damageFrame2`, `_hasSecondAttack` on EnemyMob
- Fighter alternates Attack/Attack2 triggers; CutThroat uses Attack only
- **Cached Animator** (`_cachedAnimator`): initialized in `OnStartServer`, lazy fallback in RPCs for clients
- **Locomotion animation**: `LateUpdate()` calculates speed from position delta, sets `Speed` float on Animator
- Animator triggers used: `Attack`, `Anticipation`, `Die`
- Animator float used: `Speed` (via `SpeedHash` static)
- FishNet pooling: `OnStartServer` resets ALL state (objects are reused, not destroyed)
- SyncVars: `_currentHealth`, `_isDead`

### EnemyData SO (`1_Data/ScriptableObjects/Enemies/EnemyData.cs`)
Fields: EnemyTag, DisplayName, MaxHealth, MinDamage/MaxDamage, AttackRange, AttackCooldown, DetectionRange, MoveSpeed, IsRanged, ProjectilePrefab, ProjectileSpeed, PreferredDistance, Role, Archetype, LeashRange, PatrolRadius, PatrolWaitTime, AnticipationDuration, RecoveryDuration, KiteThreshold, KiteDistance, KitingProjectilePrefab, KitingProjectileSpeed, SupportScanRange, HealAmount, HealCooldown, KnockbackForce, KnockbackDuration, HitSound, DeathSound, GoldReward, LootTable.

### EnemySpawner.cs (`2_Simulation/Entities/Enemy/EnemySpawner.cs`)
- Fields: `_enemyPrefab` (GameObject), `_enemyData` (EnemyData), `_maxAlive`, `_respawnDelay`, `_spawnRadius`, `_initialCount`
- Server-only: waits for `ServerManager.Started`, then spawns initial batch
- Respawn loop maintains `_maxAlive` count with `_respawnDelay`

---

## Bandit Enemies (replacing Grunts)

### FBX Model
- Path: `5_Content/Models/Enemies/Bandits/Bandits_Base.fbx` (17.6 MB)
- GUID: `166f79434f6c7b24fbff1590f5e3820d`
- Single modular model with skeleton, used by all 3 bandit types

### FBX Hierarchy (direct children of root, relevant for visuals)
**Body meshes** (always active): Belt, Chest, Feet, Hands, Head, Mask, Pants, Skirt1
**Toggleable accessories**: Hood, ChestPads, Hands_ACC, Skirt2, CrossBelt, ShoulderPads
**Hairstyles** (pick 1 of 3): Hairstyle_1, Hairstyle_2, Hairstyle_3
**Weapons**: R_Dagger, L_Dagger, Cimitar_Hand, Cimitar_Belt, CrossBow

### Animation Takes (38 total, some duplicated with `Bandit_Base_Rig|` prefix)

| Category | Clips |
|---|---|
| Fighter idle/standby | Fighter_Standby_Idle (80f, loop), Fighter_Standby_Siting (80f), Fighter_Standby_Siting_Alert, Fighter_Standby_Siting_Stand, Fighter_Standby_ToCombat |
| Fighter combat | Fighter_Combat_Walk (17f, loop), Fighter_Combat_Idle (29f, loop), Fighter_Combat_Attack01 (30f), Fighter_Combat_Attack02 (30f) |
| Fighter death | Fighter_Death01 (36f), Fighter_Death02 (15f) |
| CutThroat | CutThroat_Combat_Idle (29f, loop), CutThroat_Combat_Attack01 (30f) |
| Crossbow | Crossbow_Combat_Idle (40f, loop), Crossbow_Combat_Walk (17f, loop), Crossbow_Combat_Attack01 (16f), Crossbow_Combat_Death (31f), Crossbow_Standby_Idle (80f), Crossbow_Standby_Alert |

**Note**: Fighter_Combat_Walk and Fighter_Combat_Idle only exist with `Bandit_Base_Rig|` prefix. CutThroat has NO walk/death clips — borrows from Fighter.

### AnimatorControllers (`5_Content/Animations/Enemies/Bandits/`)

- **Parameters**: `Speed` (float), `Attack` (trigger), `Attack2` (trigger), `Anticipation` (trigger), `Die` (trigger)
- **States**: Locomotion (BlendTree: Speed 0→idle, Speed >0→walk), Attack, Attack2 (Fighter only), Anticipation (attack clip at 0.3x speed), Die
- **Transitions**: AnyState→Attack/Attack2/Anticipation/Die via triggers, Attack/Attack2/Anticipation→Locomotion via exit time 0.9, Die has no exit

| Controller | GUID | Idle | Walk | Attack | Attack2 | Death |
|---|---|---|---|---|---|---|
| AC_Bandit_Fighter | `724a7385301d147458eecd53c75ad5b1` | Fighter_Standby_Idle | Fighter_Combat_Walk | Fighter_Combat_Attack01 (dmg frame 20) | Fighter_Combat_Attack02 (dmg frame 15) | Fighter_Death01 |
| AC_Bandit_CutThroat | `877b69ac183914e7ca26227b98be4c9f` | Fighter_Standby_Idle | Fighter_Combat_Walk | CutThroat_Combat_Attack01 (dmg frame 15) | — | Fighter_Death01 |
| AC_Bandit_Crossbow | `4fa901361fb8d47dba45339ecf88793d` | Crossbow_Combat_Idle | Crossbow_Combat_Walk | Crossbow_Combat_Attack01 | — | Crossbow_Combat_Death |

### EnemyVisualRandomizer.cs (`2_Simulation/Entities/Enemy/EnemyVisualRandomizer.cs`)
- `NetworkBehaviour` with `SyncVar<int> _visualSeed`
- Server generates random seed on `OnStartServer`
- `ApplyVisuals(seed)`: uses `System.Random(seed)` for deterministic randomization
- Toggles 6 accessories (50/50 each): Hood, ChestPads, Hands_ACC, Skirt2, CrossBelt, ShoulderPads
- Picks 1 of 3 hairstyles
- Weapons are NOT randomized — set active/inactive per prefab variant

### Prefabs (`5_Content/Prefabs/Enemies/Bandits/`)

| Prefab | GUID | Root fileID | Animator | EnemyData | Active Weapons | Inactive Weapons |
|---|---|---|---|---|---|---|
| Bandit_Fighter | `fa183507cb13c4cfaa518e4bc1dae655` | `3359724325573224909` | AC_Bandit_Fighter | Enemy_GruntMelee | Cimitar_Hand | R_Dagger, L_Dagger, Cimitar_Belt, CrossBow |
| Bandit_CutThroat | `e13d327198c8f495a86ba9790799133c` | `5906278121376584797` | AC_Bandit_CutThroat | Enemy_GruntMelee | R_Dagger, L_Dagger | Cimitar_Hand, Cimitar_Belt, CrossBow |
| Bandit_Crossbow | `0af2f1be5ca064e55a42b5fd10b37b5f` | `2832003618188000937` | AC_Bandit_Crossbow | Enemy_GruntRanged | CrossBow | R_Dagger, L_Dagger, Cimitar_Hand, Cimitar_Belt |

Prefab structure: Root (components listed above) → Child "Bandits_Base" (FBX instance with Animator)

### Spawner Mapping (Chunk_0_0.unity)

| Spawner | Old Prefab | New Prefab |
|---|---|---|
| Spawner_GruntMelee | Grunt_Enemy_Melee (`5c7ad08d...`) | Bandit_Fighter (`fa183507...`) |
| Spawner_GruntRanged | Grunt_Enemy_Range (`3efc04c5...`) | Bandit_Crossbow (`0af2f1be...`) |
| Spawner_GruntSupport | Grunt_Enemy_Support (`0490883c...`) | Bandit_CutThroat (`e13d3271...`) |

### Grunt Data Assets (reused by Bandits)

| Asset | GUID | HP | Dmg | Range | Speed | Archetype |
|---|---|---|---|---|---|---|
| Enemy_GruntMelee | `318cc8598c8084fdab9f1206434c5964` | 150 | 8-14 | 2.5 | 3.5 | Melee |
| Enemy_GruntRanged | `7090fd7b69c234f4ea2a4fdec5795066` | 80 | 12-20 | 12 | 3.5 | Ranged |
| Enemy_GruntSupport | `d6ca3f5f616ef4ec287fed9dd8e25a1a` | 80 | 3-6 | 8 | 4 | Support |

---

## Editor Tooling

### BanditAnimationSetup.cs (`1_Data/Editor/BanditAnimationSetup.cs`)
Menu items under `Genesis/Tools/`:
1. **Bandit - 1. Discover Animations** — Logs FBX takes + hierarchy
2. **Bandit - 2. Setup Clips** — Copies `defaultClipAnimations` → `clipAnimations`, sets loop on idle/walk clips, reimports FBX
3. **Bandit - 3. Create AnimatorControllers** — Generates 3 controllers with BlendTree + states
4. **Bandit - 4. Create Prefabs** — Creates 3 prefabs with all components, wires EnemyVisualRandomizer refs, sets FishNet AssetPathHash

### Genesis.Editor.asmdef
References: Genesis.Data, Genesis.Core, Genesis.Simulation, **FishNet.Runtime** (added for prefab creation)

---

## Key Gotchas
- FBX clip names: some only exist with `Bandit_Base_Rig|` prefix (Fighter_Combat_Walk, Fighter_Combat_Idle). The FindClip helper tries both.
- Layer name is `"Enemy"` (not "Enemies") — layer index 6
- FishNet `NetworkObject.AssetPathHash` must be set manually when creating prefabs via code (uses FNV-1a 64-bit hash of sanitized asset path+name)
- Scene prefab references need root GameObject `fileID` from inside the .prefab file, NOT the fileID from DefaultPrefabObjects.asset
- `TradeSession.cs` had a pre-existing `uint`→`int` type mismatch on `ObjectId` dictionary (fixed)

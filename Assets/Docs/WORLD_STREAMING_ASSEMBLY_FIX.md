# World Streaming System - Assembly Definition Fix

## Problem

Initial implementation violated assembly dependency rules:
```
Core -> Data -> Simulation -> Presentation
```

`PlayerSpawnManager` (in Core) was trying to reference:
- `Genesis.Data` (WorldDatabase, ChunkData)
- `Genesis.Simulation.World` (PlayerChunkTracker, PlayerState)

**Error:**
```
CS0234: The type or namespace name 'Data' does not exist in the namespace 'Genesis'
CS0234: The type or namespace name 'Simulation' does not exist in the namespace 'Genesis'
```

## Solution: Dependency Inversion

Used the **Dependency Inversion Principle** to allow Core to stay independent while Simulation provides the implementation.

### Architecture

```
┌─────────────────────────────────────────────────────┐
│                   0_Core                            │
│  ┌───────────────────────────────────────────┐     │
│  │  ISpawnPositionProvider (Interface)       │     │
│  │    + GetSpawnPosition(): Vector3          │     │
│  └───────────────────────────────────────────┘     │
│                      ▲                              │
│                      │ implements                   │
│  ┌───────────────────────────────────────────┐     │
│  │  PlayerSpawnManager                       │     │
│  │    - Uses ISpawnPositionProvider via      │     │
│  │      ServiceLocator (no direct reference) │     │
│  └───────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────┘
                       ▲
                       │ registers
                       │
┌─────────────────────────────────────────────────────┐
│               2_Simulation                          │
│  ┌───────────────────────────────────────────┐     │
│  │  WorldSpawnProvider                       │     │
│  │    implements ISpawnPositionProvider      │     │
│  │    - Uses WorldDatabase                   │     │
│  │    - Returns chunk spawn positions        │     │
│  └───────────────────────────────────────────┘     │
│                                                     │
│  ┌───────────────────────────────────────────┐     │
│  │  PlayerSpawnHandler                       │     │
│  │    - Listens for NetworkObject spawns     │     │
│  │    - Adds PlayerChunkTracker              │     │
│  │    - Adds PlayerState                     │     │
│  │    - Calls ServerSceneHandler             │     │
│  └───────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────┘
                       ▲
                       │ initializes
                       │
┌─────────────────────────────────────────────────────┐
│               4_Bootstrap                           │
│  ┌───────────────────────────────────────────┐     │
│  │  WorldStreamingBootstrap                  │     │
│  │    - Creates WorldSpawnProvider           │     │
│  │    - Registers it as ISpawnPositionProvider│    │
│  │    - Spawns PlayerSpawnHandler            │     │
│  └───────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────┘
```

## New Files

### 1. ISpawnPositionProvider (Core)
**Path:** `Assets/_Project/0_Core/Networking/ISpawnPositionProvider.cs`

```csharp
public interface ISpawnPositionProvider
{
    Vector3 GetSpawnPosition();
}
```

**Purpose:** Contract that allows Core to request spawn positions without knowing about WorldDatabase.

### 2. WorldSpawnProvider (Simulation)
**Path:** `Assets/_Project/2_Simulation/World/WorldSpawnProvider.cs`

```csharp
public class WorldSpawnProvider : ISpawnPositionProvider
{
    private WorldDatabase _worldDB;

    public Vector3 GetSpawnPosition() {
        ChunkData chunk = _worldDB.GetRandomStartingChunk();
        return chunk.SpawnPositions[Random.Range(0, chunk.SpawnPositions.Length)];
    }
}
```

**Purpose:** Implements spawn position selection using WorldDatabase.

### 3. PlayerSpawnHandler (Simulation)
**Path:** `Assets/_Project/2_Simulation/World/PlayerSpawnHandler.cs`

```csharp
public class PlayerSpawnHandler : MonoBehaviour
{
    void OnNetworkObjectSpawned(NetworkObject netObj) {
        // Add PlayerChunkTracker
        // Add PlayerState
        // Move to chunk scene
    }
}
```

**Purpose:** Handles post-spawn initialization (adding components, scene migration).

## Modified Files

### PlayerSpawnManager.cs (Core)
**Before:**
```csharp
using Genesis.Data; // ❌ Assembly violation
using Genesis.Simulation.World; // ❌ Assembly violation

WorldDatabase worldDB = ServiceLocator.Instance.Get<WorldDatabase>(); // ❌
ChunkData chunk = worldDB.GetRandomStartingChunk(); // ❌
```

**After:**
```csharp
using Genesis.Core; // ✅ Same assembly

var provider = ServiceLocator.Instance.Get<ISpawnPositionProvider>(); // ✅
Vector3 pos = provider.GetSpawnPosition(); // ✅
```

### WorldStreamingBootstrap.cs (Bootstrap)
**Added:**
```csharp
// Register spawn provider
WorldSpawnProvider spawnProvider = new WorldSpawnProvider(worldDatabase);
ServiceLocator.Instance.Register<ISpawnPositionProvider>(spawnProvider);

// Spawn PlayerSpawnHandler
GameObject spawnHandler = Instantiate(playerSpawnHandlerPrefab);
```

## Data Flow

### 1. Initialization (Bootstrap)
```
WorldStreamingBootstrap.Awake():
  1. Load WorldDatabase from Resources
  2. Initialize WorldDatabase
  3. Register WorldDatabase with ServiceLocator
  4. Create WorldSpawnProvider(worldDatabase)
  5. Register WorldSpawnProvider as ISpawnPositionProvider
```

### 2. Player Spawn Request (Core)
```
PlayerSpawnManager.TrySpawnPlayer(conn):
  1. Get ISpawnPositionProvider from ServiceLocator
  2. Call provider.GetSpawnPosition() → returns Vector3
  3. Instantiate player at position
  4. NetworkManager.Spawn(player, conn)
```

### 3. Post-Spawn Initialization (Simulation)
```
PlayerSpawnHandler.OnNetworkObjectSpawned(netObj):
  1. Add PlayerChunkTracker component
  2. Add PlayerState component
  3. Calculate ChunkCoordinate from position
  4. Call ServerSceneHandler.MovePlayerToChunkScene()
```

## Benefits of This Approach

1. **Assembly Independence:** Core doesn't know about Data or Simulation
2. **Testability:** ISpawnPositionProvider can be mocked for tests
3. **Flexibility:** Can swap spawn logic without modifying Core
4. **Separation of Concerns:** Core handles networking, Simulation handles game logic

## Configuration Steps (Updated)

### 1. Create Prefabs
You now need **3 prefabs** (not 2):

#### a. ChunkLoaderManager.prefab
```
Assets/_Project/5_Content/Prefabs/World/ChunkLoaderManager.prefab
Components:
  - ChunkLoaderManager.cs
```

#### b. ServerSceneHandler.prefab
```
Assets/_Project/5_Content/Prefabs/World/ServerSceneHandler.prefab
Components:
  - ServerSceneHandler.cs
  - NetworkObject (FishNet)
    - Is Global: ✅
```

#### c. PlayerSpawnHandler.prefab (NEW)
```
Assets/_Project/5_Content/Prefabs/World/PlayerSpawnHandler.prefab
Components:
  - PlayerSpawnHandler.cs
```

### 2. Configure WorldStreamingBootstrap
```
Open: Assets/_Project/4_Bootstrap/Bootstrap.unity
GameObject: WorldStreamingBootstrap

Inspector:
  - World Database: WorldDatabase.asset (from Resources/Databases/)
  - Chunk Loader Prefab: ChunkLoaderManager.prefab
  - Server Scene Handler Prefab: ServerSceneHandler.prefab
  - Player Spawn Handler Prefab: PlayerSpawnHandler.prefab ← NEW
```

### 3. Player Prefab (Optional)
You can optionally add PlayerChunkTracker and PlayerState directly to your player prefab to avoid runtime AddComponent calls:

```
Assets/_Project/5_Content/Prefabs/Player.prefab
Add Components:
  - PlayerChunkTracker.cs
  - PlayerState.cs
```

If you do this, PlayerSpawnHandler will skip adding them.

## Testing

### Verify Assembly Boundaries
```bash
# Should compile without errors
Unity > Assets > Open C# Project
# Check that Core doesn't reference Data or Simulation
```

### Verify Spawn System
```
1. Start server
2. Connect client
3. Check Console:
   - [WorldStreamingBootstrap] WorldSpawnProvider registered
   - [WorldSpawnProvider] Providing spawn position from chunk X: (pos)
   - [PlayerSpawnHandler] Added PlayerChunkTracker to Player(Clone)
   - [PlayerSpawnHandler] Added PlayerState to Player(Clone)
   - [ServerSceneHandler] Moved Player to scene Chunk_X_Y
```

## Troubleshooting

### Error: "ISpawnPositionProvider not found in ServiceLocator"
**Cause:** WorldStreamingBootstrap not running or failed to register provider.

**Fix:**
1. Ensure WorldStreamingBootstrap component is in Bootstrap scene
2. Check WorldDatabase is assigned
3. Verify WorldDatabase.asset is in Resources/Databases/ folder

### Error: "ServerSceneHandler not found"
**Cause:** ServerSceneHandler prefab not spawned or not assigned in Bootstrap.

**Fix:**
1. Check ServerSceneHandlerPrefab is assigned in WorldStreamingBootstrap
2. Verify Bootstrap scene loads before player spawns
3. Check Console for "[WorldStreamingBootstrap] ServerSceneHandler spawned"

### Players spawn at (0,0,0)
**Cause:** WorldSpawnProvider returning zero vector.

**Fix:**
1. Verify WorldDatabase has chunks with IsStartingChunk = true
2. Check ChunkData.SpawnPositions array is not empty
3. Review Console for WorldDatabase initialization logs

## Summary

The assembly violation has been fixed using **Dependency Inversion**:
- Core defines the **interface** (ISpawnPositionProvider)
- Simulation provides the **implementation** (WorldSpawnProvider)
- Bootstrap **registers** the implementation via ServiceLocator
- Core uses the interface without knowing the concrete type

This maintains clean architecture and respects Unity assembly definition boundaries.

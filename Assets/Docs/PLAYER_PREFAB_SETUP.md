# Player Prefab Setup - World Streaming System

## Required Components on Player Prefab

Para que el sistema de World Streaming funcione correctamente, tu **Player Prefab** debe tener estos componentes:

### 1. PlayerChunkTracker
**Path:** `Genesis.Simulation.World.PlayerChunkTracker`

**Función:**
- Detecta cuando el jugador cambia de chunk
- Dispara evento `PLAYER_CHUNK_CHANGED` para cargar/descargar chunks
- Notifica al servidor para migración de escena

**Configuración:** Ninguna (funciona automáticamente)

---

### 2. PlayerState
**Path:** `Genesis.Simulation.World.PlayerState`

**Función:**
- Almacena si el jugador está en safe zone (SyncVar)
- Actualiza UI cuando cambia de zona
- Usado por CombatValidator para bloquear daño

**Configuración:** Ninguna (funciona automáticamente)

---

### 3. PlayerSpawnHandler
**Path:** `Genesis.Simulation.World.PlayerSpawnHandler`

**Función:**
- Mueve al jugador a la escena de chunk correcta al spawnearse
- Solo se ejecuta en el servidor (OnStartServer)
- Verifica que PlayerChunkTracker y PlayerState existan

**Configuración:** Ninguna (funciona automáticamente)

---

## Cómo Configurar el Player Prefab

### Opción 1: Agregar manualmente (Recomendado)

```
1. Abre tu Player Prefab en el editor
2. Add Component > PlayerChunkTracker
3. Add Component > PlayerState
4. Add Component > PlayerSpawnHandler
5. Save Prefab
```

**Ventajas:**
- Los componentes están siempre presentes
- No hay AddComponent() en runtime
- Mejor rendimiento
- Menos logs de debug

---

### Opción 2: Los componentes se verifican en PlayerSpawnHandler

Si no agregas los componentes manualmente:
- `PlayerSpawnHandler` tiene `[RequireComponent]` que los solicita
- Unity agregará los componentes automáticamente al prefab
- Recibirás un warning en consola

**Recomendado:** Agrega manualmente para evitar warnings.

---

## Verificación

### 1. Inspeccionar Player Prefab
```
Assets/_Project/5_Content/Prefabs/Player.prefab

Componentes requeridos:
✅ NetworkObject (FishNet)
✅ PlayerStats (ya existente)
✅ PlayerChunkTracker (NUEVO)
✅ PlayerState (NUEVO)
✅ PlayerSpawnHandler (NUEVO)
```

### 2. Console Logs Esperados (al conectar)
```
[WorldStreamingBootstrap] WorldDatabase initialized
[WorldStreamingBootstrap] WorldSpawnProvider registered
[SpawnManager] 🟢 Spawned player X in chunk Chunk(0, 0) at (128, 0, 128)
[PlayerSpawnHandler] Player spawned in chunk Chunk(0, 0)
[ChunkLoader] Loaded chunk Chunk(0, 0) - Scene: Chunk_0_0 (Client)
[ServerSceneHandler] Moved Player(Clone) to scene Chunk_0_0
```

### 3. Verificar en Runtime
```
1. Start server + client
2. Hierarchy > buscar "Player(Clone)"
3. Inspector > verificar componentes:
   - PlayerChunkTracker
   - PlayerState
   - PlayerSpawnHandler
```

---

## Troubleshooting

### Error: "Missing Component: PlayerChunkTracker"
**Causa:** Componente no está en el prefab.

**Fix:**
```
Open Player Prefab > Add Component > PlayerChunkTracker > Save
```

### Error: "ServerSceneHandler not found"
**Causa:** ServerSceneHandler no se spawneó en Bootstrap.

**Fix:**
```
1. Verify Bootstrap scene has WorldStreamingBootstrap
2. Verify ServerSceneHandlerPrefab is assigned
3. Check Console for "[WorldStreamingBootstrap] ServerSceneHandler spawned"
```

### Player no se mueve a escena de chunk
**Causa:** PlayerSpawnHandler no está en el prefab.

**Fix:**
```
Open Player Prefab > Add Component > PlayerSpawnHandler > Save
```

### Console spam: "Player X entered/exited Safe Zone"
**Causa:** Normal si hay triggers de safe zone en el chunk.

**No es error:** Es el comportamiento esperado cuando el jugador camina por zonas.

---

## Orden de Inicialización

```
1. Bootstrap.Awake()
   - WorldDatabase.Initialize()
   - WorldSpawnProvider registered

2. Bootstrap.Start()
   - ServerSceneHandler spawned (server only)
   - ChunkLoaderManager spawned (client and server)

3. Player Connects
   - PlayerSpawnManager.TrySpawnPlayer()
   - Instantiate(playerPrefab, spawnPosition)
   - NetworkManager.Spawn(player)

4. Player.OnStartServer()
   - PlayerSpawnHandler.OnStartServer()
   - ServerSceneHandler.MovePlayerToChunkScene()

5. Player.OnStartClient() (owner)
   - PlayerChunkTracker.OnStartClient()
   - EventBus: PLAYER_CHUNK_CHANGED
   - ChunkLoaderManager loads 9-slice grid
```

---

## Diferencias con Versión Anterior (Assembly Fix)

### Antes (No compila - Assembly violation)
```csharp
// PlayerSpawnManager agregaba componentes directamente
player.AddComponent<PlayerChunkTracker>();  // ❌ Assembly violation
player.AddComponent<PlayerState>();         // ❌ Assembly violation
```

### Ahora (Compila correctamente)
```csharp
// Los componentes están en el prefab
// PlayerSpawnHandler (en prefab) hace la migración de escena
[RequireComponent(typeof(PlayerChunkTracker))]  // ✅
[RequireComponent(typeof(PlayerState))]         // ✅
public class PlayerSpawnHandler : NetworkBehaviour
```

**Resultado:** Respeta assembly definitions (Core no referencia Simulation).

---

## Summary

1. **Agrega 3 componentes** a tu Player Prefab:
   - PlayerChunkTracker
   - PlayerState
   - PlayerSpawnHandler

2. **Asigna prefabs** en WorldStreamingBootstrap (Bootstrap scene):
   - ChunkLoaderPrefab
   - ServerSceneHandlerPrefab

3. **Crea chunks** y configura ChunkData con SceneName

4. **Run:** Tools > Add Chunk Scenes to Build Settings

5. **Test:** Start server + client, verify chunks load

---

**Next:** Follow `WORLD_STREAMING_NO_ADDRESSABLES.md` for chunk creation workflow.

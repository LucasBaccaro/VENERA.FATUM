# World Streaming System - Guía de Implementación Manual

## ✅ Código Ya Implementado

El sistema de World Streaming (Scene Stacking) ya está completamente implementado en código. Este documento te guía en la **configuración manual** que debes hacer en Unity.

---

## 📋 Resumen del Sistema

- **Chunks:** 256x256m cada uno
- **Carga dinámica:** 9-slice grid (chunk actual + 8 vecinos)
- **Safe Zones:** Triggers físicos en Layer 9 (independientes de chunks)
- **Networking:** Server authority (FishNet)
- **Scene Loading:** Unity SceneManager nativo (sin Addressables)

---

## 🔧 PASO 1: Configurar Layer 9 "SafeZone"

### 1.1 Crear Layer
```
Edit > Project Settings > Tags and Layers

Layers:
  Layer 9: "SafeZone"
```

### 1.2 Configurar Collision Matrix
```
Edit > Project Settings > Physics > Layer Collision Matrix

Desmarcar Layer 9 (SafeZone) para TODAS las capas:
  ❌ Default (0)
  ❌ Player (3)
  ❌ Enemy (6)
  ❌ Environment (8)
  ❌ SafeZone (9) ← También self-collision!
```

**¿Por qué?** Los triggers no deben bloquear movimiento, solo detectar entrada/salida.

---

## 🎮 PASO 2: Configurar Player Prefab

Abre tu **Player Prefab** y agrega estos 3 componentes:

### 2.1 PlayerChunkTracker
```
Select Player Prefab > Add Component > PlayerChunkTracker

Función:
- Detecta cuando el player cambia de chunk
- Dispara evento para cargar/descargar chunks
- Notifica al servidor para migración de escena

Configuración: Ninguna (automático)
```

### 2.2 PlayerState
```
Select Player Prefab > Add Component > PlayerState

Función:
- Almacena si el player está en safe zone (SyncVar)
- Actualiza UI cuando cambia de zona
- Bloquea combate en safe zones

Configuración: Ninguna (automático)
```

### 2.3 PlayerSpawnHandler
```
Select Player Prefab > Add Component > PlayerSpawnHandler

Función:
- Mueve al player a la escena de chunk correcta al spawnearse
- Solo se ejecuta en servidor

Configuración: Ninguna (automático)
```

**IMPORTANTE:** Save Prefab después de agregar los componentes.

**Resultado esperado:**
```
Player.prefab:
✅ NetworkObject (ya existe)
✅ PlayerStats (ya existe)
✅ PlayerChunkTracker (NUEVO)
✅ PlayerState (NUEVO)
✅ PlayerSpawnHandler (NUEVO)
```

---

## 🏗️ PASO 3: Crear Prefabs de Managers

### 3.1 ChunkLoaderManager Prefab

```
1. Hierarchy > Create Empty > Name: "ChunkLoaderManager"
2. Add Component > ChunkLoaderManager
3. Drag to: Assets/_Project/5_Content/Prefabs/World/ChunkLoaderManager.prefab
4. Delete from Hierarchy
```

### 3.2 ServerSceneHandler Prefab

```
1. Hierarchy > Create Empty > Name: "ServerSceneHandler"
2. Add Component > ServerSceneHandler
3. Add Component > NetworkObject (FishNet)
4. Configure NetworkObject:
   - Is Global: ✅ CHECKED
   - Default Despawn Type: Destroy
5. Drag to: Assets/_Project/5_Content/Prefabs/World/ServerSceneHandler.prefab
6. Delete from Hierarchy
```

**Resultado:**
```
Assets/_Project/5_Content/Prefabs/World/
├── ChunkLoaderManager.prefab
└── ServerSceneHandler.prefab
```

---

## 🌍 PASO 4: Crear Escenas de Chunks

### 4.1 Crear Primera Escena de Chunk

```
1. File > New Scene (Basic)
2. Save as: Assets/_Project/5_Content/Scenes/Chunks/Chunk_0_0.unity
```

### 4.2 Agregar Geometría (Ejemplo básico)

```
3. GameObject > 3D Object > Plane
4. Transform:
   - Position: (128, 0, 128)
   - Scale: (25.6, 1, 25.6)  ← 256x256m
   - Rotation: (0, 0, 0)
5. Material: Assign any material (grass, dirt, etc.)
```

**IMPORTANTE:** Cada chunk debe tener su geometría centrada en su área de mundo:
- **Chunk_0_0:** Geometría en (0-256, 0-256) → Centro en (128, 0, 128)
- **Chunk_0_1:** Geometría en (0-256, 256-512) → Centro en (128, 0, 384)
- **Chunk_1_0:** Geometría en (256-512, 0-256) → Centro en (384, 0, 128)

### 4.3 Agregar Safe Zone (Opcional)

Si quieres una zona segura en este chunk:

```
6. GameObject > Create Empty > Name: "SafeZone_TownCenter"
7. Add Component > Box Collider
   - Is Trigger: ✅ CHECKED
   - Size: (50, 20, 50)  ← Ajustar según necesidad
8. Inspector > Layer: SafeZone (9)
9. Add Component > ZoneTrigger
   - Zone Type: SafeZone
   - Zone Name: "Town Center"
10. Position: Centro de la zona que quieres proteger
```

### 4.4 Guardar Escena

```
11. File > Save (Ctrl+S)
12. Verify saved in: 5_Content/Scenes/Chunks/Chunk_0_0.unity
```

### 4.5 Repetir para Más Chunks

Crea al menos **3 chunks** para testing:
- **Chunk_0_0** - Centro (con safe zone para spawn)
- **Chunk_0_1** - Norte
- **Chunk_1_0** - Este

**Nombres IMPORTANTES:** Deben seguir exactamente el formato `Chunk_X_Y.unity`

---

## 📦 PASO 5: Agregar Escenas a Build Settings

### Opción Automática (Recomendado)

```
Menu: Tools > World Streaming > Add Chunk Scenes to Build Settings
```

Este comando:
- ✅ Busca todas las escenas en `5_Content/Scenes/Chunks/`
- ✅ Las agrega a Build Settings automáticamente
- ✅ Ignora duplicados

### Opción Manual

```
1. File > Build Settings
2. Click "Add Open Scenes" con cada chunk abierto
3. O arrastra las escenas desde Project a la lista
```

### Verificar

```
Menu: Tools > World Streaming > List All Chunk Scenes in Build Settings

Deberías ver en Console:
✅ Chunk_0_0
✅ Chunk_0_1
✅ Chunk_1_0
```

---

## 📝 PASO 6: Crear ChunkData ScriptableObjects

### 6.1 Crear ChunkData para Chunk_0_0

```
1. Right-click en Project > Create > Genesis > World > Chunk Data
2. Name: "Chunk_0_0_Data"
3. Configure en Inspector:

   [Identity]
   - Coordinate: X=0, Y=0
   - ChunkName: "Plains Center"

   [Scene Reference]
   - SceneName: "Chunk_0_0"  ← DEBE coincidir con el archivo .unity

   [Spawn Points]
   - IsStartingChunk: ✅ CHECKED (este es un spawn inicial)
   - SpawnPositions: Size = 3
     - Element 0: (100, 0, 100)
     - Element 1: (128, 0, 128)  ← Centro del chunk
     - Element 2: (150, 0, 150)

   [Metadata]
   - BiomeType: "Plains"

4. Save to: Assets/_Project/1_Data/ScriptableObjects/World/ChunkData/
```

### 6.2 Crear ChunkData para Chunk_0_1

```
1. Right-click > Create > Genesis > World > Chunk Data
2. Name: "Chunk_0_1_Data"
3. Configure:
   - Coordinate: X=0, Y=1
   - ChunkName: "Plains North"
   - SceneName: "Chunk_0_1"
   - IsStartingChunk: ❌ (no es spawn inicial)
   - SpawnPositions: Size = 0 (no spawns aquí)
   - BiomeType: "Plains"
4. Save
```

### 6.3 Crear ChunkData para Chunk_1_0

```
1. Right-click > Create > Genesis > World > Chunk Data
2. Name: "Chunk_1_0_Data"
3. Configure:
   - Coordinate: X=1, Y=0
   - ChunkName: "Plains East"
   - SceneName: "Chunk_1_0"
   - IsStartingChunk: ❌
   - SpawnPositions: Size = 0
   - BiomeType: "Plains"
4. Save
```

**CRÍTICO:** El campo `SceneName` debe coincidir EXACTAMENTE con el nombre del archivo .unity (sin extensión).

**Resultado:**
```
Assets/_Project/1_Data/ScriptableObjects/World/ChunkData/
├── Chunk_0_0_Data.asset
├── Chunk_0_1_Data.asset
└── Chunk_1_0_Data.asset
```

---

## 🗄️ PASO 7: Crear WorldDatabase

### 7.1 Crear Asset

```
1. Navigate to: Assets/_Project/1_Data/Resources/Databases/
2. Right-click > Create > Genesis > World > World Database
3. Name: "WorldDatabase"
```

**IMPORTANTE:** Debe estar en la carpeta `Resources/Databases/` para que se cargue en runtime.

### 7.2 Configurar WorldDatabase

```
4. Select WorldDatabase.asset
5. Inspector > Chunks: Size = 3
   - Element 0: Drag Chunk_0_0_Data
   - Element 1: Drag Chunk_0_1_Data
   - Element 2: Drag Chunk_1_0_Data
6. Save (Ctrl+S)
```

### 7.3 Verificar

```
WorldDatabase.asset:
✅ Chunks (3)
   ✅ Chunk_0_0_Data (IsStartingChunk = true)
   ✅ Chunk_0_1_Data
   ✅ Chunk_1_0_Data
```

**CRÍTICO:** Al menos 1 chunk debe tener `IsStartingChunk = true` o los players no podrán spawnear.

---

## ⚙️ PASO 8: Configurar Bootstrap Scene

### 8.1 Abrir Bootstrap Scene

```
Open: Assets/_Project/4_Bootstrap/Bootstrap.unity
```

### 8.2 Crear GameObject WorldStreamingBootstrap

```
1. Hierarchy > Create Empty > Name: "WorldStreamingBootstrap"
2. Add Component > WorldStreamingBootstrap
```

### 8.3 Asignar Referencias

```
3. Select WorldStreamingBootstrap en Hierarchy
4. Inspector > WorldStreamingBootstrap:

   [References]
   - World Database: Drag WorldDatabase.asset desde Resources/Databases/
   - Chunk Loader Prefab: Drag ChunkLoaderManager.prefab
   - Server Scene Handler Prefab: Drag ServerSceneHandler.prefab

5. Save Scene (Ctrl+S)
```

**Resultado esperado:**
```
Bootstrap.unity:
├── NetworkManager (ya existe)
├── PlayerSpawnManager (ya existe)
└── WorldStreamingBootstrap (NUEVO)
    ✅ World Database assigned
    ✅ Chunk Loader Prefab assigned
    ✅ Server Scene Handler Prefab assigned
```

---

## 🎨 PASO 9: Configurar Safe Zone UI (Opcional)

Si quieres un indicador visual cuando el player entra a safe zone:

### 9.1 Agregar UI Icon

```
1. Open: Tu HUD Canvas (ej: Assets/_Project/3_Presentation/UI/HUD.prefab)
2. Hierarchy > Right-click HUD > UI > Image
3. Name: "SafeZoneIcon"
4. Configure:
   - Anchor: Top-Right
   - Position: (-50, -50, 0)
   - Size: (32, 32)
   - Color: Green
   - Source Image: Shield icon (si tienes)
```

### 9.2 Agregar Text (Opcional)

```
5. Right-click HUD > UI > Text - TextMeshPro
6. Name: "SafeZoneText"
7. Configure:
   - Position: Below SafeZoneIcon
   - Text: "SAFE ZONE"
   - Font Size: 14
   - Color: Green
   - Alignment: Center
```

### 9.3 Agregar Script

```
8. Select HUD root GameObject
9. Add Component > SafeZoneIndicatorUI
10. Assign:
    - SafeZoneIcon: Drag UI Image
    - SafeZoneText: Drag TextMeshPro
11. Save Prefab
```

**Comportamiento:**
- Icon/Text **ocultos** por default
- Se **muestran** al entrar a safe zone
- Se **ocultan** al salir de safe zone

---

## ✅ PASO 10: Verificación Final

### 10.1 Checklist de Archivos

```
Assets/_Project/

0_Core/
├── World/
│   ├── ChunkCoordinate.cs ✅
│   └── WorldStreamingEvents.cs ✅
└── Networking/
    ├── ServerSceneHandler.cs ✅
    ├── ISpawnPositionProvider.cs ✅
    └── PlayerSpawnManager.cs ✅ (modificado)

1_Data/
├── ScriptableObjects/World/
│   ├── ChunkData.cs ✅
│   ├── WorldDatabase.cs ✅
│   └── ChunkData/
│       ├── Chunk_0_0_Data.asset ✅
│       ├── Chunk_0_1_Data.asset ✅
│       └── Chunk_1_0_Data.asset ✅
└── Resources/Databases/
    └── WorldDatabase.asset ✅

2_Simulation/
├── World/
│   ├── Tracking/
│   │   └── PlayerChunkTracker.cs ✅
│   ├── Loading/
│   │   └── ChunkLoaderManager.cs ✅
│   ├── Zones/
│   │   ├── ZoneTrigger.cs ✅
│   │   └── PlayerState.cs ✅
│   ├── Validation/
│   │   └── CombatValidator.cs ✅
│   ├── WorldSpawnProvider.cs ✅
│   └── PlayerSpawnHandler.cs ✅
└── Entities/Player/
    └── PlayerStats.cs ✅ (modificado)

3_Presentation/UI/SafeZone/
└── SafeZoneIndicatorUI.cs ✅

4_Bootstrap/Bootstrap/
└── WorldStreamingBootstrap.cs ✅

5_Content/
├── Scenes/Chunks/
│   ├── Chunk_0_0.unity ✅
│   ├── Chunk_0_1.unity ✅
│   └── Chunk_1_0.unity ✅
└── Prefabs/
    ├── World/
    │   ├── ChunkLoaderManager.prefab ✅
    │   └── ServerSceneHandler.prefab ✅
    └── Player.prefab ✅ (con 3 componentes nuevos)

Editor/
└── ChunkSceneBuilder.cs ✅
```

### 10.2 Checklist de Configuración

```
✅ Layer 9 "SafeZone" creado
✅ Layer 9 sin colisiones en Physics Matrix
✅ Player Prefab tiene PlayerChunkTracker
✅ Player Prefab tiene PlayerState
✅ Player Prefab tiene PlayerSpawnHandler
✅ ChunkLoaderManager.prefab creado
✅ ServerSceneHandler.prefab creado (con NetworkObject)
✅ 3 escenas de chunks creadas
✅ Escenas agregadas a Build Settings
✅ 3 ChunkData assets creados
✅ WorldDatabase creado en Resources/Databases/
✅ WorldDatabase tiene 3 chunks
✅ Al menos 1 chunk con IsStartingChunk = true
✅ Bootstrap scene tiene WorldStreamingBootstrap
✅ WorldStreamingBootstrap tiene referencias asignadas
```

---

## 🧪 PASO 11: Testing

### 11.1 Test Básico - Compilación

```
1. Verify no errors in Console
2. File > Build Settings > Player Settings
3. Check "Development Build"
4. Close Build Settings (no build yet)
```

### 11.2 Test de Spawn

```
1. Open Bootstrap.unity
2. Play Mode (Server + Client en Editor)
3. Expected Console logs:

[WorldStreamingBootstrap] WorldDatabase initialized
[WorldStreamingBootstrap] WorldSpawnProvider registered
[WorldStreamingBootstrap] ServerSceneHandler spawned
[WorldStreamingBootstrap] ChunkLoaderManager spawned
[SpawnManager] 🟢 Spawned player 0 in chunk Chunk(0, 0) at (128, 0, 128)
[PlayerSpawnHandler] Player spawned in chunk Chunk(0, 0)
[ServerSceneHandler] Moved Player(Clone) to scene Chunk_0_0
[ChunkLoader] Loaded chunk Chunk(0, 0) - Scene: Chunk_0_0 (Client)
[ChunkLoader] Loaded chunk Chunk(0, 1) - Scene: Chunk_0_1 (Client)
[ChunkLoader] Loaded chunk Chunk(1, 0) - Scene: Chunk_1_0 (Client)
```

### 11.3 Test de Chunk Loading

```
4. In Game:
   - Player spawns en Chunk_0_0
   - Camina hacia X=256 (boundary con Chunk_1_0)
   - Verifica en Hierarchy:
     ✅ Chunk_1_0 scene loaded
     ✅ Chunk_1_1 scene loaded (vecino)
     ❌ Chunk_-1_-1 unloaded (fuera de 9-slice)

5. Expected Console:
[PlayerChunkTracker] Chunk changed: Chunk(0, 0) -> Chunk(1, 0)
[ChunkLoader] Loaded chunk Chunk(1, 1)
[ChunkLoader] Unloaded chunk Chunk(-1, 0)
[ServerSceneHandler] Moved Player(Clone) to scene Chunk_1_0
```

### 11.4 Test de Safe Zone

```
6. Camina hacia safe zone trigger (si creaste uno)
7. Expected Console (Server):
[ZoneTrigger] Player 1 entered Town Center

8. Expected UI (Client):
✅ Shield icon appears
✅ "SAFE ZONE" text appears (green)

9. Camina fuera del trigger
10. Expected Console (Server):
[ZoneTrigger] Player 1 exited Town Center

11. Expected UI (Client):
❌ Shield icon disappears
❌ Text disappears
```

### 11.5 Test de Combat Blocking

```
12. Tener 2 players conectados
13. Player A entra a safe zone
14. Player B dispara a Player A
15. Expected Console:
[ProjectileController] Projectile hit blocked: Target is in a safe zone

16. Player A intenta disparar desde safe zone
17. Expected Console:
[ProjectileController] Projectile hit blocked: Cannot attack from safe zone
```

---

## 🐛 Troubleshooting

### Error: "WorldDatabase not found in ServiceLocator"

**Causa:** WorldDatabase no está en `Resources/Databases/`

**Fix:**
```
1. Verify path: Assets/_Project/1_Data/Resources/Databases/WorldDatabase.asset
2. Folder MUST be named exactly "Resources"
3. Re-assign in WorldStreamingBootstrap if needed
```

### Error: "Scene 'Chunk_0_0' couldn't be loaded"

**Causa:** Escena no está en Build Settings

**Fix:**
```
Menu: Tools > World Streaming > Add Chunk Scenes to Build Settings
Menu: Tools > World Streaming > List All Chunk Scenes
```

### Error: "No valid starting chunk with spawn positions"

**Causa:** Ningún ChunkData tiene `IsStartingChunk = true`

**Fix:**
```
1. Open Chunk_0_0_Data.asset
2. IsStartingChunk: ✅ CHECK
3. SpawnPositions: Size = 1 (minimum)
   - Element 0: (128, 0, 128)
4. Save
```

### Warning: "ServerSceneHandler not found"

**Causa:** ServerSceneHandler prefab no asignado o no spawneó

**Fix:**
```
1. Open Bootstrap.unity
2. Select WorldStreamingBootstrap
3. Verify Server Scene Handler Prefab is assigned
4. Play Mode > Check Console for "[WorldStreamingBootstrap] ServerSceneHandler spawned"
```

### Player spawns at (0, 0, 0)

**Causa:** WorldSpawnProvider no registrado o ChunkData sin spawn positions

**Fix:**
```
1. Check Console for "[WorldStreamingBootstrap] WorldSpawnProvider registered"
2. Verify Chunk_0_0_Data has SpawnPositions array with values
3. Verify WorldDatabase is assigned in Bootstrap
```

### Safe Zone trigger no funciona

**Causa:** Layer incorrecto o collision matrix mal configurada

**Fix:**
```
1. Select ZoneTrigger GameObject
2. Inspector > Layer: SafeZone (9)
3. Box Collider > Is Trigger: ✅ CHECKED
4. Edit > Project Settings > Physics
5. Layer 9 debe estar DESMARCADO para todas las capas
```

### Chunks no se cargan/descargan

**Causa:** PlayerChunkTracker no está en Player Prefab

**Fix:**
```
1. Open Player Prefab
2. Add Component > PlayerChunkTracker
3. Add Component > PlayerState
4. Add Component > PlayerSpawnHandler
5. Save Prefab
```

---

## 📚 Documentación Adicional

- **WORLD_STREAMING_NO_ADDRESSABLES.md** - Detalles técnicos del sistema sin Addressables
- **WORLD_STREAMING_ASSEMBLY_FIX.md** - Explicación de Dependency Inversion Pattern
- **PLAYER_PREFAB_SETUP.md** - Guía detallada de componentes del player
- **CHUNKS.md** - Referencia rápida del sistema de chunks

---

## 🎯 Resumen de Configuración Manual

**Player Prefab:**
1. Add 3 components (PlayerChunkTracker, PlayerState, PlayerSpawnHandler)

**Prefabs de Managers:**
2. Create ChunkLoaderManager.prefab
3. Create ServerSceneHandler.prefab (con NetworkObject)

**Escenas:**
4. Create 3+ chunk scenes (Chunk_0_0, Chunk_0_1, Chunk_1_0)
5. Add to Build Settings (Tools menu)

**Data:**
6. Create 3 ChunkData assets (matching scene names)
7. Create WorldDatabase in Resources/Databases/
8. Add ChunkData to WorldDatabase

**Bootstrap:**
9. Add WorldStreamingBootstrap to Bootstrap scene
10. Assign 3 references (WorldDatabase + 2 prefabs)

**Testing:**
11. Play Mode > Verify logs > Test chunk loading

---

**Total Tiempo Estimado:** 30-45 minutos para configuración inicial completa.

**¡Sistema listo para usar!** 🎉

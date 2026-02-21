# Persistencia de Datos con Nakama

## Arquitectura

```
┌─────────────┐     ┌──────────────────────────┐     ┌─────────────────────────────┐
│  Bootstrap   │────▶│   Core (Genesis.Core)    │◀────│   Simulation                │
│ EntryPoint   │     │ NakamaManager            │     │ CharacterPersistenceBridge   │
│ (wiring)     │     │ IPersistenceService       │     │ (implements IPersistenceBridge)│
│              │     │ IPersistenceBridge         │     │                             │
└─────────────┘     └──────────────────────────┘     └─────────────────────────────┘
```

**Restriccion de assemblies**: Core NO puede referenciar Simulation. Se usa el patron bridge/interface:
- `IPersistenceBridge` (interface en Core) define Extract/Hydrate
- `CharacterPersistenceBridge` (clase en Simulation) implementa la interface accediendo a componentes reales
- `EntryPoint.cs` (Bootstrap) registra el bridge en el ServiceLocator al arrancar

---

## Infraestructura (Docker)

**Archivo**: `docker-compose.yml` (raiz del proyecto)

```bash
docker compose up -d
```

- **CockroachDB** (:26257, :8080): BD interna de Nakama. No se interactua directamente.
- **Nakama** (:7350 API, :7349 gRPC, :7351 Consola Admin)
- Consola: `http://localhost:7351` (user: admin, pass: password)
- Server key: `defaultkey`

---

## Modelo de Datos

**Archivo**: `0_Core/Persistence/CharacterData.cs`

```csharp
CharacterData {
    // Identity
    string playerName, int classIndex

    // Level & XP
    int level, float currentXP, int unspentPoints, int gold

    // Base Attributes (solo puntos asignados, sin equipment)
    int strength, agility, intelligence, wisdom, constitution

    // Current Stats
    float currentHealth, currentMana

    // Position
    float posX, posY, posZ, rotY

    // Equipment (11 slots: Head, Shoulders, Chest, Pants, Feet, Hands, Belt, Weapon, OffHand, Ring1, Ring2)
    SerializedItemSlot[] equipment

    // Inventory (25 slots)
    SerializedItemSlot[] inventory

    // Quests
    SerializedQuestProgress[] activeQuests
    string[] completedQuests

    // Metadata
    long lastSaveTimestamp
}
```

Se guarda como JSON en Nakama Storage:
- **Collection**: `"characters"`
- **Key**: `"main"`
- **UserID**: Nakama user ID (1 por nombre de personaje, via device auth)
- **Permisos**: Owner Read (2), Owner Write (1)

---

## Archivos Creados

| Archivo | Assembly | Proposito |
|---------|----------|-----------|
| `docker-compose.yml` | — | Nakama + CockroachDB |
| `0_Core/Persistence/CharacterData.cs` | Genesis.Core | Modelo serializable plano (solo primitivos) |
| `0_Core/Persistence/IPersistenceService.cs` | Genesis.Core | Interface: `LoadAsync`, `SaveAsync`, `MarkDirty`, `RegisterPlayer`, `UnregisterPlayer` |
| `0_Core/Persistence/IPersistenceBridge.cs` | Genesis.Core | Interface: `ExtractPlayerData`, `HydratePlayer`, `CreateNewPlayerData` |
| `0_Core/Persistence/NakamaManager.cs` | Genesis.Core | MonoBehaviour que conecta a Nakama, autentica, lee/escribe Storage, auto-save, quit-save |
| `2_Simulation/Persistence/CharacterPersistenceBridge.cs` | Genesis.Simulation | Implementa IPersistenceBridge accediendo a componentes del player |

## Archivos Modificados

| Archivo | Cambios |
|---------|---------|
| `Packages/manifest.json` | Agregado `com.heroiclabs.nakama-unity` v3.13.0 via git |
| `0_Core/Genesis.Core.asmdef` | Agregada referencia al assembly `Nakama` |
| `PlayerAttributes.cs` | Agregado `HydrateFromSave(level, xp, unspentPoints, gold, str, agi, int, wis, con)` |
| `PlayerStats.cs` | Agregado `HydrateFromSave(health, mana)` |
| `EquipmentManager.cs` | Agregado `HydrateFromSave(SerializedItemSlot[])` + `SetSlotDirect()` privado |
| `PlayerInventory.cs` | Agregado `HydrateFromSave(SerializedItemSlot[])` |
| `PlayerQuestManager.cs` | Agregado `HydrateFromSave(SerializedQuestProgress[], string[])` |
| `PlayerClassManager.cs` | `LoadOrCreateCharacterAsync()` con auth por nombre, sesiones per-player. `OnStopServer`/`SaveBeforeDespawn` como safety-net para host. Cache de `_persistedClientId`/`_persistedUserId` |
| `PlayerSpawnManager.cs` | `SavePlayerOnDisconnect()` (save ANTES de cleanup), ground-snap al spawnear, CC disable/enable |
| `PlayerMotorMultiplayer.cs` | `SpawnGroundingRoutine()` (8s timeout, raycast 50m→100m), `RpcHydrateTeleport()` para sync de posicion al cliente |
| `CharacterPersistenceBridge.cs` | `HydratePlayer()` envia `RpcHydrateTeleport` al cliente para sync de posicion |
| `EntryPoint.cs` | Registra `IPersistenceBridge` (CharacterPersistenceBridge) en ServiceLocator |

---

## Flujo de Datos

### Login (carga)

```
1. Cliente se conecta → FishNet spawn player (con ground-snap server-side)
2. PlayerMotorMultiplayer.OnStartClient() → SpawnGroundingRoutine()
   (desactiva CC, espera terreno, raycast snap al suelo, reactiva CC)
3. PlayerClassManager.OnStartClient() → CmdSetLoginData(name, classIndex)
4. [ServerRpc] CmdSetLoginData:
   a. Setea nombre y clase normalmente (SetClass)
   b. Llama LoadOrCreateCharacterAsync() (async void, con try-catch global)
5. LoadOrCreateCharacterAsync:
   a. Obtiene IPersistenceService del ServiceLocator
   b. Autentica con Nakama: deviceId = "genesis_char_{playerName}" (per-character)
   c. Cada clientId obtiene su propia ISession (sesiones per-player)
   d. Registra mapping clientId → nakamaUserId
   e. LoadAsync(userId) → lee Nakama Storage (usando la sesion del player)
   f. Si data existe: bridge.HydratePlayer() (server teleport + RpcHydrateTeleport al cliente)
   g. Si data no existe: bridge.CreateNewPlayerData(pos actual) → SaveAsync()
   h. RegisterPlayer(clientId, playerObj) para auto-save y quit-save
   i. Cache _persistedClientId, _persistedUserId, _persistenceReady (para safety-net OnStopServer)
```

### Hidratacion (orden de componentes)

```
1. PlayerAttributes.HydrateFromSave() → level, XP, gold, base stats → RecalculateDerivedStats()
2. EquipmentManager.HydrateFromSave() → 11 equipment slots → RecalculateStats()
3. PlayerInventory.HydrateFromSave() → 25 inventory slots
4. PlayerStats.HydrateFromSave() → currentHealth, currentMana (despues de max values)
5. PlayerQuestManager.HydrateFromSave() → quest log + completed quests
6. Posicion (server + client):
   a. Server: CC disable → Raycast ground snap → teleport → CC enable
   b. Client: RpcHydrateTeleport (TargetRpc) → teleporta al owner → reinicia SpawnGroundingRoutine
   (necesario porque con movimiento client-authoritative, cambios de posicion en server NO se propagan al owner)
```

### Auto-Save (periodico)

```
NakamaManager.Update() cada 30s:
  → Itera _connectedPlayers (Dictionary<int, NetworkObject>)
  → Para cada player: bridge.ExtractPlayerData(playerObj) → SaveAsyncForClient(clientId, userId, data)
  → Cada save usa la ISession correcta del player (no una sesion compartida)
```

### Disconnect (save para clientes remotos)

**Descubrimiento critico**: FishNet dispara `OnRemoteConnectionState(Stopped)` ANTES de despawnear objetos.
En `ServerManager.Transport_OnRemoteConnectionState`: linea 640 dispara el evento, linea 643 despawnea.
Esto significa que `conn.FirstObject`, sesiones y componentes siguen validos durante el handler.

```
PlayerSpawnManager.OnRemoteConnectionState(Stopped) → SavePlayerOnDisconnect(conn):
  1. SAVE PRIMERO (sesiones y objetos aun validos):
     → conn.FirstObject (NetworkObject del player — aun NO despawneado)
     → nakama.GetUserId(clientId) → obtiene userId de la sesion activa
     → bridge.ExtractPlayerData(playerObj) → captura todo el estado
     → persistence.SaveAsync(userId, data) con ContinueWith para error handling
     (SaveAsync captura la referencia a ISession sincronicamente antes del primer await)
  2. CLEANUP DESPUES de que el save empezo:
     → nakama.UnregisterPlayer(clientId) — remueve de auto-save
     → nakama.UnregisterConnection(clientId) — limpia sesion
```

**Orden critico**: Save ANTES de cleanup. Si cleanup va primero, `SaveAsync` falla con "no session for client".

### Server Stop (safety-net para host)

```
PlayerClassManager.OnStopServer() → SaveBeforeDespawn():
  → Verifica _persistenceReady y _persistedUserId (cacheados en LoadOrCreateCharacterAsync)
  → Verifica si la sesion aun existe (nakama.GetUserId). Si no existe, skip (disconnect handler ya guardo)
  → Si la sesion existe → es el host player → extract + save
  → Esto cubre el caso de detener el servidor (editor Stop, cierre de app)
```

### Application Quit (save para TODOS, incluido host)

```
NakamaManager.OnApplicationQuit():
  → Itera _connectedPlayers
  → Lanza Task.WhenAll de saves en paralelo (cada uno con su sesion)
  → Cubre el caso de editor Stop y cierre de juego
```

---

## Persistencia por Nombre de Personaje

Cada personaje tiene su propia cuenta Nakama:
- Device ID: `"genesis_char_{playerName.ToLower().Trim()}"`
- Esto crea un usuario Nakama unico por nombre de personaje
- El mismo host puede loguearse con nombres distintos y cada uno tiene datos separados
- Nakama device ID minimo 10 bytes (el prefijo `genesis_char_` lo garantiza)

## Sesiones Per-Player

NakamaManager mantiene un `Dictionary<int, ISession>` (clientId → sesion):
- Cada player se autentica y obtiene su propia sesion
- Load/Save usan la sesion correcta del owner de los datos
- Evita el bug donde un player pisaba la sesion de otro

---

## Spawn y Ground Snap

Al spawnear un player se aplican dos mecanismos de ground-snap:

### Server-side (PlayerSpawnManager)
1. Raycast desde spawn position + 2m arriba hacia abajo
2. Desactiva CharacterController antes del FishNet Spawn
3. Setea posicion al hit point
4. Reactiva CharacterController

### Client-side (PlayerMotorMultiplayer.SpawnGroundingRoutine)
1. Al iniciar como Owner, activa `_spawnProtection` y desactiva CC
2. Cada frame hace raycast desde `transform.position` (NO cacheada) buscando terreno
   - Raycast desde +50m arriba, rango 100m hacia abajo
   - Timeout: 8 segundos maximo
   - Usa posicion actual cada frame para que teleports de hidratacion se reflejen automaticamente
3. Cuando encuentra suelo, teleporta al hit point + 0.1m
4. Reactiva CC y desactiva proteccion
5. Durante `_spawnProtection` el motor no aplica movimiento ni gravedad

Esto soluciona el problema de chunk streaming: el cliente puede no tener el terreno cargado al momento del spawn.

### HydratePlayer (CharacterPersistenceBridge) — Server + Client
1. **Server**: Desactiva CC → Raycast ground snap → teleport → Reactiva CC
2. **Client (RpcHydrateTeleport)**:
   - Cancela SpawnGroundingRoutine existente si esta corriendo
   - Desactiva CC → teleport a posicion guardada → setea rotacion
   - Reinicia SpawnGroundingRoutine en la nueva posicion (espera chunks/terreno)
   - Necesario porque el movimiento es client-authoritative: `transform.position` en server no se propaga al owner

---

## Issues Conocidos

### SceneId not found in SceneObjects (FishNet)
```
SceneId of XXXXXXXXX not found in SceneObjects
```
- **Causa**: El cliente no tiene cargada la escena/chunk que el servidor referencia
- **Impacto**: Objetos de escena networkeados no se sincronizan al cliente hasta que cargue el chunk
- **Severidad**: Warning — no bloquea el gameplay, el chunk streaming eventualmente carga la escena
- **Fix pendiente**: Agregar `SceneCondition` a los NetworkObjects de escena, o sincronizar chunk loading antes del spawn

## Issues Resueltos

### Persistencia de cliente remoto (RESUELTO)
- **Problema**: El host persistia correctamente pero el cliente remoto no guardaba datos al desconectarse.
- **Causa raiz**: `SavePlayerOnDisconnect` llamaba `UnregisterConnection` (limpiando la sesion) antes de que `SaveAsync` pudiera usar la sesion. Error: `"Cannot save - no session for client -1"`.
- **Fix**: Reordenar SavePlayerOnDisconnect: save PRIMERO (sesiones/objetos validos), cleanup DESPUES.

### Posicion no se restauraba en cliente remoto (RESUELTO)
- **Problema**: Items y stats se restauraban correctamente, pero la posicion no. El cliente aparecia en el spawn point en vez de en la posicion guardada.
- **Causa raiz**: `HydratePlayer` seteaba `transform.position` solo en el servidor. Con movimiento client-authoritative, eso no se propaga al owner.
- **Fix**: Agregado `RpcHydrateTeleport` (TargetRpc) que teleporta al cliente y reinicia `SpawnGroundingRoutine`.

### SpawnGroundingRoutine no detectaba teleport de hidratacion (RESUELTO)
- **Problema**: La rutina cacheaba la posicion inicial y hacia raycast siempre desde ahi, ignorando teleports posteriores.
- **Fix**: Usar `transform.position` cada frame (sin cachear). Aumentado timeout a 8s, raycast desde +50m con rango 100m.

---

## Setup

1. **Docker**: `docker compose up -d` y verificar `http://localhost:7351`
2. **NakamaManager GameObject**: Ya existe en la Bootstrap scene con el componente `NakamaManager` (host=127.0.0.1, port=7350, key=defaultkey)
3. **Testear**: Conectar jugador → equipar items → ganar XP/gold → desconectar → reconectar → verificar persistencia

---

## Notas Tecnicas

### FishNet Event Ordering (critico)
- `OnRemoteConnectionState(Stopped)` se dispara **ANTES** de despawnear objetos del cliente
- `conn.FirstObject`, sesiones Nakama y componentes del player siguen validos durante el handler
- `OnStopServer` (per-object) se dispara **DESPUES** del disconnect handler
- Esto determina que el save DEBE hacerse en `SavePlayerOnDisconnect`, no en `OnStopServer`
- `OnStopServer` en PlayerClassManager solo sirve como safety-net para el host player

### Async Error Handling
- `SaveAsync` retorna `Task` — nunca usar `_ = SaveAsync(...)` (fire-and-forget pierde excepciones)
- Usar `.ContinueWith(t => { if (t.IsFaulted) Debug.LogError(...); })` para capturar errores
- `SaveAsync` captura la referencia a `ISession` sincronicamente antes del primer `await`, por lo que cleanup posterior no la afecta

### General
- `NakamaManager` se auto-registra como `IPersistenceService` en su `Awake()` via ServiceLocator
- `NakamaManager.Start()` hace un test de conexion a Nakama al arrancar (log visible en consola)
- `CharacterPersistenceBridge` es registrado por `EntryPoint.Awake()` como `IPersistenceBridge`
- `AuthenticateDeviceAsync(clientId, deviceId)` crea sesion per-player (no comparte sesion)
- `CharacterData` usa solo primitivos y structs serializables — no tipos de Unity ni de Simulation
- `SerializedItemSlot` castea enums (ItemTier, ItemRarity) a int para evitar dependencias de assembly
- `SerializedQuestProgress` castea QuestState a int por la misma razon
- Todos los metodos `HydrateFromSave()` son `[Server]` — solo se ejecutan en el servidor
- `JsonUtility.ToJson/FromJson` se usa para serializar CharacterData (compatible con Nakama Storage API)
- Logging exhaustivo con prefijos `[NakamaManager]`, `[PlayerClassManager]`, `[PersistenceBridge]`, `[SpawnManager]`, `[Motor]`
- Buscar `═══ PERSISTENCE FLOW` en consola para ver el flujo completo de login
- Buscar `═══ HYDRATION` para ver la carga de datos
- Buscar `═══ APPLICATION QUIT` para ver el save on quit
- Buscar `═══ DISCONNECT SAVE` para ver el save al desconectar un cliente remoto

## Docker warning (Apple Silicon)

```
The requested image's platform (linux/amd64) does not match the detected host platform (linux/arm64/v8)
```
- Nakama no tiene imagen ARM nativa, corre via Rosetta 2
- Funciona correctamente pero puede ser mas lento
- No requiere accion

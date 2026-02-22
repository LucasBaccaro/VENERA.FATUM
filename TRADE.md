# Sistema de Trade Player-to-Player

## Resumen

Sistema de intercambio de items y gold entre jugadores, diseñado para la economía Full Loot del juego. Implementado con FishNet server-authoritative, protocolo anti-scam (Lock > Accept > Countdown 4s), swap atomico server-side, y selector de cantidad para items stackeables.

---

## Arquitectura

### Decisiones de Diseño

- **TargetRpc (NO SyncVars)**: Los datos de trade son transitorios y privados entre 2 jugadores. SyncVars broadcastean a todos los observers (innecesario + leak de info). TargetRpc a cada participante da control preciso.
- **Server Authority total**: Toda validacion de items, gold, distancia, y estado se hace en el servidor. El cliente solo envia comandos y recibe resultados.
- **EventBus para desacoplar**: La comunicacion entre TradeManager (Simulation) y TradeWindowController (Presentation) se hace via EventBus, respetando la separacion de assemblies.
- **Structs para EventBus**: El EventBus soporta maximo 5 parametros. Se crearon `TradeOfferSnapshot` y `TradeLockSnapshot` para empaquetar datos en un solo parametro.

### State Machine (Server-side)

```
None -> Requested -> Active -> [Lock Phase] -> Countdown -> Completed
  ^       |           |           |               |
  +-- Cancelled <-- Cancel <-- Cancel <------  Cancel
```

---

## Archivos

### Nuevos (8 archivos)

| Archivo | Capa | Descripcion |
|---------|------|-------------|
| `2_Simulation/Trading/TradeSession.cs` | Simulation | Modelo de datos: TradeState, TradeOffer, TradeSession, constantes |
| `2_Simulation/Trading/TradeManager.cs` | Simulation | NetworkBehaviour: 9 ServerRpcs + 8 TargetRpcs + swap atomico |
| `3_Presentation/UI/Controllers/TradeWindowController.cs` | Presentation | Controller: ventana de trade + popup request + quantity picker |
| `3_Presentation/UI/Controllers/PlayerContextMenuController.cs` | Presentation | Controller: menu contextual right-click sobre otro player |
| `3_Presentation/UI/Views/TradeWindowUI.uxml` | Presentation | Layout UXML: trade window + countdown + quantity picker popup |
| `3_Presentation/UI/Styles/TradeWindowStyle.uss` | Presentation | Estilos USS: tema medieval oscuro con acentos gold |
| `3_Presentation/UI/Views/PlayerContextMenu.uxml` | Presentation | Layout UXML: popup contextual compacto |
| `3_Presentation/UI/Styles/PlayerContextMenu.uss` | Presentation | Estilos USS: popup con borde gold |

### Modificados (4 archivos)

| Archivo | Cambio |
|---------|--------|
| `3_Presentation/UI/Controllers/PlayerUIConnector.cs` | Wiring de TradeManager + PlayerInventory a los controllers en OnStartClient() |
| `3_Presentation/UI/Controllers/InventoryController.cs` | Left-click en slot redirige a trade window cuando esta abierta |
| `5_Content/Prefabs/Player/Player.prefab` | Componente TradeManager agregado al root |
| `4_Bootstrap/Bootstrap.unity` | GameObjects TradeWindowUI y PlayerContextMenuUI bajo [UI ROOT] |

---

## Protocolo de Red

### Flujo completo

1. **Request**: A hace right-click en B > context menu > "Trade" > `CmdRequestTrade(targetObjectId)` > Server valida (vivos, sin combat, < 10m, no en trade) > `TargetTradeRequested` a B con nombre de A
2. **Accept/Decline**: B ve popup > acepta > `CmdRespondTrade(sessionId, true)` > Server crea sesion Active > `TargetTradeOpened` a ambos
3. **Offer Phase**: Cada jugador agrega items/gold > `CmdAddTradeItem(slot, qty)` / `CmdSetTradeGold(amount)` > Server valida (item existe, cantidad valida, gold suficiente) > copia datos a sesion > `TargetTradeOfferUpdated` al otro jugador. Cualquier cambio resetea ambos locks.
4. **Quantity Picker**: Si el item es stackeable (qty > 1), se muestra popup con botones Min/-/+/Max y campo de texto para elegir cantidad. Items no-stackeables se agregan directo.
5. **Lock**: Jugador presiona Lock > `CmdLockTrade` > Server marca locked > `TargetTradeLockChanged` a ambos
6. **Unlock**: Si alguien desbloquea > ambos locks y accepts se resetean > notificar a ambos
7. **Accept**: Solo disponible cuando ambos locked > `CmdAcceptTrade` > si ambos aceptaron > `TargetTradeCountdownStarted` (4s)
8. **Countdown**: Server cuenta 4s en Update. Display muestra 3→2→1→0. Cualquiera puede cancelar. Al completar > `ExecuteTrade()` > `TargetTradeCompleted` a ambos
9. **Cancel**: En cualquier momento > `CmdCancelTrade` > cleanup > `TargetTradeCancelled` a ambos

### RPCs

**ServerRpcs (Cliente > Servidor):**
- `CmdRequestTrade(int targetObjectId)`
- `CmdRespondTrade(uint sessionId, bool accepted)`
- `CmdAddTradeItem(int inventorySlotIndex, int quantity)`
- `CmdRemoveTradeItem(int tradeSlotIndex)`
- `CmdSetTradeGold(int amount)`
- `CmdLockTrade()`
- `CmdUnlockTrade()`
- `CmdAcceptTrade()`
- `CmdCancelTrade()`

**TargetRpcs (Servidor > Cliente especifico):**
- `TargetTradeRequested(conn, sessionId, requesterName)`
- `TargetTradeOpened(conn, sessionId, partnerName)`
- `TargetTradeOfferUpdated(conn, myItems..., partnerItems...)` — 14 params serializados
- `TargetTradeLockChanged(conn, myLocked, myAccepted, partnerLocked, partnerAccepted)`
- `TargetTradeCountdownStarted(conn, duration)`
- `TargetTradeCompleted(conn)`
- `TargetTradeCancelled(conn, reason)`
- `TargetTradeError(conn, message)`

### EventBus Events

| Evento | Parametros | Emisor |
|--------|-----------|--------|
| `OnTradeRequested` | `uint sessionId, string requesterName` | TargetTradeRequested |
| `OnTradeOpened` | `uint sessionId, string partnerName` | TargetTradeOpened |
| `OnTradeOfferUpdated` | `TradeOfferSnapshot` | TargetTradeOfferUpdated |
| `OnTradeLockChanged` | `TradeLockSnapshot` | TargetTradeLockChanged |
| `OnTradeCountdownStarted` | `float duration` | TargetTradeCountdownStarted |
| `OnTradeCompleted` | (ninguno) | TargetTradeCompleted |
| `OnTradeCancelled` | `string reason` | TargetTradeCancelled |

---

## Swap Atomico (ExecuteTrade)

```
PASO 1 - VALIDAR (antes de tocar nada):
  - Ambos players vivos y existen
  - Items aun existen en los slots originales del inventario
  - Gold suficiente en ambos
  - Espacio en inventario: calcular net slots needed (items recibidos - items dados)

PASO 2 - EJECUTAR (remove primero, add despues):
  - Remove items de inventario A (por slot index, en orden inverso)
  - Remove items de inventario B (por slot index, en orden inverso)
  - Add items de A a inventario de B
  - Add items de B a inventario de A
  - Transfer gold: SpendGold/GainGold en ambos

PASO 3 - COMPLETAR:
  - Estado -> Completed
  - TargetTradeCompleted a ambos
  - Cleanup session
```

---

## Constantes

```csharp
MAX_TRADE_SLOTS = 6      // Items por lado
TRADE_RANGE = 10f         // Metros maximos para iniciar trade
COMBAT_COOLDOWN = 5f      // Segundos fuera de combate requeridos
COUNTDOWN_DURATION = 4f   // Segundos de countdown (display: 3,2,1,0)
```

---

## Validaciones

### Gold (cliente + servidor)
- **Cliente**: `OnGoldInputChanged` clampa al tipear via `PlayerAttributes.Gold` (SyncVar)
- **Cliente**: `SubmitGold` clampa antes de enviar RPC
- **Servidor**: `CmdSetTradeGold` clampa a `_attributes.Gold`
- **Servidor**: `CmdSetTradeGold` hace early-return si el monto no cambio (evita resetear locks innecesariamente)

### Items con cantidad parcial
- `CmdAddTradeItem(slotIndex, quantity)` acepta cantidad parcial de stacks
- Servidor valida: `quantity > 0 && quantity <= slot.Quantity`
- El trade slot guarda solo la cantidad elegida, no el stack completo
- `ExecuteTrade` valida que la cantidad original aun exista al momento del swap

---

## Edge Cases Manejados

- **Disconnect**: `OnStopNetwork()` cancela trade, notifica al partner
- **Muerte**: Server check en Update durante trade activo
- **Item consumido/equipado durante trade**: Validacion al momento de ejecutar swap
- **Inventario lleno**: Validacion pre-ejecucion considerando net slots (dados vs recibidos)
- **Distancia**: Validada al iniciar request
- **Doble trade**: Rechazado si cualquier player ya tiene sesion activa
- **Self-trade**: Rechazado en CmdRequestTrade
- **Gold set sin cambio**: Early-return evita reset de locks (fix race condition SubmitGold + CmdLockTrade)
- **UI init timing**: Controllers reintentan InitializeUI() en Update() si rootVisualElement no estaba listo en Start()

---

## Vulnerabilidades Conocidas (TODO)

### CRITICAS

| # | Vulnerabilidad | Impacto | Fix propuesto |
|---|---------------|---------|---------------|
| 1 | **SpendGold return value ignorado** | Si SpendGold falla (gold insuficiente por race), gold no se descuenta pero trade continua | Verificar return de SpendGold; si falla, rollback |
| 2 | **AddItem return value ignorado** | Si AddItem falla (inventario lleno por race), items se pierden permanentemente | Verificar return de AddItem; si falla, rollback |
| 3 | **No hay save inmediato post-trade** | Si el server crashea dentro de los 30s de auto-save, el trade se revierte | Llamar SavePlayerNow() para ambos jugadores despues de ExecuteTrade |

### MEDIAS

| # | Vulnerabilidad | Impacto | Fix propuesto |
|---|---------------|---------|---------------|
| 4 | **Race condition inventario durante ejecucion** | Otro ServerRpc puede modificar inventario entre validacion y ejecucion (mismo frame) | Bloquear inventario durante ExecuteTrade (flag `_isTrading`) |
| 5 | **ActiveSessions no se limpia en server restart** | Diccionario estatico retiene referencias muertas despues de scene reload | Limpiar en OnServerStart o usar non-static |
| 6 | **Sin timeout de sesion** | Si ambos players disconnectan sin cancel, sesion queda en memoria | Timer de timeout (ej: 5 min inactivo → auto-cancel) |

---

## Setup en Scene (Bootstrap.unity)

Bajo `[UI ROOT]`:
- **TradeWindowUI**: Transform + UIDocument (TradeWindowUI.uxml) + TradeWindowController
- **PlayerContextMenuUI**: Transform + UIDocument (PlayerContextMenu.uxml) + PlayerContextMenuController (LayerMask = 8, Layer 3 "Player")

En **Player.prefab**:
- Componente `TradeManager` en el root

Wiring automatico en **PlayerUIConnector.OnStartClient()**:
```csharp
TradeManager tradeManager = GetComponent<TradeManager>();
if (tradeManager != null) {
    TradeWindowController tradeWindow = FindFirstObjectByType<TradeWindowController>();
    if (tradeWindow != null) {
        PlayerInventory inventory = GetComponent<PlayerInventory>();
        tradeWindow.SetReferences(tradeManager, inventory);
    }
    PlayerContextMenuController contextMenu = FindFirstObjectByType<PlayerContextMenuController>();
    if (contextMenu != null) {
        contextMenu.SetReferences(tradeManager, base.NetworkObject);
    }
}
```

---

## Bugs Corregidos

### Race Condition en Context Menu
**Problema**: Al clickear "Trade", el `Update()` detectaba el left-click ANTES que UI Toolkit procesara el evento del boton, llamando `Hide()` y seteando `_targetPlayer = null`.
**Fix**: `_showFrame` + bounds check para no ocultar si click esta dentro del popup.

### SubmitGold reseteaba locks
**Problema**: `OnLockClicked()` llamaba `SubmitGold()` antes de `CmdLockTrade()`. `CmdSetTradeGold(0)` reseteaba todos los locks aunque el gold no habia cambiado. Resultado: al lockear B, se deslockeaba A.
**Fix**: Early-return en `CmdSetTradeGold` si `offer.Gold == amount`.

### UI bloqueada en cliente
**Problema**: Si `rootVisualElement` era null en `Start()` (timing de init del UIDocument), `pickingMode = Ignore` nunca se seteaba, bloqueando clicks en paneles de abajo.
**Fix**: Flag `_initialized` + reintento en `Update()` hasta que init sea exitoso.

---

## Debug Logs

Todos los logs usan prefijo `[TradeManager]` / `[TradeWindow]` / `[ContextMenu]`. Cubren:
- Request enviado/recibido con ObjectIds
- Cada razon de rechazo (ya en trade, muerto, en combate, distancia, etc.)
- Sesion creada con ID
- Respond trade con sessionId y estado
- Trade opened, countdown started, completed, cancelled



  Las 3 CRITICAS que hay que fixear:                                                                                                                                                                                                                      
                                                                                                                                                                                                                                                            
  1. SpendGold() return value ignorado — En ExecuteTrade() linea 578: attrA.SpendGold(offerA.Gold); — si por race condition el gold bajó entre la validación y la ejecución, SpendGold retorna false pero el trade continúa. El partner recibe gold que     
  nunca se descontó.
  2. AddItem() return value ignorado — Si AddItem falla (inventario cambió entre validación y ejecución), los items ya fueron removidos del sender pero nunca llegan al receiver. Items perdidos permanentemente.
  3. No hay save inmediato post-trade — El auto-save de Nakama corre cada 30s. Si el server crashea en esa ventana, el trade se revierte para ambos jugadores pero los items/gold ya se movieron en memoria. Puede causar dupeo o pérdida según el timing.

  Las MEDIAS son: race condition en inventario durante ejecución (otro RPC modifica slots en el mismo frame), ActiveSessions estático que no se limpia en restart, y sesiones huérfanas sin timeout.

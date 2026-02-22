# Sistema de Trade Player-to-Player

## Resumen

Sistema de intercambio de items y gold entre jugadores, diseñado para la economía Full Loot del juego. Implementado con FishNet server-authoritative, protocolo anti-scam (Lock > Accept > Countdown 3s), y swap atomico server-side.

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
| `3_Presentation/UI/Controllers/TradeWindowController.cs` | Presentation | Controller: ventana de trade + popup de request |
| `3_Presentation/UI/Controllers/PlayerContextMenuController.cs` | Presentation | Controller: menu contextual right-click sobre otro player |
| `3_Presentation/UI/Views/TradeWindowUI.uxml` | Presentation | Layout UXML: ventana de trade con 2 paneles + countdown |
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
3. **Offer Phase**: Cada jugador agrega items/gold > `CmdAddTradeItem` / `CmdSetTradeGold` > Server valida (item existe, gold suficiente) > copia datos a sesion > `TargetTradeOfferUpdated` al otro jugador. Cualquier cambio resetea ambos locks.
4. **Lock**: Jugador presiona Lock > `CmdLockTrade` > Server marca locked > `TargetTradeLockChanged` a ambos
5. **Unlock**: Si alguien desbloquea > ambos locks y accepts se resetean > notificar a ambos
6. **Accept**: Solo disponible cuando ambos locked > `CmdAcceptTrade` > si ambos aceptaron > `TargetTradeCountdownStarted` (3s)
7. **Countdown**: Server cuenta 3s en Update. Cualquiera puede cancelar. Al completar > `ExecuteTrade()` > `TargetTradeCompleted` a ambos
8. **Cancel**: En cualquier momento > `CmdCancelTrade` > cleanup > `TargetTradeCancelled` a ambos

### RPCs

**ServerRpcs (Cliente > Servidor):**
- `CmdRequestTrade(int targetObjectId)`
- `CmdRespondTrade(uint sessionId, bool accepted)`
- `CmdAddTradeItem(int inventorySlotIndex)`
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
COUNTDOWN_DURATION = 3f   // Segundos de countdown final
```

---

## Edge Cases Manejados

- **Disconnect**: `OnStopNetwork()` cancela trade, notifica al partner
- **Muerte**: Server check en Update durante trade activo
- **Item consumido/equipado durante trade**: Validacion al momento de ejecutar swap
- **Inventario lleno**: Validacion pre-ejecucion considerando net slots (dados vs recibidos)
- **Distancia**: Validada al iniciar request
- **Doble trade**: Rechazado si cualquier player ya tiene sesion activa
- **Self-trade**: Rechazado en CmdRequestTrade

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

## Interaccion con Inventario

Cuando la ventana de trade esta abierta, left-click en un slot del inventario agrega el item al trade:

```csharp
// En InventoryController.OnSlotClicked:
if (evt.button == 0) {
    var tradeWindow = FindFirstObjectByType<TradeWindowController>();
    if (tradeWindow != null && tradeWindow.IsTradeOpen) {
        tradeWindow.OnInventorySlotClicked(index);
        evt.StopPropagation();
        return;
    }
}
```

Click en un slot del trade propio lo remueve (via `CmdRemoveTradeItem`).

---

## Bug Corregido: Race Condition en Context Menu

**Problema**: Al clickear el boton "Trade" en el context menu, el `Update()` detectaba el left-click ANTES que UI Toolkit procesara el evento del boton. Esto llamaba `Hide()` que seteaba `_targetPlayer = null`, y cuando `OnTradeClicked()` se ejecutaba despues, fallaba porque no habia target.

**Solucion**:
1. Se agrego `_showFrame` para ignorar clicks en el frame donde se mostro el popup
2. Se verifica que el click este FUERA de los bounds del popup antes de ocultar
3. El boton Trade ya no compite con la logica de hide

```csharp
// En PlayerContextMenuController.Update():
if (_isVisible && Time.frameCount > _showFrame + 1) {
    if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        float uiY = Screen.height - mousePos.y;
        var bounds = _popup.worldBound;
        if (!bounds.Contains(new Vector2(mousePos.x, uiY))) {
            Hide();
        }
    }
}
```

---

## Debug Logs

Todos los logs usan el prefijo `[TradeManager]` o `[TradeWindow]` o `[ContextMenu]` para filtrar facilmente en la consola de Unity. Los logs cubren:
- Request enviado/recibido con ObjectIds
- Cada razon de rechazo (ya en trade, muerto, en combate, distancia, etc.)
- Sesion creada con ID
- Respond trade con sessionId y estado
- Trade opened, countdown started, completed, cancelled

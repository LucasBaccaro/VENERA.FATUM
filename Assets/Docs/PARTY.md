# Sistema de Party

## Arquitectura

El sistema de party es **solo de sesión** (no persiste entre reconexiones). Máximo **4 jugadores** por grupo. Server-authoritative vía FishNet.

```
PartyManager (server data)
  ├── PartyGroup (data class: members, leader, LootMode)
  └── llama ApplyPartyState() en cada PartyMember

PartyMember (NetworkBehaviour en player root)
  ├── SyncVars: _partyId, _isLeader, _partySize
  ├── ServerRpcs: CmdInvitePlayer, CmdAcceptInvite, CmdLeaveParty...
  └── dispara EventBus "OnPartyStateChanged" / "OnPartyInviteReceived"

PartyController (UI)
  ├── Panel de party (top-left HUD): nombres + barras HP
  └── Popup de invitación: Aceptar / Rechazar
```

---

## Archivos del Sistema

| Script | Ruta | Descripción |
|--------|------|-------------|
| `PartyGroup.cs` | `2_Simulation/Party/` | Data class. Enum `LootMode {Free, MasterLoot}` |
| `PartyManager.cs` | `2_Simulation/Party/` | MonoBehaviour server-only. Toda la lógica de invites/kick/disband |
| `IPartyService.cs` | `0_Core/Architecture/` | Interface para que Core assembly llame `OnPlayerDisconnected` |
| `PartyMember.cs` | `2_Simulation/Entities/Player/Party/` | NetworkBehaviour en el player root |
| `PartyController.cs` | `3_Presentation/UI/Controllers/` | UIToolkit: panel + popup |
| `PartyUI.uxml` | `3_Presentation/UI/Views/` | Layout del panel y popup |

---

## Setup en el Editor (pasos obligatorios)

### 1. PartyManager — en el GO `[MANAGER]` del Bootstrap

```
GameObject: [MANAGER] (o el GO donde viven AudioManager, XPRewardSystem, etc.)
  └── Add Component → PartyManager
```

Se auto-registra en `ServiceLocator` como `PartyManager` e `IPartyService` en `Awake()`.
No requiere ninguna referencia serializada.

---

### 2. PartyMember — en el prefab del jugador (root)

```
Player Prefab Root (donde están PlayerStats, PlayerCombat, etc.)
  └── Add Component → PartyMember
```

No requiere referencias. Las SyncVars (`_partyId`, `_isLeader`, `_partySize`) se sincronizan automáticamente.
> **Nota:** Agregar a **ambos** prefabs de clase (Warrior, Mage) si hay varios.

---

### 3. PartyController + UIDocument — en el GO `[UI]` del Bootstrap

```
GameObject: [UI] (o un child, como hiciste con AudioSettingsController)
  └── Add Component → PartyController
  └── Add Component → UIDocument
        └── Source Asset → PartyUI.uxml   (Assets/_Project/3_Presentation/UI/Views/PartyUI.uxml)
        └── Sort Order → (mayor que HUD, menor que popups: ej. 2)
```

El `PartyController` detecta el `UIDocument` automáticamente en `Awake()`.

---

## Flujo de Juego

```
A hace right-click en B → "Invitar a Party"
  → PlayerContextMenuController detecta PartyMember.IsPartyFull → habilita/deshabilita botón
  → OnInviteClicked → localPm.CmdInvitePlayer(B.OwnerId)  [ServerRpc]
  → PartyManager.TryCreateInvite(A, B)
  → PartyMember(B).TargetReceiveInvite(conn, "NombreDeA")
  → EventBus "OnPartyInviteReceived" → PartyController muestra popup

B hace click en [Aceptar]
  → PartyMember.CmdAcceptInvite()  [ServerRpc]
  → PartyManager.TryAcceptInvite(B)
    → Crea PartyGroup con A (líder) y B
    → ApplyPartyState en A: partyId=A.clientId, isLeader=true,  size=2
    → ApplyPartyState en B: partyId=A.clientId, isLeader=false, size=2
    → RpcPartyStateChanged() en ambos
    → EventBus "OnPartyStateChanged" → PartyController reconstruye panel

Mob muere (A lo mató, B está a ≤30m)
  → XPRewardSystem: totalXP = base * 1.05 → cada uno recibe base * 1.05 / 2

A lanza proyectil que pasa por B
  → ProjectileController: IsInSameParty(A, B) == true → return (pass-through)

A desconecta (líder)
  → PlayerSpawnManager → IPartyService.OnPlayerDisconnected(A.clientId)
  → PartyManager.DisbandParty() → B recibe ApplyPartyState(-1, false, 0) → panel oculto
```

---

## SyncVars de PartyMember

| SyncVar | Tipo | Default | Significado |
|---------|------|---------|-------------|
| `_partyId` | `int` | `-1` | ID de la party (= ClientId del líder). -1 = sin party |
| `_isLeader` | `bool` | `false` | Si este jugador es el líder |
| `_partySize` | `int` | `0` | Cantidad de miembros (para UI) |

**Propiedades públicas:** `IsInParty`, `PartyId`, `IsLeader`, `PartySize`, `IsPartyFull`

---

## EventBus Events

| Evento | Firma | Quién lo dispara | Quién lo escucha |
|--------|-------|-----------------|------------------|
| `OnPartyStateChanged` | `Action` | `PartyMember.RpcPartyStateChanged` (solo owner) | `PartyController` |
| `OnPartyInviteReceived` | `Action<string>` (inviterName) | `PartyMember.TargetReceiveInvite` | `PartyController` |

---

## API del PartyManager

```csharp
// Consulta (server o client via ServiceLocator)
bool IsInSameParty(int clientIdA, int clientIdB)
PartyGroup GetParty(int clientId)

// Flujo de invites (llamado desde PartyMember ServerRpcs)
bool TryCreateInvite(int inviterId, int inviteeId)
bool TryAcceptInvite(int inviteeId)
void DeclineInvite(int inviteeId)

// Gestión de party
void LeaveParty(int clientId)
void KickMember(int leaderId, int targetId)
void DisbandParty(int partyId)

// Disconnect (llamado por PlayerSpawnManager via IPartyService)
void OnPlayerDisconnected(int clientId)
```

---

## XP Compartido

- **Rango:** ≤ 30m del punto de muerte del mob
- **Fórmula:** `totalXP = baseXP * (1 + 0.05 * (n - 1))`  donde n = miembros calificados
- **Distribución:** `totalXP / n` por miembro
- **Ejemplo (2 players):** base=50 → total=52.5 → cada uno recibe 26.25 XP
- **Ejemplo (4 players):** base=50 → total=57.5 → cada uno recibe 14.375 XP

---

## Friendly Fire & Projectiles

- **`CombatValidator.CanApplyDamage`:** Si attacker y victim comparten party → `return false` ("Cannot damage party member")
- **`ProjectileController.HandleImpact`:** Si el hit es un party member del owner → `return` (sin impact, sin damage, sin VFX)
- **Mobs → Players:** Los proyectiles de enemigos NO chequean party (solo owner-is-player hace el check)

---

## LootMode (Diferido)

`LootBag` tiene `SyncVar<int> _lootMode` y propiedad `CurrentLootMode` pero el comportamiento actual es siempre **Free Loot** (`CanLoot()` retorna `true`). La arquitectura está lista para implementar Master Loot en el futuro.

---

## Reglas de Negocio

- Máximo **4 miembros** por party (`IsFull` se chequea en `TryCreateInvite` y `TryAcceptInvite`)
- **Líder desconecta** → `DisbandParty` completo
- **No-líder desconecta** → `LeaveParty`, si queda 1 solo → `DisbandParty`
- **Invite pendiente** se cancela si el inviter desconecta
- **Solo de sesión:** no hay persistencia de Nakama para parties

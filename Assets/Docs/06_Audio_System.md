# Audio System - Coverage & Status

## Arquitectura

- **AudioManager** (Singleton): Pool de AudioSources, crossfade de musica, EventBus listeners.
- **SoundLibrary** (ScriptableObject): Mapea `SoundType` enum a clips con volumen/pitch.
- **UISoundPlayer** (Static helper): `PlayOpen()`, `PlayClose()`, `PlayClick()`, `PlayError()`, `PlaySuccess()`.
- **Sonidos per-entity**: Campos `AudioClip` en SOs (`AbilityData`, `EnemyData`, `StatusEffectData`), broadcast via `OnPlaySFX3D`.

## Archivos de Audio Disponibles

```
Assets/_Project/5_Content/Audio/
  Music/
    music_safe.mp3          -> Zone_SafeEnter (PlayMusic, vol 0.3)
    music_unsafe.mp3        -> Zone_UnsafeEnter (PlayMusic, vol 0.3)
  SFX/
    Combat/
      npc_killed.wav        -> EnemyData.DeathSound (4 enemies)
      npc_killed1.wav       -> EnemyData.DeathSound (3 enemies)
    Items/
      chest.mp3             -> Loot_ChestOpen
      coin.mp3              -> Loot_Gold, Loot_BagOpen
      item_equip.mp3        -> Equipment_Equip, Equipment_Unequip
      potion.mp3            -> Consumable_Potion
  Footsteps/
    steps.mp3               -> Footstep system
```

---

## Estado por Categoria

### CON SONIDO (clip asignado y funcionando)

| SoundType / Campo | Clip | Trigger |
|---|---|---|
| Footsteps | steps.mp3 | PlayerMotorMultiplayer -> OnFootstep |
| Loot_Gold (11) | coin.mp3 | FloatingTextData "gold" |
| Loot_ChestOpen (19) | chest.mp3 | ChestController -> OnLootOpened |
| Loot_BagOpen (20) | coin.mp3 | LootBag -> OnLootOpened |
| Equipment_Equip (21) | item_equip.mp3 | EquipmentManager -> OnEquipmentSound |
| Equipment_Unequip (22) | item_equip.mp3 | EquipmentManager -> OnEquipmentSound |
| Consumable_Potion (23) | potion.mp3 | ConsumableHandler -> TargetRpc -> OnConsumableUsed |
| Zone_SafeEnter (25) | music_safe.mp3 | PlayerState -> OnPlayerZoneChanged (PlayMusic) |
| Zone_UnsafeEnter (26) | music_unsafe.mp3 | PlayerState -> OnPlayerZoneChanged (PlayMusic) |
| EnemyData.DeathSound | npc_killed/1.wav | EnemyMob.Die -> ObserversRpc -> OnPlaySFX3D |
| UI panels open/close | (via UISoundPlayer) | Inventory, CharacterPanel, VendorShop, DialoguePanel |

### WIRED PERO SIN CLIP (codigo listo, falta .mp3)

| SoundType / Campo | Trigger en AudioManager | MP3 necesario |
|---|---|---|
| UI_Open (1) | UISoundPlayer.PlayOpen() | `SFX/UI/ui_open.mp3` |
| UI_Close (2) | UISoundPlayer.PlayClose() | `SFX/UI/ui_close.mp3` |
| UI_Click (0) | **UIButtonSoundManager global** (todos los botones auto) + UISoundPlayer.PlayClick() | `SFX/UI/ui_click.mp3` |
| UI_Error (3) | OnCombatError, UISoundPlayer.PlayError() | `SFX/UI/ui_error.mp3` |
| UI_Success (4) | UISoundPlayer.PlaySuccess() | `SFX/UI/ui_success.mp3` |
| Combat_Hit (5) | OnFloatingText "damage" | `SFX/Combat/hit.mp3` |
| Combat_CriticalHit (6) | OnFloatingText "damage" (isCritical) | `SFX/Combat/crit_hit.mp3` |
| Combat_Miss (7) | OnFloatingText "evade" | `SFX/Combat/miss.mp3` |
| Combat_Death (8) | OnLocalPlayerDied | `SFX/Combat/player_death.mp3` |
| Combat_LevelUp (9) | OnLevelChanged | `SFX/Combat/level_up.mp3` |
| Loot_Pickup | LootBagController.TakeItem / TakeAll | `SFX/Items/loot_pickup.mp3` |
| Loot_ChestOpen_Epic | LootBagController.OnLootOpened (Epic+ en cofre) -> OnEpicChestItem | `SFX/Items/chest_epic.mp3` |
| Quest_Accept (15) | OnQuestAccepted | `SFX/UI/quest_accept.mp3` |
| Quest_Complete (16) | OnQuestCompleted (server event, host-only) | `SFX/UI/quest_complete.mp3` |
| Quest_ObjectiveComplete | OnQuestObjectiveProgress cuando current >= required | `SFX/UI/quest_objective.mp3` |
| Quest_TurnIn | OnQuestTurnedIn (TargetRpc al owner) | `SFX/UI/quest_turnin.mp3` |
| Trade_Incoming | OnTradeRequested | `SFX/UI/trade_incoming.mp3` |
| Vendor_Buy (13) | OnVendorBuyResult (success) | `SFX/Items/vendor_buy.mp3` |
| Player_Respawn (24) | OnLocalPlayerRespawned | `SFX/Combat/respawn.mp3` |
| EnemyData.HitSound | EnemyMob.TakeDamage -> ObserversRpc | Per-enemy clip en cada SO |
| EnemyData.AggroSounds[] | EnemyMob.ScanForTargets -> RpcPlayAggroSound (random) | Varios clips por SO (varios para variedad) |
| AbilityData.CastSound | PlayerCombat.RpcCastSuccess -> OnPlaySFX3D | Per-ability clip en cada SO |
| AbilityData.ImpactSound | ProjectileController.RpcPlayImpactSound -> OnPlaySFX3D | Per-ability clip en cada SO |
| StatusEffectData.ApplySound | StatusEffectSystem.ApplyEffect -> OnPlaySFX3D | Per-effect clip en cada SO |

### ENUM DEFINIDO PERO SIN TRIGGER EN CODIGO

| SoundType | Que falta | Recomendacion |
|---|---|---|
| Loot_Pickup (10) | No hay evento cuando se toma un item de loot bag/suelo | Agregar EventBus en LootBag.CmdTakeItem / CmdTakeAll |
| Loot_Drop (12) | No hay evento cuando se droppea un item | Agregar EventBus en drop-to-world (si existe) |
| Vendor_Sell (14) | No existe sistema de venta al vendor | Implementar cuando se agregue sell |
| Portal_Enter (17) | TeleportPortal no tiene integracion de audio | Agregar EventBus en TeleportPortal al entrar |
| Portal_Exit (18) | TeleportPortal no tiene integracion de audio | Agregar EventBus en TeleportPortal al salir |

---

## Sonidos Recomendados a Agregar (sugerencias)

### Prioridad Alta (impacto directo en gameplay feel)
- **hit.mp3** / **crit_hit.mp3**: Feedback de combate, es lo mas importante. Cada golpe deberia tener feedback auditivo.
- **player_death.mp3**: Sonido dramatico al morir el player.
- **level_up.mp3**: Fanfarria corta, muy satisfactoria.
- **quest_accept.mp3**: Sonido de pergamino/aceptacion.
- **quest_complete.mp3**: Fanfarria de logro.

### Prioridad Media (polish)
- **ui_open.mp3** / **ui_close.mp3**: Sonidos sutiles de panel, muy cortos.
- **ui_click.mp3**: Click sutil para botones.
- **ui_error.mp3**: Sonido de "no se puede" (buzz corto).
- **vendor_buy.mp3**: Sonido de monedas/transaccion.
- **respawn.mp3**: Sonido eterno/revival.
- **miss.mp3**: Whoosh de esquiva.

### Prioridad Baja (futuro)
- **loot_pickup.mp3**: Sonido de tomar item (requiere wiring).
- **portal_enter.mp3** / **portal_exit.mp3**: Whoosh magico (requiere wiring).
- Per-ability CastSound/ImpactSound en cada AbilityData SO.
- Per-status-effect ApplySound en cada StatusEffectData SO.

---

## Networking de Audio

| Sonido | Audiencia | Mecanismo |
|---|---|---|
| Enemy hit/death | Todos los observers | `[ObserversRpc]` en EnemyMob -> OnPlaySFX3D |
| Ability cast/impact | Todos los observers | `[ObserversRpc]` en PlayerCombat/ProjectileController -> OnPlaySFX3D |
| Status effect apply | Todos los observers | `[ObserversRpc]` en StatusEffectSystem -> OnPlaySFX3D |
| Equip/Unequip | Solo owner | SyncVar callback con IsOwner check |
| Consumable | Solo owner | `[TargetRpc]` al owner |
| Respawn | Solo owner | Evento local `OnLocalPlayerRespawned` |
| Chest/Loot open | Solo owner | `[TargetRpc]` -> EventBus local |
| UI panels | Solo local | UISoundPlayer (client-side) |
| Zone music | Solo owner | SyncVar callback con IsOwner check -> PlayMusic |
| Footsteps | Solo owner | Evento local `OnFootstep` |
| Combat text sounds | Solo owner | FloatingTextData via `[TargetRpc]` |

---

## Archivos Clave

| Archivo | Rol |
|---|---|
| `1_Data/ScriptableObjects/Core/SoundLibrary.cs` | Enum SoundType + SoundEntry mapping |
| `5_Content/Audio/SoundLibrary.asset` | Asset con clips asignados por Type |
| `3_Presentation/Audio/AudioManager.cs` | Singleton, pool, EventBus listeners |
| `3_Presentation/Audio/UISoundPlayer.cs` | Static helper para UI sounds |
| `1_Data/ScriptableObjects/Core/AbilityData.cs` | CastSound, ImpactSound per ability |
| `1_Data/ScriptableObjects/Enemies/EnemyData.cs` | HitSound, DeathSound per enemy |

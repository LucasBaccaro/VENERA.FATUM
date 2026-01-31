# Setup: Character Panel & Equipment UI

## Cambios Realizados

### 1. **Bug Fix: Closure en InventoryDebugController**
- ✅ Corregido bug que causaba "slot 25" al usar consumibles
- ✅ Agregado botón "Equipar" para items de tipo Equipment
- **Problema:** El lambda `() => UseItem(i)` capturaba el valor final del loop (25)
- **Solución:** Capturar índice en variable local: `int slotIndex = i;`

### 2. **Nuevo: CharacterPanelDebugController.cs**
- ✅ UI para ver equipamiento (tecla C)
- ✅ Muestra stats (Max HP, Max Mana, Spell Power)
- ✅ Muestra 6 slots de equipamiento (Head, Chest, Legs, Feet, Hands, Belt)
- ✅ Botón "Unequip" en cada slot equipado
- ✅ Auto-refresh cuando cambia el equipamiento

### 3. **Nuevo: CharacterPanelDebugUI.uxml**
- ✅ Layout UXML para el Character Panel

---

## Setup Manual en Unity

### Paso 1: Crear GameObject para Character Panel UI

```
1. Hierarchy → Right-click → UI Toolkit → UI Document
   Nombre: CharacterPanelUI

2. Inspector → UI Document component:
   - Source Asset: CharacterPanelDebugUI.uxml (drag desde Views folder)
   - Panel Settings: (usar el mismo que InventoryDebugUI)

3. Agregar componente CharacterPanelDebugController:
   - Add Component → Scripts → Genesis.Presentation → CharacterPanelDebugController
   - UI Document: (auto-asignado si está en el mismo GameObject)
```

### Paso 2: Verificar que InventoryDebugUI esté actualizado

El archivo `InventoryDebugController.cs` fue actualizado con:
- Bug fix del closure
- Botón "Equipar" para items de equipamiento

**NO necesitas hacer cambios manuales**, solo verifica que Unity recompile correctamente.

---

## Cómo Usar

### Inventario (Tecla I)
1. Presiona **I** para abrir inventario
2. **Consumibles** (pociones):
   - Botón **"Usar"** → Restaura HP/Mana
3. **Equipment**:
   - Botón **"Equipar"** → Equipa el item y lo mueve al Character Panel
   - Si ya hay un item en ese slot, se intercambian (swap)

### Character Panel (Tecla C)
1. Presiona **C** para abrir panel de personaje
2. Muestra:
   - **Stats totales**: Max Health, Max Mana, Spell Power
   - **6 slots de equipamiento**: Head, Chest, Legs, Feet, Hands, Belt
   - **Stats de cada item equipado**
3. **Botón "Unequip"**:
   - Desequipa el item
   - Lo devuelve al inventario
   - Stats se recalculan automáticamente

---

## Flow de Equipamiento

### Equipar desde Inventario → Character Panel
```
1. Inventario tiene: [Casco T0 Common]
2. Click "Equipar" en inventario
3. Server ejecuta: EquipmentManager.CmdEquipFromInventory(slotIndex)
4. Server:
   - Si hay item equipado en ese slot → lo devuelve al inventario
   - Equipa el nuevo item
   - Remueve del inventario
   - Recalcula stats
5. Character Panel se actualiza automáticamente (EventBus "OnEquipmentChanged")
6. Stats nuevos:
   - MaxHP: 800 → 850 (+50 del casco)
   - SpellPower: 0% → 5%
```

### Desequipar desde Character Panel → Inventario
```
1. Character Panel tiene: [Casco T0 Common equipado]
2. Click "Unequip" en Character Panel
3. Server ejecuta: EquipmentManager.CmdUnequipToInventory(EquipmentSlot.Head)
4. Server:
   - Verifica que inventario tenga espacio
   - Desequipa el item
   - Lo agrega al inventario
   - Recalcula stats
5. Inventario se actualiza automáticamente (EventBus "OnInventoryChanged")
6. Character Panel se actualiza automáticamente (EventBus "OnEquipmentChanged")
7. Stats nuevos:
   - MaxHP: 850 → 800 (-50)
   - SpellPower: 5% → 0%
```

---

## Testing Checklist

### Test 1: Consumibles (Bug Fix)
```
[ ] Abrir inventario (I)
[ ] Click en botón "Usar" de poción roja
[ ] Console muestra: "Usado consumible del slot X" (donde X es 0-24, NO 25)
[ ] HP aumenta correctamente
[ ] Quantity de poción disminuye (5 → 4)
```

### Test 2: Equipar Items
```
[ ] Abrir inventario (I)
[ ] Verificar que hay 6 items de equipment en inventario (ya equipados por StarterItemGranter)
[ ] Estos items NO deberían mostrarse en el inventario porque ya están equipados
```

**NOTA:** Si ves equipment en el inventario, significa que StarterItemGranter los agregó al inventario en lugar de equiparlos. Esto es incorrecto.

### Test 3: Character Panel
```
[ ] Presionar C → Abre Character Panel
[ ] Muestra stats:
    - Max Health: ~850 (base 800 + equipment)
    - Max Mana: ~850 (base 800 + equipment)
    - Spell Power: +33%
[ ] Muestra 6 slots equipados:
    - Head: Casco Básico (Common) - +50 HP, +5% SP
    - Chest: Pechera Básica (Common) - +100 HP, +10% SP
    - Legs: Pantalones Básicos (Common) - +75 HP, +7% SP
    - Feet: Botas Básicas (Common) - +25 HP, +3% SP
    - Hands: Guantes Básicos (Common) - +25 HP, +3% SP
    - Belt: Cinturón Básico (Common) - +50 Mana, +5% SP
```

### Test 4: Unequip
```
[ ] En Character Panel, click "Unequip" en Head slot
[ ] Item desaparece del Character Panel
[ ] Abrir inventario (I)
[ ] "Casco Básico" ahora está en el inventario
[ ] Stats actualizados:
    - Max Health: 850 → 800 (-50)
    - Spell Power: 33% → 28% (-5%)
```

### Test 5: Re-equip
```
[ ] En inventario, click "Equipar" en "Casco Básico"
[ ] Item desaparece del inventario
[ ] Abrir Character Panel (C)
[ ] "Casco Básico" ahora está en Head slot
[ ] Stats restaurados:
    - Max Health: 800 → 850 (+50)
    - Spell Power: 28% → 33% (+5%)
```

### Test 6: Swap Equipment
```
[ ] En Character Panel, desequipar Casco T0 Common
[ ] (Simular que looteaste un Casco T0 Uncommon)
[ ] En inventario, equipar Casco T0 Uncommon
[ ] Character Panel muestra:
    - Head: Casco Básico (Uncommon) - +75 HP, +10% SP
[ ] Stats mejoran:
    - Max Health: +25 HP adicional
    - Spell Power: +5% adicional
```

### Test 7: Inventario Lleno
```
[ ] Llenar inventario con items (25 slots)
[ ] En Character Panel, intentar "Unequip" un item
[ ] Console muestra: "Inventory is full!"
[ ] Item NO se desequipa
```

---

## Debugging

### Si los consumibles no funcionan:
```
Console → Buscar error:
"[PlayerInventory] Slot X is empty" → Índice incorrecto, verificar closure fix
"[ConsumableHandler] ..." → Ver logs específicos del handler
```

### Si el botón "Equipar" no aparece:
```
Verificar:
- ItemDatabase tiene los items
- ItemType == Equipment (no Consumable)
- InventoryDebugController recompilado correctamente
```

### Si Character Panel no se abre:
```
Verificar:
- CharacterPanelUI GameObject existe en Hierarchy
- UIDocument tiene CharacterPanelDebugUI.uxml asignado
- CharacterPanelDebugController está agregado al GameObject
```

### Si stats no se actualizan:
```
Console → Buscar:
"[EquipmentManager] Stats recalculated: ..." → Debería aparecer al equipar/desequipar
"[CharacterPanelDebugController] OnEquipmentChanged event received" → EventBus funcionando
```

---

## Arquitectura

### Network Flow (Server Authority)
```
Client                          Server                         All Clients
------                          ------                         -----------
Click "Equipar"
  → CmdEquipFromInventory() → [Server validates]
                                → Removes from inventory
                                → Adds to equipment slot
                                → Recalculates stats
                                → SyncVar/SyncList updates → UI refreshes
                                                               (via EventBus)
```

### EventBus Events
- **"OnInventoryChanged"**: Inventario modificado (triggered by SyncList callback)
- **"OnEquipmentChanged"**: Equipamiento modificado (triggered by SyncVar callback)

### Server Authority Checks
- ✅ Todas las operaciones son ServerRpc o [Server] methods
- ✅ Cliente NUNCA modifica datos directamente
- ✅ Servidor valida todo (espacio en inventario, item válido, etc.)

---

## Próximos Pasos

Una vez que verifiques que todo funciona:

1. ✅ **Integrar SpellPower en habilidades** (GUIA_SISTEMA_ITEMS.md - Paso 5)
2. ✅ **Testear combate con bonuses de equipamiento**
3. ✅ **Testear sistema de loot completo** (muerte → lootbag → lootear → equipar)

---

**¡Sistema de inventario y equipamiento completo!** 🎉

Equipar/desequipar items, ver stats en tiempo real, y todo sincronizado en multiplayer.

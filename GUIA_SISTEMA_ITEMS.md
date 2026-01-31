# Guía Completa: Sistema de Items y Loot (Fase 9)

## 📋 Resumen de lo Implementado

He implementado el **sistema completo de inventario, equipamiento y loot** según el plan de la Fase 9. Todo el código está funcionando y compilando correctamente.

### ✅ Sistemas Completados

#### 1. **Capa de Datos (ScriptableObjects)**

**Ubicación:** `/Assets/_Project/1_Data/ScriptableObjects/Items/`

- **ItemEnums.cs**: Define todos los enums y structs
  - `ItemType` (Consumable, Equipment, Material, Quest)
  - `EquipmentSlot` (Head, Chest, Legs, Feet, Hands, Belt)
  - `ItemTier` (T0, T1, T2)
  - `ItemRarity` (Common, Uncommon, Rare, Epic)
  - `ConsumableType` (HealthPotion, ManaPotion)
  - `StatType` (MaxHealth, MaxMana, SpellPower)
  - `ItemSlot` struct (network-friendly)
  - `StatModifier` struct

- **BaseItemData.cs**: Clase base abstracta para todos los items
  - ItemID, ItemName, Description, Icon
  - Stacking properties

- **EquipmentItemData.cs**: Items de equipamiento
  - Stats organizados por rareza (Common/Uncommon/Rare/Epic)
  - Método `GetStatsForRarity()`
  - Tooltips con información de stats

- **ConsumableItemData.cs**: Consumibles (pociones)
  - Tipo de consumible (HP/Mana)
  - Cantidad a restaurar
  - Cooldown

- **ItemDatabase.cs**: Registro central de items
  - Auto-find de items en el proyecto
  - Validación de IDs únicos
  - Getters type-safe

#### 2. **Capa de Simulación (Lógica del Juego)**

**Ubicación:** `/Assets/_Project/2_Simulation/Items/`

- **PlayerInventory.cs**: Sistema de inventario (25 slots)
  - SyncList para sincronización de red
  - Stacking automático (consumibles hasta 99)
  - Add/Remove items server-authoritative
  - EventBus "OnInventoryChanged"

- **EquipmentManager.cs**: Gestión de equipamiento (6 slots)
  - 6 SyncVars individuales para cada slot
  - Recálculo automático de stats
  - SpellPower bonus acumulativo
  - Integración con PlayerStats
  - EventBus "OnEquipmentChanged"

- **ConsumableHandler.cs**: Uso de pociones
  - Cooldown global de 3 segundos
  - Validaciones (no muerto, no HP/Mana full)
  - Integración con PlayerStats.Heal() y RestoreMana()

- **LootBag.cs**: Bolsa de loot (NetworkObject)
  - IInteractable implementation
  - Auto-despawn a los 5 minutos
  - Loot público (cualquiera puede saquear)
  - ServerRpc para Take Item / Take All

- **LootDropper.cs**: Sistema de full loot
  - Subscribe a EventBus "OnPlayerDied"
  - Recolecta inventory + equipment
  - Spawna LootBag en posición de muerte
  - Limpia inventario del jugador muerto

- **StarterItemGranter.cs**: Items iniciales
  - 5x Poción Roja, 5x Poción Azul
  - 6 piezas de equipment T0 Common
  - Delay de 0.5s para asegurar inicialización

#### 3. **Integraciones con Sistemas Existentes**

- **PlayerStats.cs**: Agregados métodos
  - `SetMaxHealth(float value)` - Actualiza max HP desde equipment
  - `SetMaxMana(float value)` - Actualiza max Mana desde equipment
  - `Heal()` ahora acepta parámetro opcional NetworkObject healer

- **PlayerClassManager.cs**:
  - Notifica a EquipmentManager cuando cambia la clase
  - Llama a `UpdateBaseStats()` para recalcular con nueva clase

- **PlayerCombat.cs**:
  - Método `CalculateFinalDamage(float baseDamage)`
  - Fórmula: `Damage = BaseDamage * (1 + SpellPowerBonus)`
  - Listo para integración en AbilityLogic

---

## 🔧 PASOS MANUALES REQUERIDOS

### Paso 1: Crear Items en Unity (ScriptableObjects)

#### A. Crear Consumibles

**Ubicación:** `/Assets/_Project/1_Data/ScriptableObjects/Items/Consumables/`

**1. Poción de Vida (Roja)**
```
Right-click → Create → VENERA.FATUM → Items → Consumable
Nombre: Potion_Health_Red

Inspector:
- Item ID: 1001
- Item Name: "Poción de Vida"
- Description: "Restaura 100 HP"
- Icon: (asignar sprite rojo cuando tengas)
- Is Stackable: ✓ true
- Max Stack Size: 99
- Consumable Type: HealthPotion
- Restore Amount: 100
- Cooldown: 3
- Usable In Combat: ✓ true
- Usable While Moving: ✓ true
```

**2. Poción de Mana (Azul)**
```
Nombre: Potion_Mana_Blue

Inspector:
- Item ID: 1002
- Item Name: "Poción de Mana"
- Description: "Restaura 100 Mana"
- Icon: (asignar sprite azul cuando tengas)
- Is Stackable: ✓ true
- Max Stack Size: 99
- Consumable Type: ManaPotion
- Restore Amount: 100
- Cooldown: 3
```

#### B. Crear Equipment T0

**Ubicación:** `/Assets/_Project/1_Data/ScriptableObjects/Items/Equipment/`

**IMPORTANTE:** SpellPower se ingresa como **decimal** (0.05 = 5%, 0.10 = 10%, etc.)

**1. Casco (Head)**
```
Right-click → Create → VENERA.FATUM → Items → Equipment
Nombre: Equipment_Head_T0

Inspector:
- Item ID: 2001
- Item Name: "Casco Básico"
- Description: "Armadura de cabeza de nivel inicial"
- Icon: (asignar cuando tengas)
- Slot: Head

Common Stats (Size: 2):
  [0] Type: MaxHealth, Value: 50
  [1] Type: SpellPower, Value: 0.05

Uncommon Stats (Size: 2):
  [0] Type: MaxHealth, Value: 75
  [1] Type: SpellPower, Value: 0.10

Rare Stats (Size: 2):
  [0] Type: MaxHealth, Value: 100
  [1] Type: SpellPower, Value: 0.15

Epic Stats (Size: 2):
  [0] Type: MaxHealth, Value: 150
  [1] Type: SpellPower, Value: 0.25
```

**2. Pechera (Chest)**
```
Nombre: Equipment_Chest_T0

- Item ID: 2002
- Item Name: "Pechera Básica"
- Slot: Chest

Common Stats: MaxHealth 100, SpellPower 0.10
Uncommon Stats: MaxHealth 150, SpellPower 0.15
Rare Stats: MaxHealth 200, SpellPower 0.20
Epic Stats: MaxHealth 300, SpellPower 0.35
```

**3. Pantalones (Legs)**
```
Nombre: Equipment_Legs_T0

- Item ID: 2003
- Item Name: "Pantalones Básicos"
- Slot: Legs

Common Stats: MaxHealth 75, SpellPower 0.07
Uncommon Stats: MaxHealth 110, SpellPower 0.12
Rare Stats: MaxHealth 150, SpellPower 0.18
Epic Stats: MaxHealth 225, SpellPower 0.30
```

**4. Botas (Feet)**
```
Nombre: Equipment_Feet_T0

- Item ID: 2004
- Item Name: "Botas Básicas"
- Slot: Feet

Common Stats: MaxHealth 25, SpellPower 0.03
Uncommon Stats: MaxHealth 40, SpellPower 0.06
Rare Stats: MaxHealth 55, SpellPower 0.09
Epic Stats: MaxHealth 85, SpellPower 0.15
```

**5. Guantes (Hands)**
```
Nombre: Equipment_Hands_T0

- Item ID: 2005
- Item Name: "Guantes Básicos"
- Slot: Hands

Common Stats: MaxHealth 25, SpellPower 0.03
Uncommon Stats: MaxHealth 40, SpellPower 0.06
Rare Stats: MaxHealth 55, SpellPower 0.09
Epic Stats: MaxHealth 85, SpellPower 0.15
```

**6. Cinturón (Belt)**
```
Nombre: Equipment_Belt_T0

- Item ID: 2006
- Item Name: "Cinturón Básico"
- Slot: Belt

Common Stats (Size: 2):
  [0] Type: MaxMana, Value: 50
  [1] Type: SpellPower, Value: 0.05

Uncommon Stats: MaxMana 75, SpellPower 0.10
Rare Stats: MaxMana 100, SpellPower 0.15
Epic Stats: MaxMana 150, SpellPower 0.25
```

---

### Paso 2: Crear ItemDatabase

**Ubicación:** `/Assets/Resources/ItemDatabase.asset`

⚠️ **CRÍTICO**: Debe estar en la carpeta `Resources` con el nombre exacto `ItemDatabase`

```
1. Navegar a: /Assets/Resources/
2. Right-click → Create → VENERA.FATUM → Databases → Item Database
3. Nombre: ItemDatabase
4. En el Inspector:
   - Click en "Auto-Find All Items"
   - Verificar que encuentre 8 items (2 consumables + 6 equipment)
   - Click en "Validate Item IDs"
   - Verificar en Console que no hay duplicados
```

---

### Paso 3: Crear LootBag Prefab

**Ubicación:** `/Assets/_Project/5_Content/Prefabs/Items/LootBag.prefab`

```
1. Crear carpeta: /Assets/_Project/5_Content/Prefabs/Items/

2. Hierarchy → Right-click → Create Empty
   Nombre: LootBag

3. Agregar componentes:

   NetworkObject (FishNet):
   - Is Networked: ✓ true
   - Is Global: false
   - Default Despawn Type: Pool

   LootBag (script):
   - Despawn Time: 300
   - Interact Radius: 3

   Visual (MeshFilter + MeshRenderer):
   - Mesh: Cube (temporal)
   - Material: Color dorado/marrón
   - Scale: (0.5, 0.5, 0.5)

   SphereCollider:
   - Is Trigger: ✓ true
   - Radius: 3

4. Drag GameObject a carpeta Prefabs para crear prefab

5. IMPORTANTE: Agregar a FishNet spawnable prefabs
   - NetworkManager → Object Pool (o Prefabs list)
   - Agregar LootBag prefab a la lista
```

---

### Paso 4: Actualizar Player Prefab

**Ubicación:** Tu Player prefab existente

```
Agregar componentes (en orden):

1. PlayerInventory
   - Inventory Size: 25

2. EquipmentManager
   - Player Stats: (arrastrar componente PlayerStats del mismo GameObject)

3. ConsumableHandler
   - Player Stats: (arrastrar PlayerStats)
   - Player Inventory: (arrastrar PlayerInventory)
   - Global Potion Cooldown: 3

4. LootDropper
   - Loot Bag Prefab: (arrastrar LootBag prefab de Prefabs/Items/)
   - Spawn Offset: (0, 0.5, 0)

5. StarterItemGranter
   - Health Potion ID: 1001
   - Mana Potion ID: 1002
   - Potion Quantity: 5
   - Head Equipment ID: 2001
   - Chest Equipment ID: 2002
   - Legs Equipment ID: 2003
   - Feet Equipment ID: 2004
   - Hands Equipment ID: 2005
   - Belt Equipment ID: 2006
   - Starter Tier: T0
   - Starter Rarity: Common
   - Grant Delay: 0.5
```

---

### Paso 5: Integrar SpellPower en Habilidades (CÓDIGO)

Este paso requiere modificar los archivos de lógica de habilidades para que usen el bonus de SpellPower del equipamiento.

**Archivos a modificar:**
- TargetedLogic.cs
- AOELogic.cs
- SelfAOELogic.cs
- ConeLogic.cs
- ChannelLogic.cs
- SkillshotLogic.cs (si aplica daño)
- DashLogic.cs (si aplica daño)
- ProjectileController.cs
- TargetedProjectile.cs
- TrapController.cs (si aplica daño)

**Patrón a aplicar:**

Buscar todas las líneas que llaman a `TakeDamage` con `data.BaseDamage`:

```csharp
// ANTES (sin SpellPower)
damageable.TakeDamage(data.BaseDamage, caster);
```

Reemplazar con:

```csharp
// DESPUÉS (con SpellPower)
float baseDamage = data.BaseDamage;
float finalDamage = baseDamage;

if (caster.TryGetComponent(out PlayerCombat playerCombat)) {
    finalDamage = playerCombat.CalculateFinalDamage(baseDamage);
}

damageable.TakeDamage(finalDamage, caster);
```

**Ejemplo completo en TargetedLogic.cs:**

```csharp
// Buscar el método ApplyEffectsToTarget alrededor de la línea 94
public static void ApplyEffectsToTarget(NetworkObject caster, NetworkObject target, AbilityData data) {
    if (target == null) return;

    // DAMAGE
    if (data.BaseDamage > 0) {
        if (target.TryGetComponent(out IDamageable damageable)) {
            // ═══ PHASE 9: Apply SpellPower bonus ═══
            float baseDamage = data.BaseDamage;
            float finalDamage = baseDamage;

            if (caster.TryGetComponent(out PlayerCombat playerCombat)) {
                finalDamage = playerCombat.CalculateFinalDamage(baseDamage);
            }

            damageable.TakeDamage(finalDamage, caster);
            Debug.Log($"[TargetedLogic] {caster.name} dealt {finalDamage} damage (base: {baseDamage})");
        }
    }
    // ... resto del código
}
```

**Nota:** Para habilidades multi-target (AOE, Cone), aplicar el cálculo **por cada target** dentro del loop.

---

## 🧪 TESTING: Checklist de Pruebas

### Test 1: Spawn del Jugador
```
[ ] Player spawns con inventario vacío (25 slots)
[ ] Player tiene 5x Poción Roja en inventario
[ ] Player tiene 5x Poción Azul en inventario
[ ] Player tiene 6 piezas de equipment equipadas (Head, Chest, Legs, Feet, Hands, Belt)
[ ] Stats iniciales muestran bonuses (debería tener más HP/Mana que el base de la clase)
```

**Stats esperados con full Common T0 equip:**
- Base clase Mage: ~800 HP, ~800 Mana (verificar tu ClassData)
- Con equipment: +275 HP, +50 Mana, +33% SpellPower
- Total aproximado: 1075 HP, 850 Mana

### Test 2: Sistema de Inventario
```
[ ] Abrir Console y verificar logs de StarterItemGranter
[ ] Debería ver: "Added 5x ItemID 1001" (poción roja)
[ ] Debería ver: "Added 5x ItemID 1002" (poción azul)
[ ] Debería ver 6 mensajes de "Equipped ItemID..."
```

### Test 3: Sistema de Equipamiento
```
[ ] Stats recalculados correctamente al spawn
[ ] Console muestra: "Stats recalculated: MaxHP=..., MaxMana=..., SpellPower=0.33"
```

### Test 4: SpellPower en Combate (después de integrar en AbilityLogic)
```
[ ] Sin equipment: habilidad hace daño base (ej: 100)
[ ] Con equipment T0 Common (33% SpellPower): habilidad hace 133 de daño
[ ] Console muestra: "Damage calculated: 100 * (1 + 0.33) = 133"
```

### Test 5: Sistema de Loot (CRÍTICO)
```
1. Iniciar servidor + cliente
2. Matar al jugador con console command o recibiendo daño

Verificar:
[ ] LootBag spawns en la posición del jugador muerto
[ ] LootBag es visible para otros jugadores
[ ] Console muestra: "LootBag spawned with X items"
[ ] Inventario del jugador muerto está vacío
[ ] Equipment del jugador muerto está vacío
[ ] Stats del jugador muerto vuelven al base de la clase

3. Acercarse al LootBag (radio 3m)
[ ] Debería poder interactuar (Press E)
[ ] (UI no implementada aún, pero debería triggerear evento)

4. Esperar 5 minutos
[ ] LootBag despawnea automáticamente
[ ] Console muestra: "Despawning after 300s (timeout)"
```

### Test 6: Consumibles (Cuando implementes el Cmd)
```
[ ] Llamar PlayerInventory.CmdUseConsumable(0) desde console
[ ] HP aumenta
[ ] Quantity de la poción disminuye (5 → 4)
[ ] No se puede usar si HP ya está full
[ ] Cooldown de 3 segundos funciona
```

---

## 📊 Tabla de Referencia: Stats de Equipment T0

| Slot  | Common HP | Uncommon HP | Rare HP | Epic HP | Common SP | Uncommon SP | Rare SP | Epic SP |
|-------|-----------|-------------|---------|---------|-----------|-------------|---------|---------|
| Head  | +50       | +75         | +100    | +150    | +5%       | +10%        | +15%    | +25%    |
| Chest | +100      | +150        | +200    | +300    | +10%      | +15%        | +20%    | +35%    |
| Legs  | +75       | +110        | +150    | +225    | +7%       | +12%        | +18%    | +30%    |
| Feet  | +25       | +40         | +55     | +85     | +3%       | +6%         | +9%     | +15%    |
| Hands | +25       | +40         | +55     | +85     | +3%       | +6%         | +9%     | +15%    |
| Belt* | +50M      | +75M        | +100M   | +150M   | +5%       | +10%        | +15%    | +25%    |

*Belt usa MaxMana en lugar de MaxHealth

**Total Full Set:**
- Common: +275 HP, +50 Mana, +33% SpellPower
- Epic: +845 HP, +150 Mana, +145% SpellPower

---

## ⚠️ Errores Comunes y Soluciones

### Error: "ItemDatabase.Instance is NULL"
**Causa:** ItemDatabase no está en Resources o tiene nombre incorrecto
**Solución:** Verificar que `/Assets/Resources/ItemDatabase.asset` existe

### Error: "Item with ID X not found"
**Causa:** ItemDatabase no fue actualizado después de crear items
**Solución:** Abrir ItemDatabase → Click "Auto-Find All Items"

### Error: "Duplicate ItemID"
**Causa:** Dos items tienen el mismo ID
**Solución:** ItemDatabase → "Validate Item IDs" → Corregir duplicados

### Error: LootBag no spawns
**Causa:** Prefab no está en FishNet spawnable prefabs
**Solución:** NetworkManager → Prefabs → Agregar LootBag

### Error: "Cannot spawn networked object"
**Causa:** LootBag prefab no tiene NetworkObject component
**Solución:** Agregar NetworkObject al prefab

### Error: Equipment no aumenta stats
**Causa:** PlayerStats reference no está asignada en EquipmentManager
**Solución:** Inspector → EquipmentManager → Drag PlayerStats component

### SpellPower muestra porcentajes incorrectos
**Causa:** Ingresaste 5 en lugar de 0.05
**Solución:** SpellPower es decimal: 5% = 0.05, 10% = 0.10, etc.

---

## 🚀 Orden de Implementación Recomendado

1. ✅ **Crear todos los items** (Paso 1)
2. ✅ **Crear ItemDatabase** (Paso 2)
3. ✅ **Crear LootBag prefab** (Paso 3)
4. ✅ **Actualizar Player prefab** (Paso 4)
5. ✅ **Probar spawn inicial** (Test 1-3)
6. ⏭️ **Integrar SpellPower** (Paso 5)
7. ⏭️ **Probar combate con bonuses** (Test 4)
8. ⏭️ **Probar sistema de loot** (Test 5)

---

## 🎯 Próximos Pasos (No Implementados)

Estos sistemas NO están implementados aún y están planeados para fases futuras:

❌ **UI (Interfaz de Usuario)**
- InventoryWindow.uxml
- LootBagWindow.uxml
- Tooltips
- Drag & drop
- Hotkeys (I, Right-click, Double-click)

❌ **Assets Visuales**
- Iconos de items
- Modelos 3D de lootbag
- Meshes de equipment equipable

❌ **Features Adicionales**
- Tiers T1 y T2
- Stats adicionales (CritChance, CooldownReduction, etc.)
- Persistencia (guardar en Nakama)
- Vendors/Tiendas
- Crafting
- Set bonuses

---

## 📝 Notas Finales

### Arquitectura de Red (FishNet v4)
- **SyncList**: Para inventario (colección dinámica)
- **SyncVar**: Para equipment slots (valores individuales)
- **ServerRpc**: Cliente solicita acciones al servidor
- **ObserversRpc**: Servidor notifica a todos los clientes
- **TargetRpc**: Servidor notifica a un cliente específico

### Server Authority
TODO el sistema es server-authoritative:
- Cliente solicita usar poción → Servidor valida y ejecuta
- Cliente solicita equipar item → Servidor valida y ejecuta
- Loot bags se crean en servidor → Sincronizan a clientes

### EventBus Pattern
Sistema desacoplado:
- Simulation dispara eventos → UI escucha
- `EventBus.Trigger("OnInventoryChanged")` → InventoryUI se actualiza
- `EventBus.Trigger("OnPlayerDied")` → LootDropper reacciona

### Debugging
Todos los sistemas tienen logs extensivos:
- `[PlayerInventory] Added 5x Health Potion`
- `[EquipmentManager] Stats recalculated: MaxHP=1075`
- `[LootBag] Spawned with 11 items from 'Player1'`
- `[PlayerCombat] Damage calculated: 100 * 1.33 = 133`

Usar Console para verificar el flujo de cada sistema.

---

**¡Sistema completo y listo para probar!** 🎉

Sigue los pasos manuales en orden y verifica cada test antes de continuar al siguiente.

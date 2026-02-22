# Balance de Items - Sistema de Equipamiento

## 1. Resumen del Sistema

El equipamiento usa un sistema de **Tier + Rarity** para escalar stats:

- **Tier** (T0, T1, T2, T3): Determina el "power level" base del item. Cada tier tiene su propio `ItemGenerationConfig` con rangos de stats.
- **Rarity** (Common, Uncommon, Rare, Epic): Dentro de cada tier, la rareza escala los stats y la cantidad de sub-stats.

Cada item de equipo (`EquipmentItemData`) tiene 4 listas de stats, una por rareza: `CommonStats`, `UncommonStats`, `RareStats`, `EpicStats`.

---

## 2. RequiredLevel (Restriccion por Nivel)

Campo `RequiredLevel` en cada `EquipmentItemData`:
- **Rango:** 0 a 50
- **Default:** 0 = sin restriccion (cualquier nivel puede equiparlo)
- **Validacion:** Server-side en `EquipmentManager.EquipItem()`. Si el jugador no tiene nivel suficiente, recibe error "Requires level X" via EventBus.
- **Tooltip:** Muestra "Requires Level X" en amarillo cuando el valor es > 0.

### Configuracion recomendada por Tier

| Tier | RequiredLevel | Contexto |
|------|--------------|----------|
| T0   | 0            | Starter items, sin restriccion |
| T1   | 10           | Primer upgrade significativo |
| T2   | 25           | Mid-game |
| T3   | 40           | End-game |

> Para setear RequiredLevel: abrir el `.asset` del item en Inspector, ajustar el slider "Required Level".

---

## 3. Estructura de Stats por Rareza

### Common (Blanco)
- Solo **Armor** (sin atributos primarios, sin sub-stats)
- Armor range definido en `CommonArmorRange` del config

### Uncommon (Verde)
- **2 atributos primarios** (clase primary + secondary) + **Armor** + **1 sub-stat**

### Rare (Azul)
- **2 atributos primarios** + **Armor** + **2 sub-stats**

### Epic (Violeta)
- **2 atributos primarios** + **Armor** + **3 sub-stats**

> Items sin clase (Rings): No tienen atributos primarios, solo Armor + sub-stats.

---

## 4. Balance Actual - T0

Archivo: `Assets/_Project/1_Data/ScriptableObjects/Core/DefaultItemGenerationConfig.asset`

### 4.1 Class Mappings

| Clase | Primary Attr | Secondary Attr | Sub-stats excluidos |
|-------|-------------|----------------|---------------------|
| Warrior | Strength | Constitution | Perception |
| Mage | Intelligence | Wisdom | Block, Penetration |

### 4.2 Categorias de Slots

Los slots se agrupan en categorias que determinan los rangos de atributos:

| Categoria | Slots | Descripcion |
|-----------|-------|-------------|
| **Armor** | Head, Shoulders, Chest, Pants | Piezas principales, stats medios |
| **Complement** | Hands, Feet | Piezas secundarias, stats bajos |
| **Weapon** | Weapon, OffHand | Stats mas altos (pieza mas importante) |
| **Accessory** | Belt, Ring1, Ring2 | Belt usa rangos Armor; Rings no tienen primary attrs |

### 4.3 Rangos de Stats por Rareza (T0)

#### Uncommon (1 sub-stat)

| Campo | Min | Max | Descripcion |
|-------|-----|-----|-------------|
| ArmorPrimary | 2 | 3 | Primary attr para Head/Shoulders/Chest/Pants |
| ArmorSecondary | 1 | 2 | Secondary attr para Head/Shoulders/Chest/Pants |
| ComplementPrimary | 1 | 2 | Primary attr para Hands/Feet |
| ComplementSecondary | 1 | 1 | Secondary attr para Hands/Feet |
| WeaponPrimary | 4 | 5 | Primary attr para Weapon/OffHand |
| WeaponSecondary | 1 | 2 | Secondary attr para Weapon/OffHand |
| ArmorRange | 2 | 3 | Armor flat para todas las piezas |
| SubStatPercent | 0.01 | 0.02 | Rango para sub-stats porcentuales (1-2%) |
| SubStatFlat | 1 | 2 | Rango para sub-stats flat |

#### Rare (2 sub-stats)

| Campo | Min | Max |
|-------|-----|-----|
| ArmorPrimary | 4 | 6 |
| ArmorSecondary | 2 | 3 |
| ComplementPrimary | 3 | 4 |
| ComplementSecondary | 2 | 2 |
| WeaponPrimary | 6 | 8 |
| WeaponSecondary | 2 | 3 |
| ArmorRange | 3 | 5 |
| SubStatPercent | 0.02 | 0.04 |
| SubStatFlat | 2 | 4 |

#### Epic (3 sub-stats)

| Campo | Min | Max |
|-------|-----|-----|
| ArmorPrimary | 7 | 10 |
| ArmorSecondary | 4 | 6 |
| ComplementPrimary | 5 | 7 |
| ComplementSecondary | 3 | 5 |
| WeaponPrimary | 9 | 12 |
| WeaponSecondary | 4 | 6 |
| ArmorRange | 5 | 8 |
| SubStatPercent | 0.04 | 0.06 |
| SubStatFlat | 4 | 7 |

#### Common

| Campo | Min | Max |
|-------|-----|-----|
| CommonArmorRange | 1 | 3 |

### 4.4 Proyeccion Full Set Epic T0 (11 piezas)

- **Warrior:** ~65-75 STR, ~35-45 CON, ~45-60 Armor, 33 sub-stat rolls
- **Mage:** ~65-75 INT, ~35-45 WIS, ~40-55 Armor, 33 sub-stat rolls
- **Referencia:** A nivel 50 un jugador tiene ~250 puntos de atributos. El set Epic T0 aporta ~30% de eso.
- **Sub-stats Epic:** ~5% Haste/LifeSteal/Penetration por pieza. Full set = ~15-18% total distribuido.

---

## 5. Sub-Stats

### 5.1 Pools por tipo de slot

**Pool General** (armor, complement, belt):
Haste, LifeSteal, Penetration, Block, LootLuck, Lockpicking, Perception, MoveSpeed

**Pool Combat** (weapons, offhand):
Haste, LifeSteal, Penetration, Block

**Pool Ring** (ring1, ring2):
Haste, LifeSteal, Penetration, MagicResistance, LootLuck, Lockpicking, Perception
_(Sin Block ni MoveSpeed, incluye MagicResistance)_

### 5.2 Tipos de Sub-stat

| Sub-stat | Tipo | Formato Display | Notas |
|----------|------|----------------|-------|
| Haste | Porcentual | "+X% Haste" | Velocidad de ataque/cast |
| LifeSteal | Porcentual | "+X% Life Steal" | % de dano convertido en vida |
| Penetration | Porcentual | "+X% Penetration" | Ignora % de armor enemigo |
| LootLuck | Porcentual | "+X% Loot Luck" | Mejor loot (UI-only por ahora) |
| MoveSpeed | Porcentual | "+X% Move Speed" | Velocidad de movimiento |
| Block | Flat | "+X Block" | Reduccion flat de dano |
| Lockpicking | Flat | "+X Lockpicking" | UI-only por ahora |
| Perception | Flat | "+X Perception" | UI-only por ahora |
| MagicResistance | Flat | "+X Magic Resistance" | Solo en rings |

> Los valores porcentuales se redondean a 2 decimales (ej: 0.02, 0.04, nunca 0.01491).

---

## 6. Como Crear Items para un Nuevo Tier

### Paso 1: Crear el Config (o reutilizar el existente)

Opcion A - Un config por tier: `Assets > Create > Genesis > Core > Item Generation Config`
Opcion B - Usar el mismo `DefaultItemGenerationConfig` ajustando rangos antes de generar.

### Paso 2: Definir los rangos del nuevo Tier

Escalado sugerido para T1 (referencia, ajustar segun playtesting):

| Campo | T0 Uncommon | T1 Uncommon (sugerido) |
|-------|-------------|----------------------|
| ArmorPrimary | 2-3 | 4-6 |
| WeaponPrimary | 4-5 | 7-9 |
| ArmorRange | 2-3 | 4-6 |
| SubStatPercent | 0.01-0.02 | 0.02-0.03 |
| SubStatFlat | 1-2 | 2-4 |

> Regla general: cada tier sube ~50-70% sobre el anterior.

### Paso 3: Crear los ScriptableObjects

1. Duplicar items T0 existentes y renombrar (ej: `Equipment_Warrior_Chest_T1`)
2. Cambiar el campo `Tier` a T1 en el Inspector
3. Setear `RequiredLevel` al nivel deseado (ej: 10 para T1)

### Paso 4: Generar Stats

1. Abrir `Genesis > Items > Generate Stats`
2. Seleccionar el Config con los rangos del tier nuevo
3. Filtrar por clase si es necesario
4. Click **"Generate Stats & Log Summary"** para generar y ver resumen en consola
5. Revisar items en Inspector para ajustes manuales

### Paso 5: Verificar Balance

Checklist:
- [ ] Uncommon/Rare/Epic tienen Armor > 0
- [ ] Sub-stats porcentuales son valores limpios (0.02, 0.04, no 0.0149)
- [ ] Weapons tienen el primary stat mas alto
- [ ] Rings solo tienen Armor + sub-stats (sin primary attrs)
- [ ] RequiredLevel seteado correctamente
- [ ] Tooltip muestra "Requires Level X" en el juego

---

## 7. Archivos Clave

| Archivo | Que es |
|---------|--------|
| `1_Data/ScriptableObjects/Core/ItemGenerationConfig.cs` | Definicion del config (struct RarityTierConfig, FloatRange, etc.) |
| `1_Data/ScriptableObjects/Core/DefaultItemGenerationConfig.asset` | Asset con los valores de balance T0 |
| `1_Data/ScriptableObjects/Items/EquipmentItemData.cs` | ScriptableObject base de cada item (RequiredLevel, stats por rareza) |
| `1_Data/ScriptableObjects/Items/ItemEnums.cs` | Enums: StatType, ItemTier, ItemRarity, EquipmentSlot |
| `Editor/ItemStatGenerator.cs` | Editor tool para generar stats automaticamente |
| `2_Simulation/Items/EquipmentManager.cs` | Validacion server-side de RequiredLevel y clase |
| `3_Presentation/UI/Controllers/ItemTooltipController.cs` | Muestra RequiredLevel en tooltip |

---

## 8. Bugs Corregidos

### ArmorRange en 0 (pre-fix)
**Problema:** El campo `ArmorRange` no estaba serializado en el `.asset`, causando que TODOS los items Uncommon/Rare/Epic se generaran con `Armor: 0`.
**Fix:** Se agrego `ArmorRange` con valores apropiados a los 3 rarity tiers en `DefaultItemGenerationConfig.asset`.

### Sub-stats con decimales excesivos (pre-fix)
**Problema:** Sub-stats porcentuales generaban valores como `0.014910028`.
**Fix:** Se agrego redondeo `Mathf.Round(value * 100f) / 100f` en `ItemStatGenerator.AddRandomSubStats()`.

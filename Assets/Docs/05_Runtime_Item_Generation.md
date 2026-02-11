# Runtime Item Generation (Futuro)

## Estado Actual (MVP)

- Items con stats fijos por rareza definidos en SOs
- Generador de editor (`Genesis/Items/Generate Stats`) llena Uncommon/Rare/Epic usando rangos de `ItemGenerationConfig`
- `ItemSlot` = { ItemID, Quantity, Tier, Rarity }
- Todos los jugadores con el mismo ItemID+Rarity ven los mismos stats

## Objetivo

Permitir que items dropeados de mobs/cofres tengan stats rolleados al momento del drop, manteniendo compatibilidad con items de stats fijos (quest rewards, tienda, sets).

## Cambios Necesarios

### 1. Agregar flag al SO

```csharp
// EquipmentItemData.cs
[Header("Generation Mode")]
[Tooltip("Si true, usa stats fijos del SO. Si false, el servidor rollea stats al drop.")]
public bool UseFixedStats = true;
```

Items existentes quedan en `true` (sin cambio de comportamiento).

### 2. Extraer logica de generacion a runtime

Crear `Assets/_Project/2_Simulation/Items/ItemStatRoller.cs`:

```csharp
// Clase static, sin dependencia de UnityEditor
public static class ItemStatRoller {
    public static List<StatModifier> Roll(EquipmentItemData item, ItemRarity rarity, ItemGenerationConfig config) {
        // Misma logica que ItemStatGenerator pero ejecutable en servidor
        // Determinar categoria por slot
        // Obtener class mapping
        // Generar primary + secondary + N sub-stats segun rarity
        // Weapons: combat sub-stats only
    }
}
```

La logica es identica a `ItemStatGenerator.GenerateStatsForRarity()` - solo se mueve de namespace Editor a Simulation.

### 3. Ampliar ItemSlot

```csharp
public struct ItemSlot {
    public int ItemID;
    public int Quantity;
    public ItemTier Tier;
    public ItemRarity Rarity;
    public uint InstanceUID;  // 0 = usa stats fijos del SO

    public bool HasInstanceStats => InstanceUID != 0;
}
```

### 4. Storage server-side

Diccionario en el servidor que mapea UID a stats rolleados:

```csharp
public class ItemInstanceRegistry {
    private Dictionary<uint, List<StatModifier>> instances;

    public uint RegisterInstance(List<StatModifier> stats) { ... }
    public List<StatModifier> GetStats(uint uid) { ... }
}
```

Persistir en Nakama Storage como JSON:
```json
{
  "collection": "item_instances",
  "key": "12345",
  "value": {
    "itemId": 3002,
    "rarity": 2,
    "stats": [
      {"type": 2, "value": 5},
      {"type": 6, "value": 3},
      {"type": 7, "value": 0.03}
    ]
  }
}
```

### 5. Modificar GetStatsForRarity

```csharp
// EquipmentItemData.cs
public List<StatModifier> GetStatsForRarity(ItemRarity rarity, uint instanceUID = 0) {
    // Si tiene instance stats, buscar en registry
    if (instanceUID != 0) {
        var registry = ServiceLocator.Get<ItemInstanceRegistry>();
        var stats = registry.GetStats(instanceUID);
        if (stats != null) return stats;
    }
    // Fallback a stats fijos
    switch (rarity) { ... }
}
```

### 6. Flujo de drop en runtime

```
1. Mob muere → servidor consulta loot table
2. Loot table devuelve: ItemID + Rarity
3. Servidor carga el SO del item
4. Si item.UseFixedStats → crear ItemSlot normal (InstanceUID = 0)
5. Si !item.UseFixedStats →
   a. ItemStatRoller.Roll(item, rarity, config) → List<StatModifier>
   b. registry.RegisterInstance(stats) → uint uid
   c. Crear ItemSlot con InstanceUID = uid
   d. Persistir en Nakama
6. Enviar ItemSlot al cliente
```

## Archivos a Crear/Modificar

| Archivo | Cambio |
|---------|--------|
| `EquipmentItemData.cs` | Agregar `UseFixedStats`, modificar `GetStatsForRarity` |
| `ItemSlot` en `ItemEnums.cs` | Agregar `InstanceUID` |
| **Nuevo:** `ItemStatRoller.cs` | Logica de roll extraida del editor script |
| **Nuevo:** `ItemInstanceRegistry.cs` | Registry server-side + persistencia Nakama |
| `EquipmentManager.cs` | Pasar InstanceUID al consultar stats |
| `PlayerUIConnector.cs` / UI | Pasar InstanceUID para mostrar stats correctos |

## Estimacion

- Cambio contenido, no requiere reestructurar la arquitectura
- `ItemGenerationConfig` y sus rangos se reusan tal cual
- El generador de editor sigue funcionando para items de stats fijos

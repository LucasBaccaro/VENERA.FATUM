# WORKFLOW: SISTEMA DE HABILIDADES

## 🎯 FLUJO DE DATOS COMPLETO

### **Arquitectura del Sistema**

```
📄 Abilities.json (Mock/Backup)
    ↓
[AbilityImporter] (Editor Script)
    ↓
📦 AbilityData.asset (ScriptableObject) ← FUENTE DE VERDAD EN RUNTIME
    ↓
📦 AbilityLogic.asset (ScriptableObject) ← Lógica de ejecución
    ↓
📦 AbilityDatabase.asset (Registry) ← Lookup rápido por ID
    ↓
⚙️ PlayerCombat.cs (Runtime) ← Ejecuta habilidades
```

---

## 📂 ESTRUCTURA DE ARCHIVOS

### **1. JSON (Data Source)**
**Ubicación**: `Assets/_Project/1_Data/JSON/Abilities.json`

**Propósito**:
- Data mock/backup
- NO se usa en runtime
- Sirve como fuente para generar assets

**Ejemplo**:
```json
[
  {
    "ID": 1001,
    "Name": "Fireball",
    "ManaCost": 20,
    "Cooldown": 0.5,
    "LogicType": "Skillshot",
    "TargetingMode": "Ground",
    "Range": 25.0,
    "BaseDamage": 50,
    "ProjectileSpeed": 20.0
  }
]
```

---

### **2. AbilityData.asset (ScriptableObject)**
**Ubicación**: `Assets/_Project/1_Data/Abilities/Ability_*.asset`

**Propósito**:
- **FUENTE DE VERDAD** en runtime
- Contiene todos los datos de la habilidad
- Referencia a un AbilityLogic

**Generación**:
- ✅ Automática: Usando AbilityImporter desde JSON
- ✅ Manual: Create > Genesis > Combat > Ability

---

### **3. AbilityLogic.asset (ScriptableObject)**
**Ubicación**: `Assets/_Project/1_Data/Abilities/Logic/Logic_*.asset`

**Propósito**:
- Contiene la **lógica de ejecución** de la habilidad
- Implementa `ExecuteDirectional()` método

**Tipos disponibles**:
```
Logic_Targeted     → Habilidades tab-target (Golpe Rápido, Daga de Maná)
Logic_Skillshot    → Proyectiles direccionales (Bola de Fuego)
Logic_AOE          → AOE ground-targeted (Meteorito)
Logic_SelfAOE      → AOE centrado en caster (Torbellino)
Logic_Dash         → Movimiento/teleport (Carga, Desenganche)
Logic_Cone         → Área cónica (Multidisparo)
Logic_Trap         → Trampas persistentes (Trampa de Hielo)
```

**Generación**:
- ✅ Automática: AbilityImporter crea si no existe
- ✅ Manual: Create > Genesis > Combat > Logic > [Tipo]

---

### **4. AbilityDatabase.asset (Registry)**
**Ubicación**: `Assets/Resources/Databases/AbilityDatabase.asset`

**Propósito**:
- Registry central de todas las habilidades
- Lookup rápido por ID
- Singleton accesible desde código

**Actualización**:
- ✅ Automática: Después de importar desde JSON
- ✅ Manual: Context Menu > Auto-Find All Abilities

---

## 🔧 CÓMO USAR EL SISTEMA

### **OPCIÓN A: IMPORTAR DESDE JSON (Recomendado)** ⭐

#### **Paso 1: Edita el JSON**
Abre `Assets/_Project/1_Data/JSON/Abilities.json` y agrega/edita habilidades:

```json
[
  {
    "ID": 1001,
    "Name": "Fireball",
    "Description": "Lanza una bola de fuego.",
    "ManaCost": 20,
    "Cooldown": 0.5,
    "GCD": 1.2,
    "CastType": "Casting",
    "CastTime": 1.5,
    "CanMoveWhileCasting": false,
    "TargetingMode": "Ground",
    "Range": 25.0,
    "Radius": 0.5,
    "Category": "Magical",
    "BaseDamage": 50,
    "BaseHeal": 0,
    "ProjectileSpeed": 20.0,
    "LogicType": "Skillshot"  ← IMPORTANTE
  }
]
```

**Campos clave**:
- `LogicType`: Determina qué Logic asset usar
  - `"Targeted"` → Habilidades tab-target
  - `"Skillshot"` → Proyectiles direccionales
  - `"AoE"` → AOE circular
  - `"Dash"` → Movimiento
  - `"Cone"` → Área cónica
  - `"Trap"` → Trampa

- `TargetingMode`: Determina el targeting
  - `"Enemy"` → Requiere target enemigo
  - `"Ground"` → Click en el suelo
  - `"Self"` → Self-cast
  - `"Ally"` → Requiere aliado

#### **Paso 2: Importar en Unity**

1. En Unity, ve al menú: **Genesis > Data > Import Abilities from JSON**
2. El script automáticamente:
   - ✅ Lee el JSON
   - ✅ Crea/actualiza AbilityData assets
   - ✅ Crea Logic assets si no existen
   - ✅ Asigna IndicatorType correcto
   - ✅ Vincula Logic con AbilityData
   - ✅ Actualiza AbilityDatabase

3. Verifica en consola:
```
[AbilityImporter] Importación completa.
Abilities - Creados: 4, Actualizados: 0
Logic Assets Creados: 4
[AbilityDatabase] Encontradas 4 habilidades en el proyecto.
```

#### **Paso 3: Verificar Assets**

Revisa que se crearon correctamente:
- `Assets/_Project/1_Data/Abilities/Ability_Fireball.asset` ✅
- `Assets/_Project/1_Data/Abilities/Logic/Logic_Skillshot.asset` ✅
- AbilityDatabase tiene la referencia ✅

---

### **OPCIÓN B: CREAR MANUALMENTE (Avanzado)**

Si prefieres crear habilidades sin JSON:

#### **Paso 1: Crear Logic Asset**
1. Right Click en `Logic/` folder
2. Create > Genesis > Combat > Logic > Skillshot
3. Nombre: `Logic_Skillshot`

#### **Paso 2: Crear AbilityData Asset**
1. Right Click en `Abilities/` folder
2. Create > Genesis > Combat > Ability
3. Nombre: `Ability_Fireball`
4. Configurar en Inspector:
   - ID: 1001
   - Name: Fireball
   - Logic: Arrastra `Logic_Skillshot` ⭐
   - Indicator Type: Line ⭐
   - Range: 25
   - Damage: 50

#### **Paso 3: Actualizar Database**
1. Selecciona `AbilityDatabase.asset`
2. Context Menu (3 puntos) > Auto-Find All Abilities

---

## 🎮 ASIGNAR HABILIDADES AL PLAYER

### **En PlayerCombat**

1. Abre prefab: `Assets/_Project/5_Content/Prefabs/Player/Player.prefab`
2. Selecciona componente: `PlayerCombat`
3. Sección `Ability Slots`:
   - Size: 6
   - Element 0: Arrastra `Ability_Fireball`
   - Element 1: Arrastra `Ability_Heal`
   - Element 2: Arrastra `Ability_Slash`
   - etc.

**Mapping de teclas**:
- Slot 0 → Tecla "1"
- Slot 1 → Tecla "2"
- Slot 2 → Tecla "3"
- Slot 3 → Tecla "4"
- Slot 4 → Tecla "5"
- Slot 5 → Tecla "6"

---

## 🔄 WORKFLOW RECOMENDADO

### **Para Balanceo de Habilidades**

1. Edita valores en `Abilities.json`
2. Re-importa: `Genesis > Data > Import Abilities from JSON`
3. Test en Unity
4. Repite

**Ventaja**: Cambios rápidos sin tocar assets manualmente.

---

### **Para Nuevas Habilidades**

1. Agrega entrada en `Abilities.json`
2. Define `LogicType` apropiado
3. Importa desde JSON
4. Asigna prefabs específicos (ProjectilePrefab, VFX, etc) manualmente en el asset
5. Test

---

### **Para Debugging**

Si una habilidad no funciona:

1. **Verifica el AbilityData asset**:
   - ¿Tiene Logic asignado? ✅
   - ¿IndicatorType correcto? ✅
   - ¿Range y Radius configurados? ✅

2. **Verifica el Logic asset**:
   - ¿Existe el archivo? ✅
   - ¿Es del tipo correcto? (Skillshot, AOE, etc) ✅

3. **Verifica AbilityDatabase**:
   - ¿Contiene la habilidad? ✅
   - Context Menu > Auto-Find All Abilities

4. **Verifica PlayerCombat**:
   - ¿Está en Ability Slots? ✅
   - ¿Tiene AbilityIndicatorSystem asignado? ✅

---

## 📊 COMPARACIÓN: JSON vs MANUAL

| Aspecto | JSON Import | Manual |
|---------|-------------|--------|
| **Velocidad** | ⚡ Muy rápido (batch) | 🐌 Lento (uno por uno) |
| **Balanceo** | ⭐ Excelente (editar JSON) | ❌ Tedioso |
| **Control** | 🔧 Medio (auto-asigna Logic) | ⭐ Total |
| **Errores** | ✅ Menos (auto-validación) | ⚠️ Más (humanos) |
| **Recomendado** | ✅ Producción | ⚠️ Prototipado |

---

## 🎯 MAPEO: LogicType → IndicatorType

El importer asigna automáticamente:

```
LogicType          → IndicatorType → Indicador Visual
───────────────────────────────────────────────────────
"Targeted"         → None          → Sin indicador (tab-target)
"Direct"           → None          → Sin indicador
"Melee"            → None          → Sin indicador

"Skillshot"        → Line          → LineIndicator (rayo verde)
"Projectile"*      → Line/None     → Depende de TargetingMode

"AoE"              → Circle        → CircleIndicator (disco)
"SelfAOE"          → Circle        → CircleIndicator (centrado)

"Dash"             → Arrow         → ArrowIndicator (flecha)

"Cone"             → Cone          → ConeIndicator (abanico)

"Trap"             → Trap          → TrapIndicator (circle + model)
```

*"Projectile" con TargetingMode="Enemy" → None (legacy)
*"Projectile" con TargetingMode="Ground" → Line (skillshot)

---

## ✅ CHECKLIST DE VALIDACIÓN

Después de importar, verifica:

- [ ] AbilityData assets creados en `Abilities/` folder
- [ ] Logic assets creados en `Logic/` folder
- [ ] Cada AbilityData tiene Logic asignado
- [ ] Cada AbilityData tiene IndicatorType correcto
- [ ] AbilityDatabase contiene todas las habilidades
- [ ] PlayerCombat tiene habilidades en Ability Slots
- [ ] Test en Play mode: Presiona "1" y verifica que funcione

---

## 🚀 RESUMEN

1. **JSON** = Data source (editable, versionable)
2. **AbilityImporter** = Generador automático
3. **AbilityData.asset** = Fuente de verdad en runtime
4. **AbilityLogic.asset** = Cerebro de ejecución
5. **AbilityDatabase** = Registry central

**Workflow**: JSON → Import → Assets → Database → PlayerCombat → Runtime ✨

---

**Fecha**: 2026-01-22
**Autor**: Claude Sonnet 4.5

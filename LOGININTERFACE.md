# Login → Character Creation Screen

## Overview

Rediseño completo de la pantalla de login, transformándola de un formulario básico (nombre + 2 botones de texto) a una pantalla de creación de personaje inmersiva con selección de clase, stats, habilidades y tooltips.

## Archivos Modificados

| Archivo | Cambio |
|---------|--------|
| `1_Data/ScriptableObjects/Core/ClassData.cs` | Campo `Description` agregado |
| `3_Presentation/UI/Views/LoginUI.uxml` | Reescritura completa del layout |
| `3_Presentation/UI/Styles/LoginStyle.uss` | Reescritura completa de estilos |
| `3_Presentation/UI/Controllers/LoginController.cs` | Reescritura mayor del controlador |

No se crearon archivos nuevos.

## Layout

```
┌════════════════════════════════════════════════════════┐
│                    VENERA FATUM                        │
│                                                       │
│  ┌──────────────┐  ┌─────────────────────────────┐   │
│  │ Choose Class  │  │  [Icon]  MAGE               │   │
│  │               │  │  "Masters of the arcane..."  │   │
│  │ [ic] MAGE    ◄│  │                             │   │
│  │ [ic] WARRIOR  │  │  ── Base Stats ──           │   │
│  │               │  │  Health  300    Mana  150    │   │
│  │               │  │  HP/s    2.0    MP/s  5.0    │   │
│  │               │  │  HP/Lvl  +8     MP/Lvl +10  │   │
│  │               │  │                             │   │
│  │               │  │  ── Starting Abilities ──   │   │
│  │               │  │  [ab1] [ab2] [ab3] [ab4]    │   │
│  │               │  │  [ab5] [ab6]                │   │
│  └──────────────┘  └─────────────────────────────┘   │
│                                                       │
│  Enter your name  [________________________]          │
│                                                       │
│               [ ENTER WORLD ]                         │
│               status message                          │
└════════════════════════════════════════════════════════┘
```

Panel de 750px centrado. Columna izquierda (200px): lista scrolleable de clases. Columna derecha (flex-grow): detalle de clase seleccionada. Abajo: input de nombre y botón de conexión.

## Cambios por Archivo

### ClassData.cs

Agregado un campo bajo `ClassIcon` en el header "Identity":

```csharp
[TextArea(2, 5)] public string Description;
```

Retrocompatible — los assets existentes inician con string vacío. El controlador provee fallback descriptions si el campo está vacío.

### LoginUI.uxml

Estructura jerárquica nueva:

- **LoginOverlay** (fullscreen)
  - **LoginPanel** (750px centered)
    - `GameTitle` — "VENERA FATUM"
    - **ContentRow** (flex-direction: row)
      - **ClassListPanel** (200px, izquierda) — título + ScrollView con entries dinámicas
      - **ClassDetailPanel** (flex-grow, derecha)
        - `ClassHeader` — icono 56px + nombre 24px
        - `ClassDescriptionLabel` — texto italic, white-space normal
        - Divider + "Base Stats"
        - `StatsGrid` — 6 stats en 2 columnas (Health, Mana, HP/s, MP/s, HP/Lvl, MP/Lvl)
        - Divider + "Starting Abilities"
        - `AbilitiesRow` — slots 48x48 dinámicos
    - **BottomSection** — name input + connect button + status label
  - **AbilityTooltip** (absolute, hidden) — nombre, categoría, descripción, stats de combate

### LoginStyle.uss

Design language mantenido del original:

- Fondo oscuro: `rgba(20, 20, 30, 0.95)`
- Acentos gold: `rgb(220, 180, 100)`
- Bordes seleccionados: `rgba(120, 90, 50, 0.8)`
- Bordes inactivos: `rgba(80, 80, 100, 0.4)`
- Transiciones: `0.15s`

Clases principales:

| Clase | Uso |
|-------|-----|
| `.class-list-entry` / `--selected` | Cards de clase en la lista izquierda |
| `.class-icon-large`, `.class-name-label` | Header del detalle |
| `.class-description-label` | Descripción italic |
| `.stats-grid`, `.stat-row`, `.stat-label`, `.stat-value` | Grid de stats en 2 columnas |
| `.ability-slot-login`, `.ability-icon-login` | Slots de habilidades 48x48 con hover scale |
| `.ability-tooltip`, `.tooltip-*` | Tooltip flotante estilo ItemTooltip |

### LoginController.cs

**Serialized fields**: `UIDocument uiDocument` + `List<ClassData> availableClasses`

**Auto-discovery**: Si `availableClasses` está vacío, busca automáticamente todos los `ClassData` assets via `AssetDatabase.FindAssets("t:ClassData")` en el editor. Ordenados alfabéticamente.

**Métodos principales**:

| Método | Función |
|--------|---------|
| `AutoFindClasses()` | Auto-descubre ClassData assets si la lista serializada está vacía |
| `BuildClassList()` | Crea entries dinámicamente en el ScrollView (icono + nombre por ClassData) |
| `SelectClass(int)` | Toggle `.class-list-entry--selected`, pobla detalle y habilidades |
| `PopulateDetail(ClassData)` | Llena header, descripción y stats |
| `PopulateAbilities(ClassData)` | Crea slots 48x48 con iconos y registra MouseEnter/MouseLeave |
| `ShowAbilityTooltip(AbilityData, Vector2)` | Posiciona y muestra tooltip con stats del ability |
| `HideAbilityTooltip()` | Oculta tooltip |
| `OnConnectClicked()` | Valida nombre, setea LoginData, conecta via NetworkBootstrap |
| `WaitForPlayerSpawn()` | Coroutine que oculta UI al detectar spawn del player |

**Sin cambios a**: `LoginData.cs`, `NetworkBootstrap.cs`, `PlayerClassManager.cs`.

## Tooltip de Habilidades

Muestra al hacer hover sobre un icono de habilidad:

- **Nombre** (gold, bold)
- **Categoría** (Physical/Magical/Utility + CastType)
- **Descripción** (texto normal)
- **Stats** (solo los relevantes > 0):
  - Mana cost
  - Cooldown (s)
  - Cast Time (s)
  - Range (m)
  - Damage (rojo)
  - Heal (verde)

## Setup Manual (Inspector)

1. **Opcional**: Asignar `Class_Mage.asset` y `Class_Warrior.asset` a la lista `availableClasses` del LoginController. Si no se asignan, se auto-descubren en el editor.
2. **Opcional**: Llenar el campo `Description` en cada ClassData asset. Si está vacío, se usan fallback descriptions hardcodeadas.

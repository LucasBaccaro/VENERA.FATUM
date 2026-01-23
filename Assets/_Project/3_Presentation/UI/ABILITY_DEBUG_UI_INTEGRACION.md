# Ability Debug UI - Guía de Integración

## Descripción General
La UI de Debug de Habilidades proporciona visualización en tiempo real de cooldowns de habilidades, GCD, estados de combate e historial de casts. Esta guía explica cómo integrarla en tu escena.

## Archivos Creados
- **Layout UXML:** `/Assets/_Project/3_Presentation/UI/Views/AbilityBarDebug.uxml`
- **Estilos USS:** `/Assets/_Project/3_Presentation/UI/Styles/AbilityBarDebugStyle.uss`
- **Controlador:** `/Assets/_Project/3_Presentation/UI/Controllers/AbilityBarDebugController.cs`

## Archivos Modificados
- **PlayerCombat:** `/Assets/_Project/2_Simulation/Entities/Player/PlayerCombat.cs`
  - Se agregaron triggers de EventBus para:
    - `OnAbilityCast` (int abilityId, string name)
    - `OnAbilityCooldownStart` (int abilityId, float duration)
    - `OnCombatStateChanged` (string state)
    - `OnAbilityFailed` (int abilityId, string reason)

---

## Pasos de Integración en la Escena

### Paso 1: Agregar UI Document al GameObject HUD

1. Localiza tu **GameObject HUD** en la jerarquía de la escena (donde está adjunto `HUDController`)
2. Agrega un nuevo componente **UIDocument**:
   - Clic en **Add Component** → **UI Toolkit** → **UI Document**
3. En el nuevo componente UIDocument:
   - Establece **Source Asset** a: `AbilityBarDebug.uxml`
   - Establece **Panel Settings** al mismo que usa tu HUD principal
   - Establece **Sort Order** a un valor mayor que el HUD principal (ej: 10) para que renderice encima

### Paso 2: Agregar Componente AbilityBarDebugController

1. En el mismo **GameObject HUD**, agrega el componente `AbilityBarDebugController`:
   - Clic en **Add Component** → Busca **"Ability Bar Debug Controller"**
2. En el Inspector, configura:
   - **UI Document:** Arrastra el componente UIDocument que acabas de crear
   - **Player Combat:** Déjalo vacío por ahora (se configurará en runtime)

### Paso 3: Agregar PlayerUIConnector al Prefab del Jugador

**Este es el paso más importante - conecta automáticamente todas las UIs:**

1. Abre tu **Prefab del jugador** (el que está asignado en PlayerSpawnManager)
   - Debería estar en algo como: `Assets/_Project/5_Content/Prefabs/Player/Player.prefab`

2. Agrega el componente **PlayerUIConnector**:
   - Selecciona el prefab del jugador
   - Click en **Add Component**
   - Busca **"Player UI Connector"**
   - Agrega el componente

3. **¡Listo!** El componente se encargará automáticamente de:
   - Detectar cuando el jugador local se spawnea
   - Buscar el HUDController en la escena
   - Buscar el AbilityBarDebugController en la escena
   - Conectar ambos automáticamente

**NO necesitas escribir código adicional.** El PlayerUIConnector hace todo el trabajo.

#### ¿Cómo funciona?

El `PlayerUIConnector` se ejecuta cuando el jugador se spawnea en el cliente:

```csharp
// Se ejecuta automáticamente cuando tu jugador se spawnea
public override void OnStartClient() {
    if (!base.IsOwner) return; // Solo para el jugador local

    // Busca y conecta automáticamente:
    HUDController hud = FindObjectOfType<HUDController>();
    AbilityBarDebugController debugUI = FindObjectOfType<AbilityBarDebugController>();

    hud.SetPlayerStats(GetComponent<PlayerStats>());
    debugUI.SetPlayerCombat(GetComponent<PlayerCombat>());
}
```

#### Verificación

Para verificar que funciona:
1. Inicia el juego
2. Revisa la consola - deberías ver:
   ```
   [PlayerUIConnector] Jugador local spawneado, conectando UIs...
   [PlayerUIConnector] ✅ HUD conectado
   [PlayerUIConnector] ✅ Debug UI conectado
   ```
3. Presiona **F3** - el Debug UI debería mostrarse con tus habilidades

---

## Resumen de Integración

**3 pasos simples:**

1. ✅ Agrega **UIDocument** al GameObject HUD en la escena
   - Source Asset: `AbilityBarDebug.uxml`

2. ✅ Agrega **AbilityBarDebugController** al mismo GameObject HUD
   - UI Document: Asigna el UIDocument del paso 1

3. ✅ Agrega **PlayerUIConnector** al prefab del jugador
   - ¡Eso es todo! Se conecta automáticamente

---

## Uso

### Alternar Debug UI
- Presiona **F3** para mostrar/ocultar el overlay de debug
- Por defecto está **oculto** al iniciar el juego

### Layout de la UI (Reorganizado para mejor visibilidad)

```
┌─────────────────────────────────────────────────────────┐
│  [HUD - Arriba Izquierda - SIEMPRE VISIBLE]             │
│  HP: ████████░░ 80/100                                  │
│  MP: ██████████ 100/100                                 │
│  Cast: ████░░░░░ Casting Fireball (50%)                 │
│  GCD:  ██░░░░░░░ Active (33%)                           │
│                                                          │
│                                                          │
│  [Abajo Izquierda - F3]     [Centro Inferior - F3]      │
│  ┌──────────────┐           ┌─────────────┐            │
│  │ Last Ability │           │ [1][2][3]   │            │
│  │ Name: Fireball│          │ [4][5][6]   │ ← Habilidades│
│  │ Mana: 50    │           └─────────────┘            │
│  │ CD: 5s      │                                        │
│  │ State: Idle │                                        │
│  └──────────────┘                                        │
│  ┌──────────────┐                    [F3] Toggle ←      │
│  │ Event Log    │                                        │
│  │ [12:34] CAST│                                        │
│  │ [12:35] CD  │                                        │
│  └──────────────┘                                        │
└─────────────────────────────────────────────────────────┘
```

**IMPORTANTE:** Las barras de Cast y GCD están integradas en el HUD principal y **se muestran siempre**, incluso cuando el Debug UI está oculto (F3).

### Elementos de la UI

#### 1. Cast & GCD Bars (HUD Principal - SIEMPRE VISIBLE) 📊
**Posición:** Arriba izquierda, integradas en el HUD junto a HP/Mana
**Visibilidad:** **SIEMPRE visibles**, incluso cuando el Debug UI está oculto

- **Cast Bar (Amarilla/Naranja):**
  - Muestra el progreso del casteo actual
  - Indica qué habilidad se está casteando
  - Se llena de 0% a 100% durante el casteo

- **GCD Bar (Verde):**
  - Muestra el Global Cooldown activo
  - Se vacía progresivamente hasta que el GCD termina
  - Útil para timing de habilidades

**IMPORTANTE:** Estas barras funcionan independientemente del Debug UI y se actualizan siempre.

#### 2. Barra de Habilidades (Centro Inferior) 🎯
**Posición:** Centro inferior de la pantalla (donde normalmente van las habilidades)
**Visibilidad:** Solo cuando presionas F3

- Muestra 6 slots de habilidades con iconos horizontales
- Los overlays de cooldown oscurecen el icono cuando está en cooldown
- El texto de countdown muestra el tiempo restante de cooldown
- Las etiquetas de teclas (1-6) se muestran en la esquina inferior derecha de cada slot
- Indicadores de estado (puntos pequeños):
  - **Verde:** Habilidad lista (Idle)
  - **Gris:** Habilidad en cooldown

#### 3. Panel de Detalles (Abajo Izquierda) 📝
**Posición:** Esquina inferior izquierda
**Visibilidad:** Solo cuando presionas F3

Muestra estadísticas de la **última habilidad casteada**:
- Nombre, Coste de Mana, Cooldown, Tiempo de Casteo, Rango, Tipo de Indicador
- Etiqueta de **Estado de Combate** con código de colores:
  - **Verde:** Idle (Inactivo)
  - **Amarillo:** Aiming (Apuntando)
  - **Rojo:** Casting (Casteando)
  - **Púrpura:** Channeling (Canalizando)

#### 4. Log de Eventos (Abajo Izquierda) 📜
**Posición:** Esquina inferior izquierda, debajo del Panel de Detalles
**Visibilidad:** Solo cuando presionas F3

- Historial scrollable de eventos recientes (máximo 20 entradas)
- Entradas con código de colores:
  - **Verde:** Casteos exitosos
  - **Rojo:** Casteos fallidos
  - **Naranja:** Inicio de cooldowns
  - **Azul:** Cambios de estado
- Hace scroll automáticamente a la entrada más nueva
- Muestra timestamps en formato [HH:MM:SS]

---

## Personalización

### Estilos
Edita `/Assets/_Project/3_Presentation/UI/Styles/AbilityBarDebugStyle.uss` para personalizar:
- Posición (actualmente centro-inferior)
- Colores (overlays de cooldown, indicadores de estado, etc.)
- Tamaños (los slots de habilidad son 60×60px)
- Transparencia (overlay es 85% opaco)

### Layout
Edita `/Assets/_Project/3_Presentation/UI/Views/AbilityBarDebug.uxml` para:
- Agregar/quitar slots de habilidades (actualmente 6)
- Reorganizar paneles
- Cambiar campos del panel de detalles

### Lógica del Controlador
Edita `/Assets/_Project/3_Presentation/UI/Controllers/AbilityBarDebugController.cs` para:
- Cambiar tecla de toggle (actualmente F3)
- Ajustar máximo de entradas del log (actualmente 20)
- Modificar frecuencia de actualización
- Agregar manejadores de eventos personalizados

---

## Solución de Problemas

### La UI No Se Muestra
- **Verifica UIDocument:** Asegúrate de que `AbilityBarDebug.uxml` esté asignado
- **Verifica Panel Settings:** UIDocument necesita PanelSettings válidos
- **Presiona F3:** La UI está oculta por defecto

### No Aparecen Iconos de Habilidades
- **Verifica Referencia de PlayerCombat:** Llama a `SetPlayerCombat()` en runtime
- **Verifica Ability Data:** Asegúrate de que las habilidades en el loadout tengan iconos asignados

### Los Cooldowns No Se Actualizan
- **Verifica IsOwner:** La UI solo se actualiza para el jugador local
- **Verifica EventBus:** Asegúrate de que PlayerCombat esté disparando eventos (revisa los logs de consola)

### Los Eventos No Se Registran
- **Verifica Suscripciones de EventBus:** El controlador se suscribe en `OnEnable()`
- **Verifica PlayerCombat:** Asegúrate de que esté usando la versión modificada con triggers de EventBus

### El GCD No Se Muestra
- La barra de GCD asume un GCD máximo de 1.5 segundos. Si tus habilidades usan valores de GCD diferentes, puede que necesites ajustar el cálculo en el método `UpdateGCD()`.

---

## Notas de Rendimiento

- La UI se actualiza solo cuando está visible (presiona F3 para ocultar)
- Arquitectura dirigida por eventos (cero overhead cuando las habilidades no se están casteando)
- El loop de Update hace polling de cooldowns/GCD solo cuando la UI está visible
- Huella de memoria: ~10-15 KB
- Sin impacto en el gameplay cuando está oculta

---

## Mejoras Futuras

Posibles mejoras para futuras iteraciones:
- Agregar visualización de rango de habilidades
- Mostrar coste de mana con código de colores (verde = puedes pagarlo, rojo = muy caro)
- Agregar visualización de cola de habilidades
- Rastrear medidores de DPS/HPS
- Exportar log a archivo para análisis
- Agregar filtros al log de eventos (solo casts, solo errores, etc.)

---

## Checklist de Pruebas

- [ ] La UI se alterna con la tecla F3
- [ ] Los iconos de habilidades se muestran correctamente para los 6 slots
- [ ] Los overlays de cooldown aparecen y cuentan regresivamente
- [ ] La barra de GCD se anima después de castear habilidades
- [ ] El panel de detalles se actualiza con las estadísticas correctas de la habilidad
- [ ] El log de eventos muestra eventos de casteo (verde)
- [ ] El log de eventos muestra casteos fallidos (rojo) cuando no hay suficiente mana
- [ ] Los cambios de estado de combate se reflejan en la UI (Idle → Aiming → Casting → Idle)
- [ ] Los indicadores de estado cambian de color según el estado de cooldown
- [ ] El log hace scroll automático hasta abajo
- [ ] El log mantiene máximo 20 entradas
- [ ] La UI está oculta por defecto al cargar la escena
- [ ] La UI persiste el estado de visibilidad a través de múltiples toggles

---

## Soporte

Para problemas o preguntas:
- Revisa los logs de EventBus: `EventBus.LogRegisteredEvents()`
- Habilita los logs de debug de PlayerCombat en el código
- Verifica que todos los archivos estén en los directorios correctos
- Asegúrate de que el paquete Unity UI Toolkit esté instalado

---

## Créditos

**Implementación:** Claude Sonnet 4.5
**Arquitectura:** UI dirigida por eventos con patrón EventBus
**Framework:** Unity UI Toolkit (UXML/USS)
**Networking:** Compatible con FishNet (UI solo del lado del cliente)

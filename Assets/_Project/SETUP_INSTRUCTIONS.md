# INSTRUCCIONES DE SETUP - FASE 1 COMPLETADA

La estructura de código está lista. Ahora necesitas configurar algunas cosas manualmente en Unity.

## ✅ YA COMPLETADO (por código)
- Estructura de carpetas `_Project/` completa
- Assembly Definitions (.asmdef) configurados
- Scripts de arquitectura core:
  - ServiceLocator
  - EventBus
  - ObjectPool + ObjectPoolManager
  - Singleton pattern
  - Utils (MathUtils, Extensions, LayerMasks)
- NetworkBootstrap y EntryPoint creados

---

## 📋 TAREAS MANUALES EN UNITY

### 1. CONFIGURAR LAYERS (Project Settings > Tags and Layers)

Abre `Edit > Project Settings > Tags and Layers` y configura:

```
Layer 3:  Player
Layer 6:  Enemy
Layer 7:  Projectile
Layer 8:  Environment
Layer 9:  SafeZone
Layer 10: Loot
Layer 11: Interactable
```

### 2. CONFIGURAR COLLISION MATRIX (Project Settings > Physics)

Abre `Edit > Project Settings > Physics` y desactiva las siguientes colisiones:

**Desactivar (NO deben colisionar):**
- Player ↔ Player
- Projectile ↔ Projectile
- SafeZone ↔ Todo (es solo trigger)

**Settings adicionales:**
- Fixed Timestep: `0.02` (50Hz)
- Default Contact Offset: `0.01`
- Queries Hit Triggers: `OFF` ⚠️ IMPORTANTE

### 3. CREAR ESCENA BOOTSTRAP

1. Crear nueva escena: `File > New Scene`
2. Guardarla como: `Assets/_Project/4_Bootstrap/Bootstrap.unity`
3. Agregar los siguientes GameObjects:

```
Bootstrap (Scene)
├── [MANAGERS]
│   ├── EntryPoint (Empty GameObject)
│   │   └── EntryPoint.cs (script)
│   │
│   ├── NetworkManager (Empty GameObject)
│   │   └── Add Component > FishNet > NetworkManager
│   │   └── Add Component > NetworkBootstrap.cs
│   │
│   └── ObjectPoolManager (Empty GameObject)
│       └── ObjectPoolManager.cs (script)
│
└── [UI ROOT] (para después)
```

### 4. CONFIGURAR NETWORKMANAGER (Inspector)

Selecciona el GameObject `NetworkManager` y configura:

**FishNet NetworkManager:**
- Transport: Tugboat (debería estar por defecto)

**Server Manager:**
- Max Connections: `50`
- Timeout: `60`

**Client Manager:**
- (dejar por defecto por ahora)

**Time Manager:**
- Tick Rate: `50` (20ms tick)
- Physics Mode: `Unity Physics`

**NetworkBootstrap (script):**
- Network Manager: Arrastra el NetworkManager aquí
- Auto Start Server: `TRUE` (solo para testing)
- Auto Start Client: `FALSE`

### 5. CONFIGURAR BUILD SETTINGS

1. `File > Build Settings`
2. Agregar la escena Bootstrap:
   - Click "Add Open Scenes"
   - Asegúrate que Bootstrap sea la escena índice 0

### 6. VERIFICAR COMPILACIÓN

Vuelve a Unity y espera a que compile. Deberías ver:
- ✅ Sin errores de compilación
- ✅ Los 5 assemblies (Genesis.Core, Data, Simulation, Presentation, Bootstrap) compilados
- ✅ Scripts reconocidos en los GameObjects

---

## 🧪 TEST: PRIMERA CONEXIÓN

Una vez configurado todo:

1. **Asegúrate de estar en la escena Bootstrap**
2. Click en **Play**
3. En la consola deberías ver:
   ```
   [EntryPoint] === GENESIS - Entry Point ===
   [EntryPoint] ServiceLocator initialized
   [EntryPoint] EventBus initialized
   [NetworkBootstrap] Server started successfully
   ```

4. **Para probar con 2 clientes** (usando ParrelSync):
   - `ParrelSync > Clones Manager > Create New Clone`
   - Abre el clon
   - En el clon: Desactiva "Auto Start Server" y activa "Auto Start Client"
   - Click Play en ambos (original + clon)
   - Deberían conectarse

---

## 🎯 SIGUIENTE PASO: FASE 2

Una vez que los 2 clientes se conecten correctamente, avísame y continuaremos con **FASE 2: Entity Basics** (crear el Player prefab y movimiento).

---

**¿Algún problema? Avísame en qué paso estás.**

# DEBUG: PLAYER NO SPAWNA - CHECKLIST

## 🔍 PASO 1: VERIFICAR CONSOLA

**Click Play y revisa la consola de Unity:**

### Mensajes que DEBERÍAS ver:
```
[EntryPoint] === GENESIS - Entry Point ===
[NetworkBootstrap] Server started successfully
[PlayerSpawnManager] Initialized
[PlayerSpawnManager] Player spawned for connection X at (0, 0.5, 0)
```

### ¿Qué ves en consola?
- ✅ Si ves "Player spawned" → El spawn funciona, el problema es visual
- ❌ Si NO ves "Player spawned" → PlayerSpawnManager no está funcionando
- ❌ Si ves errores en rojo → Cópiame el error completo

---

## 🔍 PASO 2: VERIFICAR HIERARCHY DURANTE PLAY

1. Click **Play**
2. Mira la **Hierarchy** panel
3. ¿Aparece un GameObject llamado "Player(Clone)" o similar?

### Si SÍ aparece:
- El spawn funciona ✅
- El problema es que la **cámara no lo ve**
- Ve a PASO 4

### Si NO aparece:
- El spawn NO funciona ❌
- Ve a PASO 3

---

## 🔍 PASO 3: VERIFICAR CONFIGURACIÓN SPAWN

### A) PlayerSpawnManager en Bootstrap

**En Hierarchy (Bootstrap scene):**
```
[MANAGERS]
└── PlayerSpawnManager ← ¿Existe?
    └── Script: PlayerSpawnManager.cs ← ¿Asignado?
```

**En Inspector del PlayerSpawnManager:**
- **Player Prefab:** ¿Tiene asignado el prefab Player? ⚠️ CRÍTICO
- **Spawn Points:** ¿Tiene al menos 1 Transform asignado?

### B) Verificar que el Prefab Player existe

**En Project:**
- `Assets/_Project/5_Content/Prefabs/Player/Player.prefab` ← ¿Existe?

**El prefab debe tener:**
- NetworkObject component
- NetworkTransform component
- PlayerController component
- PlayerStats component

---

## 🔍 PASO 4: PROBLEMA DE CÁMARA (Si el player SÍ spawna)

Si ves "Player(Clone)" en Hierarchy pero no lo ves en Game view:

### Solución: Reposicionar Cámara

1. Selecciona **Main Camera** en Hierarchy
2. En Inspector, cambia:
   ```
   Position: (0, 10, -10)
   Rotation: (45, 0, 0)
   ```
3. Debería ver ahora el jugador desde arriba

### O Seleccionar el Player manualmente:
1. Durante Play, en Hierarchy
2. Doble-click en "Player(Clone)"
3. La Scene view se centrará en él
4. ¿Lo ves ahí? Entonces el problema ES la cámara

---

## 🔍 PASO 5: VERIFICAR LAYER & CULLING

Si el player existe pero es invisible:

1. Selecciona "Player(Clone)" en Hierarchy
2. Inspector > Layer: Debería ser "Player" (Layer 3)
3. Selecciona Main Camera
4. Inspector > Culling Mask: Asegúrate que "Player" esté ✅ activado

---

## 🐛 ERRORES COMUNES

### Error: "Player prefab no asignado"
- **Solución:** Asigna el prefab en PlayerSpawnManager Inspector

### Error: "NetworkObject not found"
- **Solución:** El prefab Player no tiene NetworkObject component

### Player spawna en (0, 0, 0) y cae infinito
- **Solución:** Crea un Plane en (0, 0, 0) con Layer "Environment"

---

## 📊 REPORTE DE DEBUG

**Por favor, responde estas preguntas:**

1. ¿Qué ves en la consola cuando le das Play?
2. ¿Aparece "Player(Clone)" en Hierarchy?
3. ¿Tienes el prefab Player creado en la carpeta correcta?
4. ¿PlayerSpawnManager tiene el prefab asignado?
5. Screenshot de PlayerSpawnManager Inspector (opcional)

---

**Con esta info puedo ayudarte a resolver el problema exacto!** 🔧

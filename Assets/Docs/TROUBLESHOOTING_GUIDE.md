# World Streaming - Guía de Diagnóstico

## 🔍 Herramientas de Debug

He agregado logs detallados y herramientas de diagnóstico. Usa estos comandos:

### Menu Tools (Editor)
```
Tools > World Streaming > Debug: Verify World Setup
Tools > World Streaming > Debug: List All ChunkData in WorldDatabase
Tools > World Streaming > Debug: Test Chunk Coordinate Math
Tools > World Streaming > Debug: Check Player Prefab Components
```

---

## ❌ PROBLEMA 1: Safe Zone No Funciona (Jugadores se pueden atacar)

### Síntomas
- No aparecen logs de entrada/salida de safe zone
- Jugadores pueden atacarse dentro del trigger
- UI de safe zone no aparece

### Paso 1: Verificar Layer del Trigger

**En la escena con el safe zone trigger:**

```
1. Select el GameObject con ZoneTrigger
2. Inspector > Top-right > Layer dropdown
3. Verificar que dice: "SafeZone" (Layer 9)
```

**Si dice "Default" o cualquier otro:**
```
Layer dropdown > SafeZone (9)
Save Scene
```

### Paso 2: Verificar Collider Configuration

**Select el GameObject con ZoneTrigger:**

```
Inspector > Box Collider:
✅ Is Trigger: DEBE estar CHECKED
✅ Size: Debe cubrir el área deseada (ej: 50, 20, 50)
✅ Center: (0, 0, 0) o ajustado según necesidad
```

### Paso 3: Verificar ZoneTrigger Component

```
Inspector > ZoneTrigger:
- Zone Type: SafeZone
- Zone Name: "Tu nombre de zona"
```

**Si no tiene ZoneTrigger component:**
```
Add Component > ZoneTrigger
Configure Zone Type y Zone Name
Save Scene
```

### Paso 4: Verificar Player Prefab

**Run menu tool:**
```
Tools > World Streaming > Debug: Check Player Prefab Components
```

**Debe tener:**
```
✅ NetworkObject
✅ PlayerChunkTracker
✅ PlayerState ← CRÍTICO para safe zones
✅ PlayerSpawnHandler
```

**Si falta PlayerState:**
```
1. Open Player Prefab
2. Add Component > PlayerState
3. Save Prefab
4. Restart Play Mode
```

### Paso 5: Verificar Collision Matrix

```
Edit > Project Settings > Physics > Layer Collision Matrix

Layer 9 (SafeZone) debe estar DESMARCADO para:
❌ Default (0)
❌ Player (3)
❌ Enemy (6)
❌ Environment (8)
❌ SafeZone (9) ← También self-collision!
```

**Por qué:** Si está marcado, el trigger bloqueará movimiento en lugar de solo detectar.

### Paso 6: Testing con Logs Mejorados

**Start Play Mode:**

1. **Al iniciar, buscar en Console:**
```
[ZoneTrigger] Initialized: Tu Zona | Layer: 9 | IsTrigger: True
```

Si dice `Layer: 0` → El layer está mal configurado (volver a Paso 1)

2. **Caminar hacia el trigger:**

**Esperado en Console (Server):**
```
[ZoneTrigger] OnTriggerEnter detected: Player(Clone) | Layer: 3 | IsServer: True
[PlayerState] Safe zone changed: False -> True
✅ Player X ENTERED Tu Zona (en verde)
```

**Esperado en Console (Client owner):**
```
[PlayerState] OnSafeZoneChanged: False -> True | IsOwner: True
✅ EventBus triggered for UI: True (en cyan)
```

3. **Si NO ves logs:**

**Caso A: No detecta OnTriggerEnter**
- Problema: Collision Matrix bloqueando
- Fix: Paso 5 (desmarcar Layer 9)

**Caso B: Detecta pero dice "No NetworkObject"**
- Problema: Player no tiene NetworkObject
- Fix: Verificar que el player prefab tiene NetworkObject component

**Caso C: Detecta pero dice "No PlayerState"**
- Problema: PlayerState falta en prefab
- Fix: Paso 4 (agregar PlayerState)

**Caso D: Detecta pero dice "Ignoring trigger (not server)"**
- Normal en cliente
- Debe aparecer en la ventana de Console cuando corres como Server

### Paso 7: Verificar Combat Blocking

**Con 2 jugadores conectados:**

1. Player A camina a safe zone
2. Buscar en Console:
```
✅ Player 1 ENTERED Safe Zone (verde)
[PlayerState] Safe zone changed: False -> True (amarillo)
```

3. Player B dispara a Player A
4. Buscar en Console:
```
[CombatValidator] Victim PlayerA safe zone state: True
❌ BLOCKED: Target is in a safe zone (rojo)
[ProjectileController] Projectile hit blocked: Target is in a safe zone
```

**Si NO bloquea el daño:**

**Verificar que PlayerStats.cs tiene el código:**
```csharp
[Server]
public void TakeDamage(float damage, NetworkObject attacker) {
    if (_isDead) return;

    // ═══ SAFE ZONE VALIDATION ═══
    if (!CombatValidator.CanApplyDamage(base.NetworkObject, attacker, out string reason)) {
        Debug.Log($"[PlayerStats] Damage blocked: {reason}");
        return;
    }
    // ... resto del código
}
```

Si falta, revisa que el archivo `PlayerStats.cs` tenga el `using Genesis.Simulation.World;` y la validación.

---

## ❌ PROBLEMA 2: Chunk_0_2 No Se Carga

### Síntomas
- Chunk_0_1 carga correctamente
- Chunk_0_2 NO carga cuando estás en Chunk_0_1
- Scene no aparece en Hierarchy

### Paso 1: Verificar Coordenadas

**Chunk_0_1 está en Y=1, sus vecinos son:**

```
(-1, 2)  (0, 2)  (1, 2)  ← Chunk_0_2 DEBERÍA estar aquí
(-1, 1)  (0, 1)  (1, 1)  ← Tu chunk actual
(-1, 0)  (0, 0)  (1, 0)
```

**Si tu Chunk_0_2 tiene coordenadas (0, 2):**
✅ Correcto - es vecino de Chunk_0_1

**Si tu Chunk_0_2 tiene otras coordenadas:**
❌ Incorrecto - cambia el ChunkData

### Paso 2: Verificar ChunkData

**Run menu tool:**
```
Tools > World Streaming > Debug: List All ChunkData in WorldDatabase
```

**Buscar en Console:**
```
Chunk(0, 0) | Name: ... | Scene: Chunk_0_0 | Spawns: 3
Chunk(0, 1) | Name: ... | Scene: Chunk_0_1 | Spawns: 0
Chunk(0, 2) | Name: ... | Scene: Chunk_0_2 | Spawns: 0  ← DEBE aparecer
```

**Si NO aparece Chunk(0, 2):**
```
Problema: ChunkData no está en WorldDatabase
Fix:
1. Open WorldDatabase.asset (Resources/Databases/)
2. Chunks > Add Chunk_0_2_Data
3. Save
```

**Si aparece pero Scene está vacío:**
```
Problema: ChunkData.SceneName está vacío
Fix:
1. Open Chunk_0_2_Data.asset
2. SceneName: "Chunk_0_2"  ← Sin .unity
3. Save
```

### Paso 3: Verificar Escena en Build Settings

**Run menu tool:**
```
Tools > World Streaming > Debug: Verify World Setup
```

**Buscar en Console:**
```
[3] Checking Build Settings (Chunk scenes)...
Found X chunk scenes in Build Settings:
  ✅ | Chunk_0_0 | Path: ...
  ✅ | Chunk_0_1 | Path: ...
  ✅ | Chunk_0_2 | Path: ...  ← DEBE aparecer
```

**Si Chunk_0_2 NO aparece:**
```
Fix:
Menu: Tools > World Streaming > Add Chunk Scenes to Build Settings
```

**Si aparece pero con ❌ DISABLED:**
```
Fix:
1. File > Build Settings
2. Find Chunk_0_2 in list
3. Check the checkbox to enable it
4. Close Build Settings
```

### Paso 4: Verificar Nombre de Escena

**IMPORTANTE:** El nombre debe coincidir EXACTAMENTE:

```
Archivo:    Chunk_0_2.unity
ChunkData:  SceneName = "Chunk_0_2"  (sin .unity)
```

**Si no coincide:**
```
Fix:
1. Rename scene file a: Chunk_0_2.unity
2. Open ChunkData asset
3. SceneName: "Chunk_0_2"
4. Save
5. Re-run: Tools > Add Chunk Scenes to Build Settings
```

### Paso 5: Testing con Logs Mejorados

**Start Play Mode y caminar a Chunk_0_1:**

**Buscar en Console:**
```
═══ PLAYER MOVED TO CHUNK Chunk(0, 1) ═══
Required chunks (9-slice): Chunk(0, 1), Chunk(-1, 0), Chunk(0, 0), ...
Chunks to LOAD: Chunk(0, 2), Chunk(-1, 2), Chunk(1, 2), ...  ← Chunk(0, 2) DEBE aparecer
```

**Luego, para CADA chunk a cargar:**
```
📥 Attempting to load chunk Chunk(0, 2)...
Found ChunkData: Chunk_0_2_Data | SceneName: 'Chunk_0_2' | Coordinate: (0, 2)
Starting async load for scene: Chunk_0_2
✅ Loaded chunk Chunk(0, 2) - Scene: Chunk_0_2 (Client)
```

**Si ves:**
```
❌ No ChunkData found for Chunk(0, 2) in WorldDatabase!
```
→ Problema: ChunkData falta (volver a Paso 2)

**Si ves:**
```
❌ ChunkData has empty SceneName!
```
→ Problema: SceneName vacío en ChunkData (volver a Paso 2)

**Si ves:**
```
❌ LoadSceneAsync returned null! Scene not in Build Settings?
```
→ Problema: Escena no está en Build Settings (volver a Paso 3)

### Paso 6: Test Coordinate Math

**Run menu tool:**
```
Tools > World Streaming > Debug: Test Chunk Coordinate Math
```

**Buscar output:**
```
--- Testing 9-slice grid from Chunk(0, 1) ---
Center: Chunk(0, 1)
Neighbors (8): Chunk(-1, 2), Chunk(0, 2), Chunk(1, 2), ...
```

**Verificar que Chunk(0, 2) aparece en la lista de vecinos.**

---

## 🎯 Checklist Rápido

### Safe Zone No Funciona

```
✅ Layer 9 creado y nombrado "SafeZone"
✅ Trigger GameObject en Layer 9
✅ Box Collider con Is Trigger = true
✅ ZoneTrigger component agregado
✅ Player Prefab tiene PlayerState component
✅ Collision Matrix: Layer 9 desmarcado para todas las capas
✅ PlayerStats.cs tiene código de CombatValidator
✅ ProjectileController.cs tiene código de CombatValidator
```

### Chunk No Se Carga

```
✅ ChunkData asset existe para el chunk
✅ ChunkData.Coordinate correcto (ej: 0, 2)
✅ ChunkData.SceneName correcto (ej: "Chunk_0_2")
✅ Escena existe: Chunk_0_2.unity
✅ Escena en Build Settings (enabled)
✅ ChunkData en WorldDatabase
✅ WorldDatabase en Resources/Databases/
```

---

## 📞 Si Aún No Funciona

1. **Ejecuta TODOS los comandos de Tools > World Streaming > Debug**
2. **Copia TODOS los logs de Console** (especialmente los rojos/amarillos)
3. **Toma screenshot de:**
   - Inspector del GameObject con ZoneTrigger
   - WorldDatabase.asset con lista de chunks
   - Build Settings con lista de escenas
   - Player Prefab con componentes
4. **Envía esa info para diagnóstico específico**

---

## 🔧 Comandos de Debug Disponibles

### En Editor (Menu Tools)
```
Debug: Verify World Setup
  → Verifica WorldDatabase, ChunkData, Build Settings, Layers

Debug: List All ChunkData in WorldDatabase
  → Lista todos los chunks registrados

Debug: Test Chunk Coordinate Math
  → Verifica que el cálculo de 9-slice funciona

Debug: Check Player Prefab Components
  → Verifica que el player tenga los componentes necesarios

Add Chunk Scenes to Build Settings
  → Agrega automáticamente todas las escenas de chunks

List All Chunk Scenes in Build Settings
  → Muestra qué escenas están en el build
```

### En Play Mode (Console Logs)

**Safe Zone:**
```
[ZoneTrigger] Initialized: ...
[ZoneTrigger] OnTriggerEnter detected: ...
✅ Player X ENTERED ... (verde)
[PlayerState] Safe zone changed: ...
✅ EventBus triggered for UI (cyan)
[CombatValidator] Victim safe zone state: ...
❌ BLOCKED: ... (rojo si bloquea)
```

**Chunk Loading:**
```
═══ PLAYER MOVED TO CHUNK ... ═══
Required chunks (9-slice): ...
Chunks to LOAD: ...
📥 Attempting to load chunk ...
✅ Loaded chunk ... (verde)
```

---

**Usa los logs de colores para diagnosticar rápidamente!**
- 🟢 Verde = Operación exitosa
- 🟡 Amarillo = Estado cambiado
- 🔵 Cyan = Evento UI disparado
- 🔴 Rojo = Error o bloqueo

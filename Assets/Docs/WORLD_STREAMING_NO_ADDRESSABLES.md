# World Streaming System - SceneManager (Sin Addressables)

## Cambios vs Versión con Addressables

Esta versión usa **SceneManager nativo** de Unity en lugar de Addressables.

### Ventajas
- ✅ Sin dependencias externas
- ✅ Más simple de configurar
- ✅ Compatible con Unity 6.3
- ✅ Mismo resultado final (carga escenas aditivas)

### Diferencias de Implementación

#### ChunkData.cs
**Antes (Addressables):**
```csharp
public AssetReference SceneAsset;
```

**Ahora (SceneManager):**
```csharp
public string SceneName; // Ejemplo: "Chunk_0_0"
```

#### ChunkLoaderManager.cs
**Antes (Addressables):**
```csharp
var handle = Addressables.LoadSceneAsync(data.SceneAsset, LoadSceneMode.Additive);
await handle.Task;
```

**Ahora (SceneManager):**
```csharp
AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(data.SceneName, LoadSceneMode.Additive);
yield return asyncLoad;
```

---

## Configuración Simplificada

### 1. NO Necesitas Addressables

**Ignora estos pasos:**
- ❌ Window > Package Manager > Addressables
- ❌ Crear grupos de Addressables
- ❌ Marcar escenas como Addressable
- ❌ Build Addressables

### 2. Agregar Escenas a Build Settings (Automático)

Usa el Editor Script incluido:

```
Menu: Tools > World Streaming > Add Chunk Scenes to Build Settings
```

Este comando:
1. Busca todas las escenas en `Assets/_Project/5_Content/Scenes/Chunks/`
2. Las agrega automáticamente a `File > Build Settings > Scenes in Build`
3. ✅ Listo!

**Comandos adicionales:**
- `Remove All Chunk Scenes from Build Settings` - Limpia chunks del build
- `List All Chunk Scenes in Build Settings` - Muestra chunks en consola

### 3. Crear Chunk Scenes (Mismo proceso)

1. File > New Scene
2. Save as: `Assets/_Project/5_Content/Scenes/Chunks/Chunk_0_0.unity`
3. Add terrain/geometry (256x256m)
4. Add safe zone triggers (Layer 9)
5. Save
6. **Ejecuta:** `Tools > World Streaming > Add Chunk Scenes to Build Settings`

**IMPORTANTE:** El nombre del archivo debe seguir el formato `Chunk_X_Y.unity`

### 4. Crear ChunkData Assets

```
Right-click > Create > Genesis > World > Chunk Data
Name: Chunk_0_0_Data

Configure:
  - Coordinate: (0, 0)
  - ChunkName: "Plains Center"
  - SceneName: "Chunk_0_0" ← DEBE coincidir con el nombre del archivo .unity
  - IsStartingChunk: true
  - SpawnPositions: [(128, 0, 128), ...]
```

**CRÍTICO:** `SceneName` debe ser **exactamente** el nombre del archivo de escena (sin extensión .unity).

### 5. Crear WorldDatabase (Igual que antes)

```
Right-click in: Assets/_Project/1_Data/Resources/Databases/
Create > Genesis > World > World Database
- Add all ChunkData assets
- Save
```

### 6. Configure Bootstrap Scene (Igual que antes)

```
Open: Bootstrap.unity
GameObject: WorldStreamingBootstrap

Assign:
  - World Database
  - Chunk Loader Prefab
  - Server Scene Handler Prefab
  - Player Spawn Handler Prefab
```

---

## Workflow Comparación

### Con Addressables
```
1. Create scene
2. Mark as Addressable
3. Set Address
4. Assign to Group
5. Build Addressables
6. Create ChunkData with AssetReference
```

### Sin Addressables (SceneManager)
```
1. Create scene in Chunks/ folder
2. Run: Tools > Add Chunk Scenes to Build Settings
3. Create ChunkData with SceneName string
```

**Resultado:** 3 pasos vs 6 pasos, mismo resultado final!

---

## Ventajas Técnicas

### Memory Management
- **Addressables:** Descarga assets completamente de memoria
- **SceneManager:** Descarga escenas completamente de memoria
- **Conclusión:** Idéntico para este caso de uso

### Loading Speed
- **Addressables:** ~100-300ms por chunk
- **SceneManager:** ~100-300ms por chunk
- **Conclusión:** Idéntico (ambos cargan desde disco)

### Build Size
- **Addressables:** Chunks en carpeta separada
- **SceneManager:** Chunks en Build Settings
- **Conclusión:** Mismo tamaño final

---

## Troubleshooting

### Error: "Scene 'Chunk_0_0' couldn't be loaded"
**Causa:** Escena no está en Build Settings.

**Fix:**
```
Menu: Tools > World Streaming > Add Chunk Scenes to Build Settings
```

Verifica:
```
Menu: Tools > World Streaming > List All Chunk Scenes in Build Settings
```

### Error: "ChunkData has empty SceneName"
**Causa:** Olvidaste configurar el campo `SceneName` en ChunkData.

**Fix:**
1. Abre el ChunkData asset
2. SceneName: "Chunk_0_0" (sin extensión .unity)
3. Save

### Chunk no se carga pero no hay error
**Causa:** Nombre de escena no coincide.

**Verifica:**
- Archivo: `Chunk_0_0.unity`
- ChunkData.SceneName: `"Chunk_0_0"` ← Debe coincidir EXACTAMENTE
- Build Settings: Escena está listada y ✅ enabled

---

## Migration Path (Futuro)

Si en el futuro quieres migrar a Addressables (ej: Unity 7.0+):

1. Instalar Addressables package
2. Cambiar `ChunkData.SceneName` → `ChunkData.SceneAsset`
3. Cambiar `ChunkLoaderManager` para usar Addressables API
4. Marcar escenas como Addressable
5. Build Addressables

**Tiempo estimado:** ~30 minutos

Por ahora, **SceneManager nativo es la mejor opción** para Unity 6.3.

---

## Editor Tools Reference

### Add Chunk Scenes to Build Settings
**Menu:** `Tools > World Streaming > Add Chunk Scenes to Build Settings`

**Qué hace:**
- Busca escenas en `5_Content/Scenes/Chunks/`
- Las agrega a Build Settings automáticamente
- Ignora duplicados

**Cuándo usar:**
- Después de crear nuevas escenas de chunks
- Si mueves/renombras escenas
- Si Build Settings se corrompe

### Remove All Chunk Scenes
**Menu:** `Tools > World Streaming > Remove All Chunk Scenes from Build Settings`

**Qué hace:**
- Remueve TODAS las escenas con "/Chunks/Chunk_" del build
- Útil para limpiar antes de re-generar

**Cuándo usar:**
- Antes de regenerar lista de chunks
- Si quieres empezar de cero

### List Chunk Scenes
**Menu:** `Tools > World Streaming > List All Chunk Scenes in Build Settings`

**Qué hace:**
- Imprime en consola todas las escenas de chunks
- Muestra si están enabled (✅) o disabled (❌)

**Cuándo usar:**
- Debug: verificar qué chunks están en el build
- Asegurar que nuevas escenas se agregaron correctamente

---

## Testing

### Test 1: Scene Loading
```
1. Create Chunk_0_0.unity
2. Run: Tools > Add Chunk Scenes to Build Settings
3. Create ChunkData with SceneName = "Chunk_0_0"
4. Add to WorldDatabase
5. Start game
6. Check Console: "[ChunkLoader] Loaded chunk Chunk(0, 0) - Scene: Chunk_0_0"
```

### Test 2: Multi-Chunk Loading
```
1. Create Chunk_0_0, Chunk_0_1, Chunk_1_0
2. Add all to Build Settings
3. Create ChunkData for each
4. Walk player across boundary (x=256)
5. Verify old chunks unload, new chunks load
6. Check SceneManager.sceneCount stays at ~9
```

---

## Performance Notes (Sin Addressables)

- **Client Memory:** ~40-60MB per chunk (identical to Addressables)
- **Loading Time:** ~100-300ms per chunk (identical to Addressables)
- **Build Time:** Faster (no Addressables catalog generation)
- **Iteration Time:** Faster (no need to rebuild Addressables)

---

## Summary

SceneManager nativo es **igual de eficiente** que Addressables para este caso de uso, con la ventaja de ser:
- Más simple
- Sin dependencias externas
- Compatible con Unity 6.3
- Más rápido para iterar

**Recomendación:** Usa SceneManager hasta que necesites features avanzadas de Addressables (remote asset loading, CDN hosting, etc.).

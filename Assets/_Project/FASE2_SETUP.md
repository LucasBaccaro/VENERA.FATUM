# FASE 2 - ENTITY BASICS - SETUP MANUAL

La arquitectura de jugador está implementada. Ahora necesitas crear el prefab y configurar la escena.

## ✅ YA COMPLETADO (Scripts)
- **PlayerController**: Movimiento WASD con KCC custom
- **PlayerStats**: HP/Mana sincronizados con SyncVars
- **IDamageable/IInteractable**: Interfaces base
- **HUDController**: Controlador de UI
- **HUD.uxml + MainStyle.uss**: UI Toolkit assets
- **PlayerSpawnManager**: Gestión de spawning

---

## 📋 CONFIGURACIÓN MANUAL EN UNITY

### 1. CREAR PREFAB DE PLAYER

#### Paso 1: Crear el GameObject
1. En la escena, crea: `GameObject > 3D Object > Capsule`
2. Renómbralo a: `Player`
3. Configura:
   - Position: `(0, 1, 0)`
   - Scale: `(1, 1, 1)`

#### Paso 2: Agregar Scripts al Player
Selecciona el GameObject `Player` y agrega estos componentes en orden:

1. **NetworkObject** (FishNet)
   - Is Networked: `TRUE`
   - Is Global: `FALSE`

2. **NetworkTransform** (FishNet) ⚠️ CRÍTICO PARA FISHNET V4
   - Synchronize Position: `TRUE`
   - Synchronize Rotation: `TRUE`
   - Synchronize Scale: `FALSE`
   - Interpolation: `10`

3. **PlayerController** (script)
   - Move Speed: `6`
   - Rotation Speed: `10`
   - Gravity: `-15`
   - Ground Check Radius: `0.3`
   - Ground Check Distance: `0.1`
   - Ground Layer: `Environment` (Layer 8)
   - Model Transform: Arrastra el mismo GameObject `Player` aquí
   - Animator: (dejar vacío por ahora)

4. **PlayerStats** (script)
   - Max Health: `100`
   - Max Mana: `100`
   - Mana Regen Per Second: `5`

#### Paso 3: Configurar Collider
El Capsule ya tiene un `CapsuleCollider`. Configúralo:
- Is Trigger: `FALSE`
- Radius: `0.5`
- Height: `2`
- Center: `(0, 0, 0)`

#### Paso 4: Configurar Layer
- Selecciona el GameObject `Player`
- En el Inspector (arriba a la derecha): Layer > `Player` (Layer 3)

#### Paso 5: Crear Prefab
1. Arrastra el GameObject `Player` desde la Hierarchy a:
   `Assets/_Project/5_Content/Prefabs/Player/`
2. Elimina el GameObject de la escena (ya tenemos el prefab)

---

### 2. CONFIGURAR SPAWN MANAGER EN BOOTSTRAP

Abre la escena `Bootstrap.unity`.

#### Crear GameObject SpawnManager
1. Crea: `GameObject > Create Empty`
2. Renómbralo: `PlayerSpawnManager`
3. Muévelo dentro de `[MANAGERS]` en la jerarquía
4. Agrega el script: `PlayerSpawnManager.cs`

#### Configurar Inspector
- **Player Prefab**: Arrastra el prefab `Player` que acabas de crear
- **Spawn Points**: Size `1`
  - Element 0: Crea un Empty GameObject llamado `SpawnPoint_01`
  - Posición: `(0, 0.5, 0)`
  - Arrastra `SpawnPoint_01` al slot
- **Randomize Spawn Point**: `TRUE`

---

### 3. CONFIGURAR HUD (UI)

#### Crear UI Document
1. En la escena Bootstrap, crea: `GameObject > UI Toolkit > UI Document`
2. Renómbralo: `HUD`
3. Configura el Inspector:
   - **Source Asset**: Arrastra `Assets/_Project/3_Presentation/UI/Views/HUD.uxml`
   - **Panel Settings**: (dejar por defecto)

#### Agregar HUDController
1. Selecciona el GameObject `HUD`
2. Agrega el script: `HUDController.cs`
3. En el Inspector:
   - **Ui Document**: Debería auto-detectarse, si no, arrástralo

#### Aplicar Estilos (Opcional - para mejorar visual)
1. Abre el archivo `HUD.uxml` en Unity (doble click)
2. En el Editor UI Builder, selecciona el root element `HUD`
3. En `StyleSheets` > Click `+` > Arrastra `MainStyle.uss`
4. Guarda (Ctrl+S)

---

### 4. CONFIGURAR INPUT SYSTEM

Si aún no tienes configurado el Input System:

1. `Edit > Project Settings > Input System Package`
2. Asegúrate de tener configurados:
   - **Horizontal**: A/D o Flechas izq/der
   - **Vertical**: W/S o Flechas arriba/abajo

Si usas el New Input System:
- El proyecto ya tiene `InputSystem_Actions.inputactions`
- Ábrelo y asegúrate de tener un Action Map con `Move` (Vector2)

---

### 5. CREAR TERRENO SIMPLE (Para Testing)

Para que el Ground Check funcione, necesitas un suelo:

1. `GameObject > 3D Object > Plane`
2. Renómbralo: `Ground`
3. Configura:
   - Position: `(0, 0, 0)`
   - Scale: `(10, 1, 10)` (100m x 100m)
   - Layer: `Environment` (Layer 8)

---

### 6. CONFIGURAR CÁMARA (Básica por ahora)

Edita la `Main Camera` en Bootstrap:
- Position: `(0, 5, -8)`
- Rotation: `(30, 0, 0)`

Esto te da una vista isométrica básica. En fases futuras usaremos Cinemachine.

---

## 🧪 TEST: MOVIMIENTO EN RED

### Test Local (1 Cliente)
1. Asegúrate de estar en la escena `Bootstrap`
2. Click `Play`
3. Deberías ver:
   - Jugador spawn en `(0, 0.5, 0)`
   - HUD con barras HP/Mana llenas
   - Movimiento con WASD

### Test Multi-Cliente (2 Jugadores)
1. **Ventana Original (Server+Client):**
   - NetworkBootstrap: Auto Start Server = `TRUE`, Auto Start Client = `TRUE`

2. **Clone de ParrelSync (Solo Client):**
   - Abre el clon: `ParrelSync > Clones Manager > Open Clone`
   - En el clon: NetworkBootstrap: Auto Start Server = `FALSE`, Auto Start Client = `TRUE`

3. Click `Play` en ambas ventanas
4. Deberías ver:
   - En ambas ventanas: 2 cápsulas (jugadores)
   - Movimiento WASD en tu jugador
   - El otro jugador se mueve en red (interpolado)

---

## ✅ CRITERIOS DE ÉXITO - FASE 2

- ✅ Movimiento fluido sin jitter
- ✅ 2 jugadores se ven moverse entre sí
- ✅ HUD muestra HP/Mana correctamente
- ✅ Stats se sincronizan (prueba con Context Menu > Take 20 Damage)

---

## 🐛 TROUBLESHOOTING

**Problema: Jugador no spawna**
- Verifica que `PlayerSpawnManager` tenga el prefab asignado
- Verifica que el prefab tenga `NetworkObject`

**Problema: No hay movimiento**
- Verifica que `Ground Layer` en PlayerController sea `Environment`
- Verifica que el Plane tenga Layer `Environment`

**Problema: HUD no se ve**
- Verifica que el UIDocument tenga asignado `HUD.uxml`
- Abre la consola y busca errores de UI Toolkit

**Problema: Cliente remoto no se ve moverse**
- Verifica en la consola que ambos clientes se conectaron
- Verifica que los SyncVars estén funcionando

---

**Avísame cuando termines el test y continuamos con FASE 3: Targeting System!** 🎯

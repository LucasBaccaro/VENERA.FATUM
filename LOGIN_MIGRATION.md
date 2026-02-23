# Login Scene & Nakama Auth Migration

Documentacion de la migracion del sistema de login desde Bootstrap (inline) a una escena separada con autenticacion email via Nakama.

## Arquitectura General

```
Login Scene (index 0)          Bootstrap Scene (index 1)          Chunk Scenes (2+)
┌─────────────────────┐        ┌──────────────────────────┐       ┌─────────────┐
│ Main Camera          │        │ NetworkManager (FishNet) │       │ Terrain     │
│ Directional Light    │        │ NetworkBootstrap         │       │ NPCs        │
│ LoginSceneUI:        │  ───►  │ AudioManager (Singleton) │       │ Enemies     │
│   UIDocument         │ additive│ InputManager (Singleton)│       │ Props       │
│   LoginSceneController│ load  │ PlayerSpawnManager       │       └─────────────┘
│   AudioSource (local)│        │ EntryPoint               │
└─────────────────────┘        │ HUD, Inventory, etc.     │
                                └──────────────────────────┘
```

## Flujo de Autenticacion

```
1. Login Scene carga (index 0)
2. Usuario ingresa username + password
3. NakamaAuthClient.LoginAsync() → email auth contra Nakama
4. Si tiene personaje → directo a Loading
   Si no tiene personaje → pantalla de Character Creation
5. LoginSceneController carga Bootstrap scene (additive)
6. SetActiveScene(Bootstrap) para que objetos runtime pertenezcan a Bootstrap
7. Desactiva Camera + AudioListener de Login
8. NetworkBootstrap pre-warmed server, LoginSceneController conecta el client
9. Espera spawn del player (LostArkCamera.target != null)
10. Espera 2s para que chunks carguen
11. Blur focus de TextFields + re-enable Player input action map
12. Descarga Login scene
```

## Archivos Creados

### `Assets/_Project/0_Core/Persistence/NakamaAuthClient.cs`
Clase estatica para auth client-side (sin MonoBehaviour). Crea un Nakama client liviano para login/register antes de que Bootstrap exista.

- `LoginAsync(username, password)` → AuthResult (tokens + character data)
- `RegisterAsync(username, password)` → AuthResult (tokens, sin character)
- `LoadCharacterAsync(session)` → CharacterData desde Nakama storage
- Usa formato email `username@venera.fatum`

### `Assets/_Project/3_Presentation/UI/Controllers/LoginSceneController.cs`
Controller principal de la Login scene. State machine con 4 estados:

- **Login**: username + password, boton login
- **Register**: username + password + confirm, boton register
- **CharacterCreation**: seleccion de clase, stats, abilities, nombre, faccion (Citizen/PK)
- **Loading**: barra de progreso, tips, carga Bootstrap additively

Audio manejado con AudioSource local (NO usa AudioManager singleton - no existe en Login scene).

### `Assets/_Project/3_Presentation/UI/Views/LoginSceneUI.uxml`
Layout completo con 4 paneles (Login, Register, CharCreation, Loading) + AbilityTooltip flotante.

### `Assets/_Project/3_Presentation/UI/Styles/LoginSceneStyle.uss`
Estilos dark fantasy con paleta gold `rgb(220, 180, 100)`.

### `Assets/_Project/1_Data/Editor/UpdateBuildSettings.cs`
Editor utility: `Genesis/Tools/Update Build Settings (Login First)` → setea Login=0, Bootstrap=1.

### `Assets/_Project/5_Content/Scenes/Login.unity`
Escena con: Main Camera (MainCamera tag, AudioListener), Directional Light, LoginSceneUI (UIDocument + LoginSceneController + AudioSource).

## Archivos Modificados

### `Assets/_Project/0_Core/Networking/LoginData.cs`
Expandido con campos de auth:
- `Username`, `AuthToken`, `RefreshToken`, `UserId`, `IsNewCharacter`
- `LoginRequired` (flag para que NetworkBootstrap sepa esperar)
- `Clear()` limpia todos los campos

### `Assets/_Project/0_Core/Persistence/NakamaManager.cs`
Agregado `RestoreSession(clientId, authToken, refreshToken)`:
- Usa `Session.Restore()` para recrear sesion server-side sin password
- El servidor nunca ve el password del usuario

### `Assets/_Project/2_Simulation/Entities/Player/PlayerClassManager.cs`
- `CmdSetLoginData` ahora recibe `authToken`, `refreshToken`, `isNewCharacter`
- `LoadOrCreateCharacterAsync` soporta token-based auth con fallback a device auth (legacy/testing)
- Si tiene tokens → `RestoreSession()`, sino → `AuthenticateDeviceAsync()`

### `Assets/_Project/0_Core/Networking/NetworkBootstrap.cs`
- Si `LoginData.LoginRequired == true`: pre-warm server en editor y return (no auto-connect client)
- LoginSceneController llama `StartClientLocal()` despues del auth

### `Assets/_Project/3_Presentation/UI/Controllers/MainMenuController.cs`
- `OnLogout()`: carga escena "Login" en vez de "Bootstrap"
- `LoginData.Clear()` en vez de `LoginData.IsSet = false`
- Chequeo ESC usa `LoginSceneController.IsActive`

## Build Settings

| Index | Scene |
|-------|-------|
| 0 | Login |
| 1 | Bootstrap |
| 2+ | Chunk_X_Y |

## Problemas Resueltos Durante la Migracion

### AudioManager singleton en Login scene
**Problema**: Poner AudioManager en Login causaba errores de singleton duplicado al cargar Bootstrap.
**Solucion**: Login usa un AudioSource local para musica/SFX. AudioManager solo vive en Bootstrap.

### Camera de Login tapaba Bootstrap
**Problema**: Al cargar Bootstrap additive, la Camera de Login seguia renderizando negro encima.
**Solucion**: Desactivar Camera y AudioListener de Login inmediatamente despues de cargar Bootstrap.

### Active Scene incorrecta
**Problema**: Objetos runtime se creaban en Login scene, al descargar Login se destruian.
**Solucion**: `SceneManager.SetActiveScene(bootstrapScene)` despues de cargar Bootstrap.

### WASD no funcionaba post-login
**Problema**: Los TextFields del Login capturaban el foco del teclado para el Input System. Al descargar Login, el action map "Player" (WASD) quedaba deshabilitado.
**Solucion**: Dos fixes complementarios:
1. `_root.focusController.focusedElement?.Blur()` - suelta foco de TextFields
2. `InputManager.Instance.SetPlayerControlsEnabled(true)` - fuerza re-habilitacion del action map "Player"

### Pantalla oscura al transicionar
**Problema**: Login scene se descargaba antes de que chunks cargaran, resultando en oscuridad.
**Solucion**: Esperar 2 segundos despues del spawn antes de descargar Login.

## Archivos Deprecados

- `Assets/_Project/3_Presentation/UI/Controllers/LoginController.cs` - el viejo login inline en Bootstrap. Ya no tiene GO en ninguna escena pero el archivo sigue existiendo. `LoginController.IsActive` queda en `false` por defecto.
- `Assets/_Project/3_Presentation/UI/Views/LoginUI.uxml` - viejo layout del login.
- `Assets/_Project/3_Presentation/UI/Styles/LoginStyle.uss` - viejos estilos.

## Diagrama de Auth Flow

```
        CLIENT (Login Scene)                    SERVER (Bootstrap)
        ════════════════════                    ══════════════════

   ┌─ NakamaAuthClient ─┐
   │  LoginAsync()       │──── email auth ────► Nakama
   │  tokens ◄───────────│◄── session ─────────┘
   └─────────────────────┘
            │
            │ tokens almacenados en LoginData (static)
            │
   ┌─ LoginSceneController ─┐
   │  LoadBootstrap()        │──── additive load ────► Bootstrap Scene
   │  StartClientLocal()     │──── FishNet connect ──► NetworkManager
   └─────────────────────────┘
            │
            │ FishNet spawns player
            │
   ┌─ PlayerClassManager ─────────────────────────────────────────┐
   │  OnStartClient()                                              │
   │  CmdSetLoginData(name, class, faction, authToken, refreshToken)│
   │                                          │                    │
   │                               [ServerRpc]│                    │
   │                                          ▼                    │
   │                              NakamaManager.RestoreSession()   │
   │                              Session.Restore(token, refresh)  │
   │                              LoadAsync(userId) → character    │
   │                              Hydrate player stats/position    │
   └───────────────────────────────────────────────────────────────┘
```

## Notas Importantes

- **El server nunca ve el password**: solo recibe authToken + refreshToken via ServerRpc
- **Device auth sigue funcionando**: si no hay tokens (testing desde Bootstrap directo), fallback a `AuthenticateDeviceAsync`
- **LoginData es static**: sobrevive scene loads sin necesidad de DontDestroyOnLoad
- **Login scene NO tiene**: AudioManager, UIButtonSoundManager, NetworkManager, ni ningun singleton de Bootstrap
- **sortingOrder 100**: el UIDocument del Login usa sortingOrder alto para estar encima de todo mientras esta activo

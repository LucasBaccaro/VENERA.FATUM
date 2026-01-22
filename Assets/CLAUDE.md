# PROYECTO GENESIS - TECHNICAL CONTEXT

## 1. CORE PRINCIPLES (DOGMAS)
- **Server Authority:** El servidor (FishNet) es la única fuente de verdad. Validación paranoica.
- **Bootstrapping:** Usar `Bootstrap` scene persistente. NO usar `DontDestroyOnLoad`.
- **Assembly Definitions:** Estricta separación: Core -> Data -> Simulation -> Presentation.
- **Data-Driven:** Balanceo mediante ScriptableObjects + JSON.
- **Performance:** Object Pooling OBLIGATORIO. Physics: SphereCast > Rigidbody.
- **Optimización:** Queries Hit Triggers: OFF. Collision Matrix estricta.

## 2. TECH STACK
- **Engine:** Unity 6.3 LTS.
- **Net:** FishNet (Tugboat UDP). Tick Rate: 50Hz.
- **Backend:** Nakama (Self-Hosted via Docker) para Auth, Storage, Chat.
- **Architecture:** ServiceLocator, EventBus, Strategy Pattern (Abilities).

## 3. PROJECT STRUCTURE
`Assets/_Project/`
- `0_Core/`: Architecture (EventBus, ServiceLocator), Net Managers, Utils.
- `1_Data/`: ScriptableObjects (Abilities, Items), JSON.
- `2_Simulation/`: Game Logic (PlayerController, Combat, Stats, NPC).
- `3_Presentation/`: UI (Toolkit), VFX, Audio.
- `4_Bootstrap/`: Entry point & Global Managers.
- `5_Content/`: Prefabs, Models, Textures.

## 4. DOCUMENTATION REFERENCES
Para implementar sistemas específicos, **LEE** el archivo correspondiente en `Assets/Docs`:
- **Configuración, Escenas y Performance:** `Docs/01_Architecture_Stack.md`
- **Combate, Skills, Proyectiles y Targeting:** `Docs/02_Combat_System.md`
- **FishNet, Server Tick y Nakama:** `Docs/03_Net_Persistence.md`
- **Plan de Trabajo y Tareas:** `Docs/04_Roadmap.md`

## 5. CODING GUIDELINES
- Usa `[Server]`, `[ServerRpc]`, `[ObserversRpc]` explícitamente.
- Evita referencias directas entre sistemas dispares; usa `EventBus`.
- KCC (Kinematic Character Controller) custom basado en `transform` + `Physics.SphereCast`. No usar `CharacterController` ni `Rigidbody` para movimiento de players.
- Logica de juego separada de la UI (MVC/MVP).

## 6. CURRENT SCOPE
- **Vertical Slice:** 50 jugadores concurrentes.
- **Modo:** Full Loot Open World.
- **Prioridad:** Estabilidad de red, predicción de cliente y combate responsivo.

⚠️ **REGLAS DE ORO:**
*   **Server Authority:** El servidor SIEMPRE valida.
*   **No DontDestroyOnLoad:** Usa Bootstrap scene.
*   **Object Pooling:** Para TODA entidad que se spawne frecuentemente.
*   **SyncVars con moderación:** Solo para datos críticos.
*   **EventBus > Referencias directas:** Desacopla sistemas.

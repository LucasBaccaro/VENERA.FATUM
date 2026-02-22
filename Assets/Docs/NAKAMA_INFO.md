⏺ Nakama en tu Proyecto - Resumen Completo                                                                                                                                                                                                                  
                                                                                                                                                                                                                                                            
  ---                                                                                                                                                                                                                                                       
  1. Las 2 instancias de Docker                                                                                                                                                                                                                             
                                                                                                                                                                                                                                                            
  Tu docker-compose.yml levanta 2 servicios:                                                                                                                                                                                                                
                                                                                                                                                                                                                                                            
  ┌─────────────┬───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┬────────────────────────────────────────────────────────────┐                                          
  │  Servicio   │                                                              Que es                                                               │                          Puertos                           │                                          
  ├─────────────┼───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┼────────────────────────────────────────────────────────────┤                                          
  │ CockroachDB │ Base de datos distribuida (compatible con PostgreSQL wire protocol). Nakama la usa internamente, vos nunca la tocas directamente. │ :26257 (SQL), :8080 (admin UI)                             │
  ├─────────────┼───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┼────────────────────────────────────────────────────────────┤                                          
  │ Nakama      │ El game server/backend. Expone APIs REST, gRPC, WebSocket y una consola admin.                                                    │ :7350 (API HTTP/REST), :7349 (gRPC), :7351 (Consola Admin) │
  └─────────────┴───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┴────────────────────────────────────────────────────────────┘                                          

  Al arrancar, Nakama ejecuta migrate up (crea/actualiza tablas en CockroachDB) y luego inicia el servidor.

  ---
  2. Como se conecta Unity con Nakama

  La conexion es directa desde C# via el SDK nakama-unity (HTTP REST + WebSocket):

  Unity (NakamaManager.cs)
      │
      ├── new Client("http", "127.0.0.1", 7350, "defaultkey")  ← REST/gRPC
      │       ├── AuthenticateDeviceAsync()     → autenticacion
      │       ├── ReadStorageObjectsAsync()     → leer datos
      │       └── WriteStorageObjectsAsync()    → escribir datos
      │
      └── (Opcional) Socket.From(client)  ← WebSocket (real-time)
              ├── Chat en tiempo real
              ├── Matchmaking
              ├── Presences (quién está online)
              └── Notificaciones

  Actualmente solo usas el Client REST. No usas WebSocket todavia.

  El flujo actual de datos es:

  Unity (NakamaManager) ──HTTP/JSON──► Nakama Server ──SQL──► CockroachDB
                                            │
                                      Serializa/deserializa
                                      CharacterData como JSON
                                      en la tabla "storage"

  Si, todo es JSON. CharacterData se serializa con JsonUtility.ToJson() y se guarda en la Nakama Storage API como un objeto JSON dentro de una collection/key:

  - Collection: "characters"
  - Key: "main"
  - UserID: un UUID de Nakama (1 por nombre de personaje)
  - Value: el JSON completo del CharacterData

  ---
  3. Que te da Nakama como framework (built-in)

  Esto es lo que ya existe listo para usar sin escribir codigo server-side:

  3.1 Autenticacion (7+ metodos)

  ┌──────────────────┬──────────────────────────────────────────────────────────────────────┐
  │      Metodo      │                            Como funciona                             │
  ├──────────────────┼──────────────────────────────────────────────────────────────────────┤
  │ Device           │ ID unico del dispositivo (lo que usas ahora con genesis_char_{name}) │
  ├──────────────────┼──────────────────────────────────────────────────────────────────────┤
  │ Email + Password │ Clasico email/password con hash bcrypt                               │
  ├──────────────────┼──────────────────────────────────────────────────────────────────────┤
  │ Facebook         │ OAuth token → auto-importa amigos                                    │
  ├──────────────────┼──────────────────────────────────────────────────────────────────────┤
  │ Google           │ OAuth token                                                          │
  ├──────────────────┼──────────────────────────────────────────────────────────────────────┤
  │ Apple            │ Sign in with Apple                                                   │
  ├──────────────────┼──────────────────────────────────────────────────────────────────────┤
  │ Steam            │ Steam session ticket → auto-importa amigos                           │
  ├──────────────────┼──────────────────────────────────────────────────────────────────────┤
  │ Custom           │ Cualquier ID externo (tu propio auth server)                         │
  └──────────────────┴──────────────────────────────────────────────────────────────────────┘

  Se pueden linkear multiples metodos a una misma cuenta. Ej: empezas con Device, despues linkeas Email para recovery.

  3.2 Storage Engine (lo que ya usas)

  Collection (string)
    └── Key (string)
          ├── Value: JSON arbitrario
          ├── Owner: user_id
          ├── Version: para concurrencia optimista (compare-and-swap)
          └── Permissions:
                Read:  0=solo server, 1=owner, 2=publico
                Write: 0=solo server, 1=owner

  - Batch operations: podes leer/escribir multiples objetos en una sola llamada
  - Version control: cada objeto tiene un version hash. Si pasas el version esperado al escribir, falla si alguien mas ya lo modifico (evita race conditions)
  - Indexing: podes crear indices sobre campos del JSON para busquedas rapidas via runtime code
  - Server bypass: el runtime code del server ignora permisos (puede leer/escribir todo)

  3.3 Sistema de Amigos

  ┌────────────────────┬──────────────────┐
  │       Estado       │   Significado    │
  ├────────────────────┼──────────────────┤
  │ Friend             │ Amistad mutua    │
  ├────────────────────┼──────────────────┤
  │ Invite (outgoing)  │ Request enviado  │
  ├────────────────────┼──────────────────┤
  │ Invited (incoming) │ Request recibido │
  ├────────────────────┼──────────────────┤
  │ Blocked            │ Bloqueado        │
  └────────────────────┴──────────────────┘

  Facebook/Steam pueden auto-importar amigos al autenticar.

  3.4 Grupos / Guilds / Clans

  Sistema completo de guilds con jerarquia:

  ┌────────────┬──────────────────────────┬───────────────────┬────────────────────┐
  │    Rol     │      Puede kickear       │  Puede promover   │ Puede borrar guild │
  ├────────────┼──────────────────────────┼───────────────────┼────────────────────┤
  │ Superadmin │ Si (todos)               │ Si (a superadmin) │ Si                 │
  ├────────────┼──────────────────────────┼───────────────────┼────────────────────┤
  │ Admin      │ Si (excepto superadmins) │ Si (a admin)      │ No                 │
  ├────────────┼──────────────────────────┼───────────────────┼────────────────────┤
  │ Member     │ No                       │ No                │ No                 │
  └────────────┴──────────────────────────┴───────────────────┴────────────────────┘

  - Guilds publicas (anyone joins) o privadas (invite-only)
  - Metadata JSON (hasta 16KB) para stats/info de guild
  - Chat grupal integrado automaticamente para cada guild
  - Sistema de ban (kick + prevent rejoin)

  3.5 Chat en Tiempo Real

  3 tipos de canales:

  ┌───────┬─────────────────────────┬───────────────────────────┐
  │ Tipo  │       Visibilidad       │       Uso para MMO        │
  ├───────┼─────────────────────────┼───────────────────────────┤
  │ Room  │ Publico                 │ Chat global, chat de zona │
  ├───────┼─────────────────────────┼───────────────────────────┤
  │ Group │ Solo miembros del grupo │ Chat de guild             │
  ├───────┼─────────────────────────┼───────────────────────────┤
  │ DM    │ Privado entre 2         │ Whisper/privado           │
  └───────┴─────────────────────────┴───────────────────────────┘

  - Persistente: mensajes guardados en BD, historial disponible
  - Multi-canal simultaneo (estar en world chat + guild chat + DM a la vez)
  - Eventos de presencia (quien entra/sale del canal)

  3.6 Leaderboards

  - Sort: DESC (mayor gana) o ASC (menor gana)
  - Operadores: set (ultimo score), best (mejor score), incr (acumulativo)
  - Reset: via CRON (diario, semanal, mensual)
  - Metadata: JSON arbitrario por record
  - Authoritative mode: solo el server puede submitear scores (anti-cheat)
  - Vistas: global, around me, friends only

  3.7 Torneos

  Leaderboards con scheduling:
  - Duracion, reset periodico via CRON, fecha de fin
  - Max participantes, max intentos por periodo
  - Rewards via hooks server-side al resetear

  3.8 Matchmaking

  - Criteria-based: propiedades numericas, string, boolean
  - Query filters: "+level:>=10 +class:warrior"
  - Party support: un party entero matchea junto con capacidad reservada
  - Min/Max count: rango aceptable de jugadores por match
  - Timeout configurable

  3.9 Multiplayer Real-Time (Matches)

  Dos modelos:

  Relayed (client-authoritative): Nakama solo reenvía mensajes entre clientes sin validar.

  Server-Authoritative: game loop en el servidor con callbacks:
  - MatchInit, MatchJoinAttempt, MatchJoin, MatchLeave
  - MatchLoop (tick function, configurable tick rate)
  - MatchTerminate, MatchSignal

  (Vos ya usas FishNet para esto, no necesitas los matches de Nakama para gameplay)

  3.10 Wallet / Economia

  - Cada user tiene un wallet (JSONB) con balances de monedas
  - Todas las operaciones son server-side (credit, debit, update)
  - Ledger de auditoria: wallet_ledger registra cada transaccion
  - IAP Validation: Apple App Store, Google Play, Huawei AppGallery

  3.11 Notificaciones In-App

  - Persistentes: guardadas en BD, entregadas al reconectar
  - No-persistentes: solo para usuarios online

  3.12 Status / Presences

  - Users pueden setear un status message ("Jugando en Dungeon X")
  - Seguir a otros users para recibir cambios de status
  - Ideal para "online/offline" y "currently playing"

  3.13 Server Runtime Code (Hooks)

  Nakama permite escribir logica server-side en 3 lenguajes:

  ┌────────────┬─────────────────┬─────────────────────────────┐
  │  Lenguaje  │   Performance   │            Notas            │
  ├────────────┼─────────────────┼─────────────────────────────┤
  │ Go         │ Maxima (nativo) │ Plugin compilado (.so)      │
  ├────────────┼─────────────────┼─────────────────────────────┤
  │ TypeScript │ Buena (V8)      │ Recomendado por Heroic Labs │
  ├────────────┼─────────────────┼─────────────────────────────┤
  │ Lua        │ Adecuada        │ Mas simple, stdlib limitada │
  └────────────┴─────────────────┴─────────────────────────────┘

  Tipos de hooks:
  - Before Hooks: interceptar requests antes de procesarlos (validar, modificar, rechazar)
  - After Hooks: ejecutar logica despues de un evento
  - RPC Functions: endpoints custom que el cliente llama por ID
  - Match Handlers: logica de match server-authoritative

  ---
  4. Tablas que ya existen en CockroachDB

  Nakama crea estas tablas automaticamente con migrate up:

  ┌────────────────────┬─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
  │       Tabla        │                                                                Contenido                                                                │
  ├────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ users              │ Cuentas: UUID, username, display_name, email, password (bcrypt), social IDs, wallet (JSONB), metadata (JSONB), lang, location, timezone │
  ├────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ user_device        │ Links device ID → user account                                                                                                          │
  ├────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ user_edge          │ Grafo de amigos: source → destination + state                                                                                           │
  ├────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ user_tombstone     │ IDs de cuentas borradas (previene re-registro)                                                                                          │
  ├────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ storage            │ Tu data vive aca: collection + key + user_id → value (JSONB), version, permisos                                                         │
  ├────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ notification       │ Notificaciones in-app: user_id, subject, content (JSONB)                                                                                │
  ├────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ message            │ Mensajes de chat: stream info, sender, content (JSONB)                                                                                  │
  ├────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ leaderboard        │ Definiciones: id, sort_order, operator, reset_schedule, metadata                                                                        │
  ├────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ leaderboard_record │ Scores: leaderboard_id, owner_id, score, subscore, metadata                                                                             │
  ├────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ groups             │ Guilds: id, creator, name, description, metadata (JSONB), max_count                                                                     │
  ├────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ group_edge         │ Membresías: user_id → group_id + rol (superadmin/admin/member/banned)                                                                   │
  ├────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ wallet_ledger      │ Historial de transacciones de wallet: changeset (JSONB), metadata                                                                       │
  ├────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ purchase           │ Validacion de IAPs                                                                                                                      │
  ├────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ subscription       │ Tracking de suscripciones                                                                                                               │
  └────────────────────┴─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘

  ---
  5. Escalabilidad

  ┌────────────────────┬─────────────────────────────────────────────────────┐
  │       Escala       │                      Capacidad                      │
  ├────────────────────┼─────────────────────────────────────────────────────┤
  │ 1 nodo, 1 CPU      │ ~20,000 CCU                                         │
  ├────────────────────┼─────────────────────────────────────────────────────┤
  │ 1 nodo (practico)  │ ~10,000 CCU                                         │
  ├────────────────────┼─────────────────────────────────────────────────────┤
  │ 2 nodos, 2 CPU c/u │ ~35,700 CCU                                         │
  ├────────────────────┼─────────────────────────────────────────────────────┤
  │ Load test probado  │ 2,000,000 CCU (AWS/GCP, 22,300 req/s, p95 < 26.7ms) │
  └────────────────────┴─────────────────────────────────────────────────────┘

  - Horizontal scaling: agregar nodos. No hay primary/replica, todos los nodos son iguales
  - Auto-discovery: nodos se encuentran via gossip protocol + CRDTs
  - Self-healing: si un nodo cae, el cluster redistribuye automaticamente
  - CockroachDB escala horizontalmente por separado (agregar mas nodos de DB)

  ---
  6. Que mas deberias almacenar para un MMO

  Cosas que Nakama ya te da y no estas usando todavia:

  ┌───────────────────────┬──────────────────────────────────────────────────────────────────────────────────────────────────┬───────────────┐
  │        Feature        │                                           Como usarla                                            │   Prioridad   │
  ├───────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────┼───────────────┤
  │ Guilds                │ Groups API. Guild chat gratis. Metadata para guild bank, nivel, etc.                             │ Alta          │
  ├───────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────┼───────────────┤
  │ Friends               │ Friends API. Online status via presences.                                                        │ Alta          │
  ├───────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────┼───────────────┤
  │ Chat global/zona      │ Chat Rooms via WebSocket. Persistente, con historial.                                            │ Alta          │
  ├───────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────┼───────────────┤
  │ Whisper/DM            │ Direct Messages via WebSocket.                                                                   │ Media         │
  ├───────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────┼───────────────┤
  │ Leaderboards          │ PvP ranking, DPS, dungeon clear times, level ranking                                             │ Media         │
  ├───────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────┼───────────────┤
  │ Wallet                │ Reemplazar tu gold field en CharacterData → wallet nativo de Nakama. Ledger de auditoria gratis. │ Media         │
  ├───────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────┼───────────────┤
  │ Matchmaking           │ Cola de dungeon, matcheo por nivel/clase/party                                                   │ Media         │
  ├───────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────┼───────────────┤
  │ Notifications         │ Guild invites, trade requests, system announcements                                              │ Media         │
  ├───────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────┼───────────────┤
  │ Trading entre players │ RPC custom server-side que atomicamente transfiere items entre storage objects                   │ Alta          │
  ├───────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────┼───────────────┤
  │ Auction House         │ Storage collection para listings + RPCs para buy/sell                                            │ Baja (futuro) │
  ├───────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────┼───────────────┤
  │ Mail system           │ Notifications persistentes + storage para attachments                                            │ Baja          │
  └───────────────────────┴──────────────────────────────────────────────────────────────────────────────────────────────────┴───────────────┘

  Datos que podrias mover a collections separadas en Storage

  En vez de tener todo en un solo CharacterData JSON, podes separar:

  Collection: "characters"     Key: "main"        → stats, level, position
  Collection: "characters"     Key: "equipment"   → 11 slots
  Collection: "characters"     Key: "inventory"   → 25+ slots
  Collection: "characters"     Key: "quests"      → quest log
  Collection: "characters"     Key: "skills"      → skill builds / talent trees
  Collection: "characters"     Key: "achievements" → logros
  Collection: "global"         Key: "settings"    → settings del jugador (audio, keybinds)

  Ventaja: podes leer/escribir parcialmente sin tocar todo el JSON cada vez.

  ---
  7. Tu arquitectura actual vs lo que podes hacer

  AHORA:
  ┌──────────┐   FishNet    ┌──────────────┐   HTTP/JSON   ┌────────┐   SQL   ┌─────────────┐
  │  Unity   │◄────────────►│  FishNet     │──────────────►│ Nakama │◄──────►│ CockroachDB │
  │  Client  │  (gameplay)  │  Server      │  (persist)    │ Server │        │     (DB)    │
  └──────────┘              │  (host/dedi) │               └────────┘        └─────────────┘
                            └──────────────┘
                            Solo usa: Auth + Storage (REST)

  POTENCIAL:
  ┌──────────┐   FishNet    ┌──────────────┐   HTTP + WS    ┌────────┐   SQL   ┌─────────────┐
  │  Unity   │◄────────────►│  FishNet     │───────────────►│ Nakama │◄──────►│ CockroachDB │
  │  Client  │  (gameplay)  │  Server      │                │ Server │        │     (DB)    │
  │          │              │              │  Auth           │        │        │             │
  │          │◄─────WebSocket──────────────│  Storage        │  + TS  │        │             │
  │          │  (chat, friends,            │  Guilds         │ Runtime│        │             │
  │          │   matchmaking,              │  Friends        │ (hooks)│        │             │
  │          │   presences)                │  Chat           └────────┘        └─────────────┘
  └──────────┘                             │  Leaderboards
                                           │  Matchmaking
                                           │  Wallet
                                           │  Notifications
                                           │  Before/After Hooks (validacion)
                                           └──────────────┘

  Resumen: Nakama es mucho mas que una base de datos JSON. Es un game backend completo con social, economy, matchmaking, y logica server-side. Actualmente solo usas ~10% de lo que ofrece (auth + storage). Todo lo social (guilds, friends, chat,
  leaderboards) ya esta listo para usar sin reinventar.

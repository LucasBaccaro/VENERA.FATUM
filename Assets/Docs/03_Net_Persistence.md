## 8. NETWORKING LAYER

### 8.1 Configuración de FishNet

**NetworkManager Settings (Inspector):**
```text
[Server Manager]
├── Max Connections: 50
├── Timeout: 60s
├── Allow Headless: TRUE (para Linux server)
└── Start On Headless: TRUE

[Client Manager]
├── Enable Logging: Development Only
└── Reconnect Attempts: 3

[Time Manager]
├── Tick Rate: 50 (20ms tick)
├── Physics Mode: Unity Physics
└── Max Buffered Ticks: 3

[Transport - Tugboat]
├── Port: 7770
├── Max MTU: 1200
├── Unreliable MTU: 1024
└── IPv6: FALSE
```

### 8.2 Server Tick Architecture

**ServerManager.cs - Core Loop**
```csharp
// Assets/_Project/0_Core/Networking/ServerManager.cs

using FishNet;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;
using System.Collections.Generic;

namespace Genesis.Core.Networking {

public class ServerManager : NetworkBehaviour {
    
    public static ServerManager Instance { get; private set; }
    
    [Header("Spawn Settings")]
    [SerializeField] private Transform[] playerSpawnPoints;
    [SerializeField] private GameObject[] playerPrefabs; // [0]=Guerrero, [1]=Mago, etc
    
    [Header("Server Tick")]
    [SerializeField] private int targetTickRate = 50; // 50Hz = 20ms
    private float _tickInterval;
    private float _nextTickTime;
    
    // Registry
    private Dictionary<int, NetworkObject> _activePlayers = new Dictionary<int, NetworkObject>();
    
    void Awake() {
        if (Instance != null) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        _tickInterval = 1f / targetTickRate;
    }
    
    public override void OnStartServer() {
        base.OnStartServer();
        
        InstanceFinder.ServerManager.OnRemoteConnectionState += OnPlayerConnectionState;
        
        Debug.Log($"[SERVER] Iniciado - Tick Rate: {targetTickRate}Hz");
    }
    
    // ═══════════════════════════════════════════════════════
    // CONNECTION HANDLING
    // ═══════════════════════════════════════════════════════
    
    private void OnPlayerConnectionState(FishNet.Connection.NetworkConnection conn, FishNet.Managing.Server.RemoteConnectionStateArgs args) {
        
        if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Started) {
            // Jugador conectado
            OnPlayerJoined(conn);
        } 
        else if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped) {
            // Jugador desconectado
            OnPlayerLeft(conn);
        }
    }
    
    [Server]
    private void OnPlayerJoined(FishNet.Connection.NetworkConnection conn) {
        Debug.Log($"[SERVER] Jugador {conn.ClientId} conectado");
        
        // Buscar datos del jugador en Nakama
        CharacterData data = NakamaManager.Instance.LoadCharacterData(conn.ClientId.ToString());
        
        // Spawn point
        Transform spawnPoint = GetFreeSpawnPoint();
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        
        // Seleccionar prefab según clase
        GameObject prefab = playerPrefabs[data.classIndex];
        
        // Spawnear jugador
        GameObject playerObj = Instantiate(prefab, spawnPos, Quaternion.identity);
        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
        
        // Asignar ownership
        InstanceFinder.ServerManager.Spawn(playerObj, conn);
        
        // Hidratar stats
        PlayerStats stats = playerObj.GetComponent<PlayerStats>();
        stats.Initialize(data);
        
        // Registrar
        _activePlayers[conn.ClientId] = netObj;
    }
    
    [Server]
    private void OnPlayerLeft(FishNet.Connection.NetworkConnection conn) {
        Debug.Log($"[SERVER] Jugador {conn.ClientId} desconectado");
        
        if (_activePlayers.TryGetValue(conn.ClientId, out NetworkObject netObj)) {
            
            // GUARDAR DATOS antes de despawn (CRÍTICO para Full Loot)
            CharacterData data = ExtractCharacterData(netObj);
            NakamaManager.Instance.SaveCharacterData(conn.ClientId.ToString(), data);
            
            // Despawn
            InstanceFinder.ServerManager.Despawn(netObj.gameObject);
            _activePlayers.Remove(conn.ClientId);
        }
    }
    
    // ═══════════════════════════════════════════════════════
    // SERVER TICK (50Hz)
    // ═══════════════════════════════════════════════════════
    
    [Server]
    void Update() {
        if (Time.time < _nextTickTime) return;
        
        ServerTick();
        _nextTickTime = Time.time + _tickInterval;
    }
    
    [Server]
    private void ServerTick() {
        // Aquí procesamos lógica que NO es física (FixedUpdate)
        // Ej: Cooldowns, timers de habilidades, IA de NPCs
        
        // TODO: NPC AI tick
        // TODO: World events tick
    }
    
    // ═══════════════════════════════════════════════════════
    // UTILITIES
    // ═══════════════════════════════════════════════════════
    
    private Transform GetFreeSpawnPoint() {
        // Buscar spawn point más alejado de otros jugadores
        Transform best = playerSpawnPoints[0];
        float maxDistance = 0f;
        
        foreach (Transform spawn in playerSpawnPoints) {
            float minDistToPlayer = float.MaxValue;
            
            foreach (var player in _activePlayers.Values) {
                float dist = Vector3.Distance(spawn.position, player.transform.position);
                minDistToPlayer = Mathf.Min(minDistToPlayer, dist);
            }
            
            if (minDistToPlayer > maxDistance) {
                maxDistance = minDistToPlayer;
                best = spawn;
            }
        }
        
        return best;
    }
    
    private CharacterData ExtractCharacterData(NetworkObject netObj) {
        PlayerStats stats = netObj.GetComponent<PlayerStats>();
        PlayerInventory inventory = netObj.GetComponent<PlayerInventory>();
        
        return new CharacterData {
            position = netObj.transform.position,
            rotation = netObj.transform.rotation.eulerAngles,
            health = stats.CurrentHealth,
            mana = stats.CurrentMana,
            items = inventory.GetItemsAsJSON()
            // etc...
        };
    }
}

} // namespace
```

### 8.3 Lag Compensation Strategy

**Caso de Uso: Validar Rango de Habilidad**
```csharp
public static class LagCompensation {
    
    /// <summary>
    /// Calcula el rango máximo permitido considerando latencia del cliente
    /// </summary>
    public static float GetCompensatedRange(float baseRange, NetworkConnection conn) {
        
        // Obtener RTT (Round Trip Time) del cliente
        float rtt = conn.GetRoundTripTime();
        
        // Latencia one-way (aproximada)
        float latency = rtt / 2f;
        
        // Velocidad máxima de movimiento del jugador (asumimos 6 m/s)
        float maxMoveSpeed = 6f;
        
        // Distancia que pudo recorrer durante la latencia
        float lagDistance = maxMoveSpeed * latency;
        
        // Tolerancia: rango base + distancia de lag + 10% extra
        return baseRange + lagDistance + (baseRange * 0.1f);
    }
}

// USO EN VALIDACIÓN:
float maxAllowed = LagCompensation.GetCompensatedRange(ability.MaxRange, conn);
if (distance > maxAllowed) {
    RpcAbilityFailed("Fuera de rango");
    return;
}
```

---

## 9. PERSISTENCIA (NAKAMA)

### 9.1 Arquitectura de Datos

**Storage Collections en Nakama:**
```text
nakama_db/
├── users/                    (Auth - Built-in)
│   └── {user_id}
│
├── characters/               (Character Data)
│   └── {user_id}/
│       └── main/             (Un personaje por usuario por ahora)
│           ├── stats         (HP, Mana, Class, Level)
│           ├── position      (x, y, z, rotation)
│           ├── inventory     (Array de items)
│           └── abilities     (Equipped abilities)
│
└── world_state/              (Shared State)
    ├── npc_spawns/
    └── loot_bags/
```

### 9.2 NakamaManager.cs

```csharp
// Assets/_Project/0_Core/Persistence/NakamaManager.cs

using UnityEngine;
using Nakama;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Genesis.Core.Persistence {

public class NakamaManager : MonoBehaviour {
    
    public static NakamaManager Instance { get; private set; }
    
    [Header("Nakama Config")]
    [SerializeField] private string serverHost = "localhost";
    [SerializeField] private int serverPort = 7350;
    [SerializeField] private string serverKey = "defaultkey";
    
    private IClient _client;
    private ISession _session;
    private ISocket _socket;
    
    private const string STORAGE_COLLECTION = "characters";
    
    // ═══════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════
    
    void Awake() {
        if (Instance != null) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        _client = new Client("http", serverHost, serverPort, serverKey);
    }
    
    // ═══════════════════════════════════════════════════════
    // AUTHENTICATION
    // ═══════════════════════════════════════════════════════
    
    public async Task<bool> LoginWithDevice() {
        try {
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            _session = await _client.AuthenticateDeviceAsync(deviceId);
            
            Debug.Log($"[NAKAMA] Logged in - UserID: {_session.UserId}");
            
            // Conectar socket para real-time
            _socket = _client.NewSocket();
            await _socket.ConnectAsync(_session);
            
            return true;
        } catch (System.Exception e) {
            Debug.LogError($"[NAKAMA] Login failed: {e.Message}");
            return false;
        }
    }
    
    // ═══════════════════════════════════════════════════════
    // CHARACTER DATA
    // ═══════════════════════════════════════════════════════
    
    public async Task<CharacterData> LoadCharacterDataAsync(string userId) {
        try {
            var result = await _client.ReadStorageObjectsAsync(_session, new StorageObjectId {
                Collection = STORAGE_COLLECTION,
                Key = "main",
                UserId = userId
            });
            
            if (result.Objects.Count() > 0) {
                string json = result.Objects.First().Value;
                return JsonUtility.FromJson<CharacterData>(json);
            } else {
                // Nuevo jugador - crear datos default
                return CreateDefaultCharacter();
            }
            
        } catch (System.Exception e) {
            Debug.LogError($"[NAKAMA] Load failed: {e.Message}");
            return CreateDefaultCharacter();
        }
    }
    
    public CharacterData LoadCharacterData(string userId) {
        // Versión síncrona (wrapper para usar desde ServerManager)
        return LoadCharacterDataAsync(userId).GetAwaiter().GetResult();
    }
    
    public async Task SaveCharacterDataAsync(string userId, CharacterData data) {
        try {
            string json = JsonUtility.ToJson(data);
            
            await _client.WriteStorageObjectsAsync(_session, new WriteStorageObject {
                Collection = STORAGE_COLLECTION,
                Key = "main",
                Value = json,
                PermissionRead = 1, // Solo owner
                PermissionWrite = 1
            });
            
            Debug.Log($"[NAKAMA] Character saved for {userId}");
            
        } catch (System.Exception e) {
            Debug.LogError($"[NAKAMA] Save failed: {e.Message}");
        }
    }
    
    public void SaveCharacterData(string userId, CharacterData data) {
        // Fire and forget (para disconnect)
        _ = SaveCharacterDataAsync(userId, data);
    }
    
    // ═══════════════════════════════════════════════════════
    // AUTO-SAVE LOOP
    // ═══════════════════════════════════════════════════════
    
    private float _autoSaveInterval = 30f; // 30 segundos
    private float _nextAutoSave;
    
    void Update() {
        if (_session == null) return;
        
        if (Time.time >= _nextAutoSave) {
            AutoSaveAllPlayers();
            _nextAutoSave = Time.time + _autoSaveInterval;
        }
    }
    
    private void AutoSaveAllPlayers() {
        // Solo en servidor
        if (!FishNet.InstanceFinder.IsServer) return;
        
        var players = FindObjectsOfType<PlayerStats>();
        foreach (var player in players) {
            if (player.TryGetComponent(out FishNet.Object.NetworkObject netObj)) {
                
                CharacterData data = ExtractCharacterData(netObj);
                string userId = netObj.OwnerId.ToString();
                
                _ = SaveCharacterDataAsync(userId, data);
            }
        }
        
        Debug.Log($"[NAKAMA] Auto-saved {players.Length} players");
    }
    
    private CharacterData ExtractCharacterData(FishNet.Object.NetworkObject netObj) {
        // Ver ServerManager.ExtractCharacterData() - misma implementación
        return new CharacterData(); // TODO
    }
    
    private CharacterData CreateDefaultCharacter() {
        return new CharacterData {
            classIndex = 0, // Guerrero por defecto
            position = Vector3.zero,
            rotation = Vector3.zero,
            health = 100f,
            mana = 50f,
            items = "[]" // Array vacío
        };
    }
}

// ═══════════════════════════════════════════════════════
// DATA STRUCTURES
// ═══════════════════════════════════════════════════════

[System.Serializable]
public class CharacterData {
    public int classIndex;
    public Vector3 position;
    public Vector3 rotation;
    public float health;
    public float mana;
    public string items; // JSON serializado
}

} // namespace
```

---

## 10. PERFORMANCE BUDGET

### 10.1 Target de Rendimiento
Para 50 jugadores concurrentes:

| Métrica | Target | Crítico | Notas |
| :--- | :--- | :--- | :--- |
| **Server FPS** | 50 FPS | 40 FPS | 1 tick = 20ms |
| **Client FPS** | 60 FPS | 45 FPS | En combate |
| **Draw Calls** | < 150 | < 200 | Static batching |
| **Vertices** | < 500k | < 700k | LOD activo |
| **Network Send** | < 5 KB/s | < 10 KB/s | Por jugador |
| **Memory (Client)** | < 2 GB | < 3 GB | RAM total |
| **Memory (Server)** | < 4 GB | < 6 GB | Headless |

### 10.2 Object Pooling (OBLIGATORIO)

```csharp
// Assets/_Project/0_Core/Architecture/Patterns/ObjectPool.cs

using UnityEngine;
using System.Collections.Generic;

namespace Genesis.Core {

public class ObjectPool<T> where T : MonoBehaviour {
    
    private readonly T _prefab;
    private readonly Queue<T> _pool = new Queue<T>();
    private readonly Transform _container;
    
    public ObjectPool(T prefab, int preWarmCount = 10) {
        _prefab = prefab;
        
        // Contenedor para organizar jerarquía
        GameObject containerObj = new GameObject($"Pool_{prefab.name}");
        _container = containerObj.transform;
        
        // Pre-warm
        for (int i = 0; i < preWarmCount; i++) {
            T instance = GameObject.Instantiate(_prefab, _container);
            instance.gameObject.SetActive(false);
            _pool.Enqueue(instance);
        }
    }
    
    public T Get() {
        T instance;
        
        if (_pool.Count > 0) {
            instance = _pool.Dequeue();
            instance.gameObject.SetActive(true);
        } else {
            // Pool exhausted - crear nuevo
            instance = GameObject.Instantiate(_prefab, _container);
            Debug.LogWarning($"[POOL] {_prefab.name} pool exhausted - creating new instance");
        }
        
        return instance;
    }
    
    public void Return(T instance) {
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(_container);
        _pool.Enqueue(instance);
    }
    
    public void Clear() {
        foreach (var instance in _pool) {
            GameObject.Destroy(instance.gameObject);
        }
        _pool.Clear();
    }
}

} // namespace
```

**ObjectPoolManager.cs:**
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Core {

public class ObjectPoolManager : MonoBehaviour {
    
    public static ObjectPoolManager Instance { get; private set; }
    
    [System.Serializable]
    public class PoolConfig {
        public GameObject prefab;
        public int preWarmCount = 20;
    }
    
    [Header("Pool Configurations")]
    [SerializeField] private PoolConfig[] poolConfigs;
    
    private Dictionary<string, object> _pools = new Dictionary<string, object>();
    
    void Awake() {
        if (Instance != null) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        InitializePools();
    }
    
    private void InitializePools() {
        foreach (var config in poolConfigs) {
            string key = config.prefab.name;
            
            // Obtener tipo del componente principal
            var component = config.prefab.GetComponent<MonoBehaviour>();
            if (component == null) continue;
            
            System.Type type = component.GetType();
            
            // Crear pool genérico usando reflexión
            System.Type poolType = typeof(ObjectPool<>).MakeGenericType(type);
            object pool = System.Activator.CreateInstance(poolType, component, config.preWarmCount);
            
            _pools[key] = pool;
            
            Debug.Log($"[POOL] Initialized {key} with {config.preWarmCount} instances");
        }
    }
    
    public ObjectPool<T> GetPool<T>(string prefabName) where T : MonoBehaviour {
        if (_pools.TryGetValue(prefabName, out object pool)) {
            return pool as ObjectPool<T>;
        }
        
        Debug.LogError($"[POOL] Pool {prefabName} not found!");
        return null;
    }
}

} // namespace
```

**Configurar en Inspector:**
```text
ObjectPoolManager
├── Pool Configs [6]
│   ├── [0] Fireball (PreWarm: 30)
│   ├── [1] Arrow (PreWarm: 50)
│   ├── [2] IceBolt (PreWarm: 20)
│   ├── [3] Explosion_VFX (PreWarm: 20)
│   ├── [4] FloatingText (PreWarm: 40)
│   └── [5] HitSpark (PreWarm: 30)
```

---

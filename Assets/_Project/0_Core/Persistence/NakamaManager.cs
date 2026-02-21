using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FishNet.Object;
using Nakama;
using UnityEngine;

namespace Genesis.Core.Persistence
{
    /// <summary>
    /// Manages Nakama connection and character data persistence.
    /// Lives in the Bootstrap scene as a MonoBehaviour.
    /// Registers itself as IPersistenceService in ServiceLocator.
    /// </summary>
    public class NakamaManager : MonoBehaviour, IPersistenceService
    {
        [Header("Nakama Connection")]
        [SerializeField] private string _scheme = "http";
        [SerializeField] private string _host = "127.0.0.1";
        [SerializeField] private int _port = 7350;
        [SerializeField] private string _serverKey = "defaultkey";

        [Header("Auto-Save")]
        [SerializeField] private float _autoSaveInterval = 30f;
        [SerializeField] private bool _enableAutoSave = true;

        private IClient _client;
        private float _autoSaveTimer;
        private bool _isSavingOnQuit;

        // Per-player Nakama sessions: clientId → ISession
        private readonly Dictionary<int, ISession> _sessions = new Dictionary<int, ISession>();

        // All connected players: clientId → NetworkObject
        private readonly Dictionary<int, NetworkObject> _connectedPlayers = new Dictionary<int, NetworkObject>();

        // Map FishNet connection owner ID → Nakama user ID
        private readonly Dictionary<int, string> _connectionToUserId = new Dictionary<int, string>();

        private const string COLLECTION = "characters";
        private const string KEY = "main";

        void Awake()
        {
            _client = new Client(_scheme, _host, _port, _serverKey);
            ServiceLocator.Instance.Register<IPersistenceService>(this);
            Debug.Log($"[NakamaManager] Initialized → {_scheme}://{_host}:{_port} (key={_serverKey})");
        }

        async void Start()
        {
            Debug.Log("[NakamaManager] Testing Nakama connection...");
            try
            {
                var session = await _client.AuthenticateDeviceAsync("nakama_connection_test_probe", null, true);
                Debug.Log($"[NakamaManager] ✓ Nakama is REACHABLE. Test auth userId={session.UserId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NakamaManager] ✗ Nakama is UNREACHABLE! Is Docker running? Error: {e.Message}");
                Debug.LogError("[NakamaManager] Run 'docker compose up -d' and check http://localhost:7351");
            }
        }

        void OnDestroy()
        {
            ServiceLocator.Instance.Unregister<IPersistenceService>();
        }

        async void OnApplicationQuit()
        {
            if (_connectedPlayers.Count == 0) return;

            _isSavingOnQuit = true;
            Debug.Log($"[NakamaManager] ═══ APPLICATION QUIT: Saving {_connectedPlayers.Count} player(s) ═══");

            if (!ServiceLocator.Instance.TryGet<IPersistenceBridge>(out var bridge))
            {
                Debug.LogWarning("[NakamaManager] Quit save SKIPPED: No IPersistenceBridge");
                return;
            }

            var tasks = new List<Task>();
            foreach (var kvp in _connectedPlayers)
            {
                int clientId = kvp.Key;
                NetworkObject playerObj = kvp.Value;
                if (playerObj == null) continue;

                string userId = GetUserId(clientId);
                if (string.IsNullOrEmpty(userId)) continue;

                try
                {
                    var data = bridge.ExtractPlayerData(playerObj);
                    if (data != null)
                    {
                        tasks.Add(SaveAsyncForClient(clientId, userId, data));
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NakamaManager] Quit save failed for client {clientId}: {e.Message}");
                }
            }

            if (tasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(tasks);
                    Debug.Log($"[NakamaManager] ✓ Quit save COMPLETE: {tasks.Count} player(s) saved");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NakamaManager] Quit save partial failure: {e.Message}");
                }
            }
        }

        void Update()
        {
            if (!_enableAutoSave || _isSavingOnQuit) return;

            _autoSaveTimer += Time.deltaTime;
            if (_autoSaveTimer >= _autoSaveInterval)
            {
                _autoSaveTimer = 0f;
                _ = AutoSaveAllConnectedPlayers();
            }
        }

        // ═══════════════════════════════════════════════════════
        // AUTHENTICATION (per-player sessions)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Authenticate a player with Nakama using device ID.
        /// Stores a per-player session keyed by clientId.
        /// Returns the Nakama user ID.
        /// </summary>
        public async Task<string> AuthenticateDeviceAsync(int clientId, string deviceId)
        {
            try
            {
                var session = await _client.AuthenticateDeviceAsync(deviceId, null, true);
                _sessions[clientId] = session;
                Debug.Log($"[NakamaManager] Authenticated client {clientId}: userId={session.UserId}, username={session.Username}");
                return session.UserId;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NakamaManager] Authentication failed for client {clientId}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Legacy overload — kept for compatibility but should not be used for multi-player.
        /// </summary>
        public async Task<string> AuthenticateDeviceAsync(string deviceId)
        {
            try
            {
                var session = await _client.AuthenticateDeviceAsync(deviceId, null, true);
                Debug.Log($"[NakamaManager] Authenticated (legacy): userId={session.UserId}");
                return session.UserId;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NakamaManager] Authentication failed: {e.Message}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════
        // CONNECTION & PLAYER TRACKING
        // ═══════════════════════════════════════════════════════

        public void RegisterConnection(int clientId, string nakamaUserId)
        {
            _connectionToUserId[clientId] = nakamaUserId;
            Debug.Log($"[NakamaManager] Mapped clientId={clientId} → userId={nakamaUserId}");
        }

        public void UnregisterConnection(int clientId)
        {
            _connectionToUserId.Remove(clientId);
            _sessions.Remove(clientId);
        }

        public string GetUserId(int clientId)
        {
            _connectionToUserId.TryGetValue(clientId, out string userId);
            return userId;
        }

        public void RegisterPlayer(int clientId, NetworkObject playerObj)
        {
            if (playerObj != null)
            {
                _connectedPlayers[clientId] = playerObj;
                Debug.Log($"[NakamaManager] Player registered for persistence: clientId={clientId} (total: {_connectedPlayers.Count})");
            }
        }

        public void UnregisterPlayer(int clientId)
        {
            _connectedPlayers.Remove(clientId);
            Debug.Log($"[NakamaManager] Player unregistered: clientId={clientId} (total: {_connectedPlayers.Count})");
        }

        private ISession GetSession(int clientId)
        {
            _sessions.TryGetValue(clientId, out var session);
            return session;
        }

        // ═══════════════════════════════════════════════════════
        // IPersistenceService IMPLEMENTATION
        // ═══════════════════════════════════════════════════════

        public async Task<CharacterData> LoadAsync(string userId)
        {
            // Find the session for this userId
            int clientId = FindClientIdByUserId(userId);
            var session = GetSession(clientId);

            Debug.Log($"[NakamaManager] LoadAsync for userId={userId}, clientId={clientId}, session={((session != null) ? "valid" : "NULL")}");

            if (session == null)
            {
                Debug.LogError($"[NakamaManager] Cannot load - no session for userId={userId} (clientId={clientId})");
                return null;
            }

            try
            {
                var readObjectId = new StorageObjectId
                {
                    Collection = COLLECTION,
                    Key = KEY,
                    UserId = userId
                };

                var result = await _client.ReadStorageObjectsAsync(session, new[] { readObjectId });

                foreach (var obj in result.Objects)
                {
                    var data = JsonUtility.FromJson<CharacterData>(obj.Value);
                    Debug.Log($"[NakamaManager] Loaded {data.playerName} Lv{data.level} gold={data.gold} pos=({data.posX:F1},{data.posY:F1},{data.posZ:F1})");
                    return data;
                }

                Debug.LogWarning($"[NakamaManager] No character data found for userId={userId} (new player)");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NakamaManager] Load FAILED for userId={userId}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                return null;
            }
        }

        public async Task SaveAsync(string userId, CharacterData data)
        {
            int clientId = FindClientIdByUserId(userId);
            await SaveAsyncForClient(clientId, userId, data);
        }

        public void MarkDirty(NetworkObject playerObj)
        {
            // No-op: auto-save now saves all connected players every interval.
        }

        // ═══════════════════════════════════════════════════════
        // INTERNAL SAVE (uses per-player session)
        // ═══════════════════════════════════════════════════════

        private async Task SaveAsyncForClient(int clientId, string userId, CharacterData data)
        {
            var session = GetSession(clientId);

            if (session == null)
            {
                Debug.LogError($"[NakamaManager] Cannot save - no session for client {clientId} (userId={userId})");
                return;
            }

            if (data == null)
            {
                Debug.LogError($"[NakamaManager] Cannot save - CharacterData is NULL for userId={userId}");
                return;
            }

            try
            {
                data.lastSaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string json = JsonUtility.ToJson(data);

                var writeObject = new WriteStorageObject
                {
                    Collection = COLLECTION,
                    Key = KEY,
                    Value = json,
                    PermissionRead = 2,
                    PermissionWrite = 1
                };

                await _client.WriteStorageObjectsAsync(session, new[] { writeObject });

                Debug.Log($"[NakamaManager] ✓ SAVED {data.playerName} Lv{data.level} gold={data.gold} pos=({data.posX:F1},{data.posY:F1},{data.posZ:F1})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NakamaManager] Save FAILED for client {clientId} (userId={userId}): {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        // ═══════════════════════════════════════════════════════
        // AUTO-SAVE
        // ═══════════════════════════════════════════════════════

        private async Task AutoSaveAllConnectedPlayers()
        {
            if (_connectedPlayers.Count == 0) return;

            if (!ServiceLocator.Instance.TryGet<IPersistenceBridge>(out var bridge))
            {
                Debug.LogWarning("[NakamaManager] Auto-save SKIPPED: No IPersistenceBridge registered!");
                return;
            }

            Debug.Log($"[NakamaManager] Auto-save: {_connectedPlayers.Count} connected player(s)");

            foreach (var kvp in new Dictionary<int, NetworkObject>(_connectedPlayers))
            {
                int clientId = kvp.Key;
                NetworkObject playerObj = kvp.Value;

                if (playerObj == null)
                {
                    _connectedPlayers.Remove(clientId);
                    continue;
                }

                string userId = GetUserId(clientId);
                if (string.IsNullOrEmpty(userId))
                {
                    Debug.LogWarning($"[NakamaManager] Auto-save SKIPPED for client {clientId}: no userId mapped!");
                    continue;
                }

                try
                {
                    var data = bridge.ExtractPlayerData(playerObj);
                    await SaveAsyncForClient(clientId, userId, data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NakamaManager] Auto-save failed for client {clientId}: {e.Message}");
                }
            }
        }

        public async Task SavePlayerNow(NetworkObject playerObj)
        {
            if (playerObj == null) return;

            if (!ServiceLocator.Instance.TryGet<IPersistenceBridge>(out var bridge))
            {
                Debug.LogWarning("[NakamaManager] Cannot save - no IPersistenceBridge registered.");
                return;
            }

            int clientId = playerObj.Owner.ClientId;
            string userId = GetUserId(clientId);
            if (string.IsNullOrEmpty(userId))
            {
                Debug.LogWarning($"[NakamaManager] Cannot save - no userId for clientId={clientId}");
                return;
            }

            var data = bridge.ExtractPlayerData(playerObj);
            await SaveAsyncForClient(clientId, userId, data);
        }

        // ═══════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════

        private int FindClientIdByUserId(string userId)
        {
            foreach (var kvp in _connectionToUserId)
            {
                if (kvp.Value == userId) return kvp.Key;
            }
            return -1;
        }
    }
}

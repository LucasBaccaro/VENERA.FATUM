using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Genesis.Data;
using Genesis.Core;
using Genesis.Core.Networking;
using Genesis.Core.Persistence;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Genesis.Simulation {
    public class PlayerClassManager : NetworkBehaviour {
        [Header("Classes")]
        [SerializeField] private List<ClassData> availableClasses = new List<ClassData>();

        [Header("References")]
        [SerializeField] private PlayerStats stats;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerMotorMultiplayer motor;
        [SerializeField] private Transform visualRoot; // El contenedor donde se instanciará el modelo

        // SyncVar para que todos los clientes sepan qué clase tiene el jugador
        private readonly SyncVar<int> _currentClassIndex = new SyncVar<int>(-1);
        private readonly SyncVar<string> _playerName = new SyncVar<string>("");
        private readonly SyncVar<bool> _classLocked = new SyncVar<bool>(false);
        private readonly SyncVar<int> _faction = new SyncVar<int>(0);

        // Cached persistence identifiers (set after successful persistence flow)
        private int _persistedClientId = -1;
        private string _persistedUserId;
        private bool _persistenceReady;

        public string PlayerName => _playerName.Value;
        public int CurrentClassIndex => _currentClassIndex.Value;
        public int Faction => _faction.Value;

        public override void OnStartNetwork() {
            base.OnStartNetwork();
            _currentClassIndex.OnChange += OnClassChanged;
            _playerName.OnChange += OnPlayerNameChanged;
            _faction.OnChange += OnFactionChanged;
        }

        public override void OnStartClient() {
            base.OnStartClient();

            // Notify UI for nameplates
            Debug.Log($"[PlayerClassManager] OnStartClient: Triggering registration for {NetworkObject.OwnerId}");
            Genesis.Core.EventBus.Trigger("OnPlayerNameplateRegister", this);

            if (!base.IsOwner) return;

            // If login data was set, send it to the server
            if (LoginData.IsSet) {
                CmdSetLoginData(
                    LoginData.PlayerName, LoginData.ClassIndex, LoginData.FactionIndex,
                    LoginData.AuthToken, LoginData.RefreshToken, LoginData.IsNewCharacter);
                LoginData.Clear();
            }
        }

        public override void OnStartServer() {
            base.OnStartServer();
            // Inicializar con la primera clase por defecto si no hay ninguna
            if (_currentClassIndex.Value == -1 && availableClasses.Count > 0) {
                SetClass(0);
            }
        }

        public override void OnStopClient() {
            base.OnStopClient();
            Genesis.Core.EventBus.Trigger("OnPlayerNameplateUnregister", this);
        }

        public override void OnStopServer() {
            base.OnStopServer();
            SaveBeforeDespawn();
        }

        /// <summary>
        /// Safety-net save for when the server stops (host player).
        /// For remote clients, SavePlayerOnDisconnect already saved before this fires.
        /// Checks if session still exists to avoid duplicate error logs.
        /// </summary>
        private void SaveBeforeDespawn() {
            if (!_persistenceReady || string.IsNullOrEmpty(_persistedUserId)) return;

            if (!ServiceLocator.Instance.TryGet<IPersistenceService>(out var persistence)) return;

            var nakama = persistence as NakamaManager;
            if (nakama == null) return;

            // If session was already cleaned up by disconnect handler, skip silently
            string checkUserId = nakama.GetUserId(_persistedClientId);
            if (string.IsNullOrEmpty(checkUserId)) {
                Debug.Log($"[PlayerClassManager] OnStopServer: session already cleaned up (disconnect save handled it)");
                return;
            }

            // Session still valid → this is a server-stop case (host player)
            if (!ServiceLocator.Instance.TryGet<IPersistenceBridge>(out var bridge)) return;

            try {
                var data = bridge.ExtractPlayerData(base.NetworkObject);
                if (data != null) {
                    var saveTask = persistence.SaveAsync(_persistedUserId, data);
                    saveTask.ContinueWith(t => {
                        if (t.IsFaulted)
                            Debug.LogError($"[PlayerClassManager] OnStopServer save FAILED: {t.Exception?.InnerException?.Message}");
                        else
                            Debug.Log($"[PlayerClassManager] ✓ OnStopServer save CONFIRMED: {data.playerName}");
                    });
                    Debug.Log($"[PlayerClassManager] OnStopServer save queued: {data.playerName} (host player)");
                }
            } catch (System.Exception e) {
                Debug.LogError($"[PlayerClassManager] OnStopServer save exception: {e.Message}");
            }
        }

        void Update() {
            if (!base.IsOwner) return;
            if (_classLocked.Value) return;

            // Detectar tecla G para rotar entre clases (Usando el nuevo Input System)
            if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) {
                CmdRequestClassSwitch();
            }
        }

        [ServerRpc]
        private void CmdSetLoginData(string playerName, int classIndex, int factionIndex,
            string authToken, string refreshToken, bool isNewCharacter) {
            _playerName.Value = playerName;
            _faction.Value = factionIndex;
            if (classIndex >= 0 && classIndex < availableClasses.Count) {
                SetClass(classIndex);
            }
            _classLocked.Value = true;

            // Trigger async persistence load
            LoadOrCreateCharacterAsync(playerName, classIndex, factionIndex, authToken, refreshToken, isNewCharacter);
        }

        /// <summary>
        /// Async server-side: authenticate with Nakama, load or create character data, hydrate player.
        /// Called after SetClass so base class stats are initialized before hydration overwrites them.
        /// Supports token-based auth (from Login scene) with fallback to device auth (legacy/testing).
        /// </summary>
        [Server]
        private async void LoadOrCreateCharacterAsync(string playerName, int classIndex, int factionIndex,
            string authToken = "", string refreshToken = "", bool isNewCharacter = false) {
            Debug.Log($"[PlayerClassManager] ═══ PERSISTENCE FLOW START ═══ player={playerName} class={classIndex} faction={factionIndex} clientId={base.Owner.ClientId}");

            try
            {
                // Step 1: Get persistence service
                if (!ServiceLocator.Instance.TryGet<IPersistenceService>(out var persistence)) {
                    Debug.LogError("[PlayerClassManager] STEP 1 FAILED: No IPersistenceService registered! Is NakamaManager in the scene?");
                    return;
                }
                Debug.Log("[PlayerClassManager] Step 1 ✓ IPersistenceService found");

                var nakama = persistence as NakamaManager;
                if (nakama == null) {
                    Debug.LogError("[PlayerClassManager] STEP 1b FAILED: IPersistenceService is not NakamaManager!");
                    return;
                }

                // Step 2: Authenticate with Nakama
                string userId;
                bool hasTokens = !string.IsNullOrEmpty(authToken) && !string.IsNullOrEmpty(refreshToken);

                if (hasTokens)
                {
                    // Token-based auth: restore session from Login scene tokens
                    Debug.Log($"[PlayerClassManager] Step 2: Restoring session from auth tokens");
                    userId = nakama.RestoreSession(base.Owner.ClientId, authToken, refreshToken);
                }
                else
                {
                    // Fallback: device auth (legacy, for editor testing without Login scene)
                    string deviceId = $"genesis_char_{playerName.ToLowerInvariant().Trim()}";
                    Debug.Log($"[PlayerClassManager] Step 2: Authenticating with deviceId={deviceId} (legacy fallback)");
                    userId = await nakama.AuthenticateDeviceAsync(base.Owner.ClientId, deviceId);
                }

                if (string.IsNullOrEmpty(userId)) {
                    Debug.LogError("[PlayerClassManager] STEP 2 FAILED: Nakama authentication returned null. Is Docker/Nakama running?");
                    return;
                }
                Debug.Log($"[PlayerClassManager] Step 2 ✓ Authenticated: userId={userId}");

                // Step 3: Register connection mapping
                nakama.RegisterConnection(base.Owner.ClientId, userId);
                Debug.Log($"[PlayerClassManager] Step 3 ✓ Connection mapped: clientId={base.Owner.ClientId} → userId={userId}");

                // Step 4: Load existing data
                Debug.Log($"[PlayerClassManager] Step 4: Loading character data...");
                var data = await persistence.LoadAsync(userId);

                if (data != null) {
                    Debug.Log($"[PlayerClassManager] Step 4 ✓ Character FOUND: {data.playerName} Lv{data.level} gold={data.gold} class={data.classIndex} faction={data.faction}");

                    // Step 5a: Hydrate existing character
                    if (!ServiceLocator.Instance.TryGet<IPersistenceBridge>(out var bridge)) {
                        Debug.LogError("[PlayerClassManager] STEP 5a FAILED: No IPersistenceBridge registered!");
                        return;
                    }

                    // Re-set class from saved data (may differ from login selection)
                    if (data.classIndex >= 0 && data.classIndex < availableClasses.Count &&
                        data.classIndex != _currentClassIndex.Value) {
                        Debug.Log($"[PlayerClassManager] Switching class from {_currentClassIndex.Value} to saved class {data.classIndex}");
                        SetClass(data.classIndex);
                    }

                    // Restore persisted faction (overrides login selection)
                    _faction.Value = data.faction;

                    bridge.HydratePlayer(base.NetworkObject, data);
                    Debug.Log($"[PlayerClassManager] Step 5a ✓ Character HYDRATED: {data.playerName} Lv{data.level}");
                } else {
                    Debug.Log($"[PlayerClassManager] Step 4: No saved data → creating NEW character");

                    // Step 5b: Create new character
                    if (!ServiceLocator.Instance.TryGet<IPersistenceBridge>(out var bridge)) {
                        Debug.LogError("[PlayerClassManager] STEP 5b FAILED: No IPersistenceBridge registered!");
                        return;
                    }

                    var newData = bridge.CreateNewPlayerData(playerName, classIndex, transform.position);
                    newData.faction = factionIndex;
                    await persistence.SaveAsync(userId, newData);

                    // Grant starter items only for NEW characters
                    var starterGranter = GetComponent<StarterItemGranter>();
                    if (starterGranter != null) {
                        starterGranter.GrantStarterItems();
                    }

                    Debug.Log($"[PlayerClassManager] Step 5b ✓ New character SAVED + starter items granted: {playerName} at {transform.position}");
                }

                // Step 6: Register player for auto-save and quit-save
                nakama.RegisterPlayer(base.Owner.ClientId, base.NetworkObject);
                persistence.MarkDirty(base.NetworkObject);
                Debug.Log($"[PlayerClassManager] Step 6 ✓ Player registered for persistence tracking");

                // Cache identifiers for safety-net save (OnStopServer)
                _persistedClientId = base.Owner.ClientId;
                _persistedUserId = userId;
                _persistenceReady = true;

                Debug.Log($"[PlayerClassManager] ═══ PERSISTENCE FLOW COMPLETE ═══");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerClassManager] ═══ PERSISTENCE FLOW EXCEPTION ═══ {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        [ServerRpc]
        private void CmdRequestClassSwitch() {
            if (_classLocked.Value) return;
            int nextIndex = (_currentClassIndex.Value + 1) % availableClasses.Count;
            SetClass(nextIndex);
        }

        [Server]
        private void SetClass(int index) {
            _currentClassIndex.Value = index;

            ClassData data = availableClasses[index];

            // 1. Actualizar Stats en el Servidor
            if (stats != null) {
                stats.InitializeFromClass(data);
            }

            // 2. Actualizar Habilidades en el Servidor
            if (combat != null) {
                combat.UpdateAbilitiesFromClass(data);
            }

            // 3. Notificar EquipmentManager para recalcular stats y validar equipo
            var equipmentManager = GetComponent<Genesis.Simulation.EquipmentManager>();
            if (equipmentManager != null) {
                equipmentManager.UpdateBaseStats(data.MaxHealth, data.MaxMana, data.HealthPerLevel, data.ManaPerLevel);
                equipmentManager.ValidateEquipmentForClass(data.ClassName);
            }

            // 4. Notificar PlayerAttributes para recalcular derived stats
            var attributes = GetComponent<Genesis.Simulation.PlayerAttributes>();
            if (attributes != null) {
                attributes.RecalculateDerivedStats();
            }
        }

        private void OnClassChanged(int oldIndex, int newIndex, bool asServer) {
            if (newIndex < 0 || newIndex >= availableClasses.Count) return;
            
            ClassData data = availableClasses[newIndex];
            UpdateVisuals(data);

            // Actualizar habilidades localmente para que la UI pueda leerlas
            if (combat != null) {
                combat.UpdateAbilitiesFromClass(data);
            }

            // Solo el dueño actualiza su propia UI
            if (base.IsOwner) {
                EventBus.Trigger("OnLoadoutChanged");
                EventBus.Trigger("OnClassChanged", data.ClassName, data.ClassIcon);
                Debug.Log($"[PlayerClassManager] UI Update triggered for class: {data.ClassName}");
            }
        }

        private void UpdateVisuals(ClassData data) {
            // 1. Limpiar visual anterior
            foreach (Transform child in visualRoot) {
                Destroy(child.gameObject);
            }

            // 2. Instanciar nuevo modelo
            if (data.ModelPrefab != null) {
                GameObject model = Instantiate(data.ModelPrefab, visualRoot);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                // 3. Buscar referencias críticas dentro del nuevo modelo
                Animator newAnimator = model.GetComponentInChildren<Animator>();
                if (newAnimator == null) newAnimator = model.GetComponent<Animator>();

                if (newAnimator != null && data.AnimatorController != null) {
                    newAnimator.runtimeAnimatorController = data.AnimatorController;
                }

                // Buscar por varios nombres posibles por flexibilidad
                Transform spawnPoint = FindChildRecursive(model.transform, "CastVFXSpawnPoint");
                if (spawnPoint == null) spawnPoint = FindChildRecursive(model.transform, "HandCastPoint");
                if (spawnPoint == null) spawnPoint = FindChildRecursive(model.transform, "CastVFXHandPoint");
                if (spawnPoint == null) spawnPoint = FindChildRecursive(model.transform, "SpawnPoint");
                if (spawnPoint == null) spawnPoint = FindChildRecursive(model.transform, "HandPoint");
                if (spawnPoint == null) spawnPoint = FindChildRecursive(model.transform, "CastPoint");

                if (spawnPoint == null) {
                    Debug.LogWarning($"[PlayerClassManager] No se encontró spawn point ('CastVFXSpawnPoint') en el modelo de {data.ClassName}.");
                }

                // 4. Re-vincular en los sistemas
                if (combat != null) {
                    combat.UpdateVisualReferences(newAnimator, spawnPoint);
                }
                if (motor != null) {
                    motor.animator = newAnimator;
                }
            }
        }

        private Transform FindChildRecursive(Transform parent, string name) {
            if (parent.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return parent;
            foreach (Transform child in parent) {
                Transform result = FindChildRecursive(child, name);
                if (result != null) return result;
            }
            return null;
        }
        public string GetCurrentClassName() {
            if (_currentClassIndex.Value >= 0 && _currentClassIndex.Value < availableClasses.Count) {
                return availableClasses[_currentClassIndex.Value].ClassName;
            }
            return "";
        }

        public ClassData CurrentClassData {
            get {
                if (_currentClassIndex.Value >= 0 && _currentClassIndex.Value < availableClasses.Count) {
                    return availableClasses[_currentClassIndex.Value];
                }
                return null;
            }
        }

        private void OnFactionChanged(int old, int next, bool asServer) {
            Genesis.Core.EventBus.Trigger("OnPlayerFactionChanged", this);
        }

        private void OnPlayerNameChanged(string oldName, string newName, bool asServer) {
            // Trigger always so the HUD/NameplateManager can see the change for any player
            Genesis.Core.EventBus.Trigger("OnPlayerNameChanged", newName);
            
            if (base.IsOwner) {
                // Specific local handling if needed
            }
        }
    }
}

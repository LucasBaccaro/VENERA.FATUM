using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Genesis.Data;
using Genesis.Core;
using Genesis.Core.Networking;
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

        public string PlayerName => _playerName.Value;

        public override void OnStartNetwork() {
            base.OnStartNetwork();
            _currentClassIndex.OnChange += OnClassChanged;
            _playerName.OnChange += OnPlayerNameChanged;
        }

        public override void OnStartClient() {
            base.OnStartClient();
            if (!base.IsOwner) return;

            // If login data was set, send it to the server
            if (LoginData.IsSet) {
                CmdSetLoginData(LoginData.PlayerName, LoginData.ClassIndex);
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

        void Update() {
            if (!base.IsOwner) return;
            if (_classLocked.Value) return;

            // Detectar tecla G para rotar entre clases (Usando el nuevo Input System)
            if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) {
                CmdRequestClassSwitch();
            }
        }

        [ServerRpc]
        private void CmdSetLoginData(string playerName, int classIndex) {
            _playerName.Value = playerName;
            if (classIndex >= 0 && classIndex < availableClasses.Count) {
                SetClass(classIndex);
            }
            _classLocked.Value = true;
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

        private void OnPlayerNameChanged(string oldName, string newName, bool asServer) {
            if (base.IsOwner) {
                EventBus.Trigger("OnPlayerNameChanged", newName);
            }
        }
    }
}

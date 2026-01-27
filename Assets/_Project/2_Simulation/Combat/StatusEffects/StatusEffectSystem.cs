using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Genesis.Data;
using Genesis.Core;
using System.Collections.Generic;
using System.Linq;

namespace Genesis.Simulation.Combat {

    /// <summary>
    /// Sistema de gestión de Status Effects (Buffs/Debuffs).
    /// Cada entidad que puede recibir efectos debe tener este componente.
    /// Server-authoritative con sincronización automática de efectos activos.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class StatusEffectSystem : NetworkBehaviour {

        // ═══════════════════════════════════════════════════════
        // SYNCVAR - Estado sincronizado con clientes (FishNet v4)
        // ═══════════════════════════════════════════════════════

        private readonly SyncDictionary<int, ActiveEffect> _activeEffects = new();

        // Cache local para VFX
        private Dictionary<int, GameObject> _vfxInstances = new Dictionary<int, GameObject>();

        // Referencias
        private PlayerStats _stats;
        private Animator _animator;

        // ═══════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════

        void Awake() {
            _stats = GetComponent<PlayerStats>();
            _animator = GetComponentInChildren<Animator>();
        }

        public override void OnStartNetwork() {
            base.OnStartNetwork();

            // Suscribirse a cambios en el diccionario (FishNet v4)
            _activeEffects.OnChange += OnEffectsChanged;
        }

        public override void OnStartClient() {
            base.OnStartClient();

            // Inicializar VFX locales para efectos ya activos (late join)
            foreach (var kvp in _activeEffects) {
                StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(kvp.Value.effectID);
                if (data != null && data.VFXPrefab != null) {
                    // Solo spawnear VFX locales (los networked ya están spawneados)
                    if (data.VFXPrefab.GetComponent<NetworkObject>() == null) {
                        SpawnLocalVFX(kvp.Key, kvp.Value.effectID);
                    }
                }
            }

            // Actualizar visuales iniciales (incluyendo Animator speed)
            UpdateAnimatorVisuals();
        }

        // ═══════════════════════════════════════════════════════
        // SERVER API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Aplica un status effect a esta entidad.
        /// </summary>
        [Server]
        public void ApplyEffect(StatusEffectData data) {
            if (data == null) {
                Debug.LogWarning("[StatusEffectSystem] Trying to apply null effect");
                return;
            }

            // Validar que el ID sea válido
            if (data.ID <= 0) {
                #if UNITY_EDITOR
                Debug.LogError($"[StatusEffectSystem] Trying to apply StatusEffectData '{data.name}' (Name: '{data.Name}') with invalid ID: {data.ID}. " +
                              $"IDs must be > 0. Check the ScriptableObject at path: {UnityEditor.AssetDatabase.GetAssetPath(data)}", data);
                #else
                Debug.LogError($"[StatusEffectSystem] Trying to apply StatusEffectData '{data.name}' (Name: '{data.Name}') with invalid ID: {data.ID}. IDs must be > 0.", data);
                #endif
                return;
            }

            // ═══ CASO 1: Invulnerable = ignora todo excepto buffs ═══
            if (HasEffect(EffectType.Invulnerable) && !data.IsBuff) {
                Debug.Log($"[StatusEffectSystem] {gameObject.name} es invulnerable, efecto {data.Name} ignorado");
                return;
            }

            // ═══ CASO 2: Efecto ya existe ═══
            if (_activeEffects.ContainsKey(data.ID)) {

                // Si NO es stackable, refrescar duración
                if (!data.IsStackable) {
                    ActiveEffect existing = _activeEffects[data.ID];
                    existing.expirationTime = Time.time + data.Duration;
                    _activeEffects[data.ID] = existing;

                    Debug.Log($"[StatusEffectSystem] Efecto {data.Name} refrescado en {gameObject.name}");
                    return;
                }

                // Si ES stackable, incrementar stack
                ActiveEffect stacked = _activeEffects[data.ID];
                if (stacked.stackCount < data.MaxStacks) {
                    stacked.stackCount++;
                    stacked.expirationTime = Time.time + data.Duration;
                    _activeEffects[data.ID] = stacked;

                    Debug.Log($"[StatusEffectSystem] Efecto {data.Name} stackeado x{stacked.stackCount} en {gameObject.name}");
                }
                return;
            }

            // ═══ CASO 3: Nuevo efecto ═══
            ActiveEffect newEffect = new ActiveEffect {
                effectID = data.ID,
                type = data.Type,
                expirationTime = data.Duration > 0 ? Time.time + data.Duration : float.MaxValue,
                stackCount = 1,
                tickInterval = data.TickInterval,
                nextTickTime = Time.time + data.TickInterval,
                percentageValue = data.PercentageValue,
                flatValue = data.FlatValue
            };

            _activeEffects.Add(data.ID, newEffect);

            // Aplicar efecto inmediato (ej: Shield)
            if (data.Type.HasFlag(EffectType.Shield) && _stats != null) {
                _stats.AddShield(data.FlatValue);
            }

            Debug.Log($"[StatusEffectSystem] Efecto {data.Name} aplicado a {gameObject.name}");

            // Spawnear VFX networked si el prefab tiene NetworkObject
            if (data.VFXPrefab != null && data.VFXPrefab.GetComponent<NetworkObject>() != null) {
                SpawnNetworkedVFX(data.ID, data.VFXPrefab);
            }

            // Notificar clientes para VFX/SFX
            RpcOnEffectApplied(data.ID);

            // Trigger evento
            EventBus.Trigger("OnStatusEffectApplied", base.NetworkObject, data);
        }

        /// <summary>
        /// Remueve un status effect específico.
        /// </summary>
        [Server]
        public void RemoveEffect(int effectID) {
            if (!_activeEffects.ContainsKey(effectID)) return;

            ActiveEffect effect = _activeEffects[effectID];

            // Buscar data para cleanup
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(effectID);

            // Limpiar shield si corresponde
            if (effect.type.HasFlag(EffectType.Shield) && _stats != null) {
                _stats.RemoveShield(effect.flatValue);
            }

            _activeEffects.Remove(effectID);

            Debug.Log($"[StatusEffectSystem] Efecto {(data != null ? data.Name : effectID.ToString())} removido de {gameObject.name}");

            // Despawnear VFX networked si existe
            if (data != null && data.VFXPrefab != null && data.VFXPrefab.GetComponent<NetworkObject>() != null) {
                DespawnNetworkedVFX(effectID);
            }

            RpcOnEffectRemoved(effectID);

            // Trigger evento
            if (data != null) {
                EventBus.Trigger("OnStatusEffectRemoved", base.NetworkObject, data);
            }
        }

        /// <summary>
        /// Remueve todos los efectos de un tipo específico.
        /// </summary>
        [Server]
        public void RemoveEffectsOfType(EffectType type) {
            List<int> toRemove = new List<int>();

            foreach (var kvp in _activeEffects) {
                if (kvp.Value.type.HasFlag(type)) {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (int id in toRemove) {
                RemoveEffect(id);
            }
        }

        /// <summary>
        /// Remueve todos los debuffs (efectos negativos).
        /// </summary>
        [Server]
        public void RemoveAllDebuffs() {
            List<int> toRemove = new List<int>();

            foreach (var kvp in _activeEffects) {
                StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(kvp.Value.effectID);
                if (data != null && !data.IsBuff) {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (int id in toRemove) {
                RemoveEffect(id);
            }
        }

        // ═══════════════════════════════════════════════════════
        // UPDATE LOOP (Server)
        // ═══════════════════════════════════════════════════════

        void Update() {
            if (!base.IsServer) return;

            // Procesar todos los efectos activos
            List<int> toRemove = new List<int>();
            Dictionary<int, ActiveEffect> toUpdate = new Dictionary<int, ActiveEffect>();

            // Crear snapshot del diccionario para evitar "Collection was modified" exception
            foreach (var kvp in _activeEffects.ToList()) {
                int id = kvp.Key;
                ActiveEffect effect = kvp.Value;

                // ═══ EXPIRACIÓN ═══
                if (Time.time >= effect.expirationTime) {
                    toRemove.Add(id);
                    continue;
                }

                // ═══ TICK DAMAGE/HEAL ═══
                if (effect.flatValue != 0 && Time.time >= effect.nextTickTime) {
                    ProcessTick(effect);

                    // Actualizar siguiente tick (guardar para aplicar después)
                    effect.nextTickTime = Time.time + effect.tickInterval;
                    toUpdate[id] = effect;
                }
            }

            // Aplicar actualizaciones (fuera del foreach para evitar modificar durante iteración)
            foreach (var kvp in toUpdate) {
                _activeEffects[kvp.Key] = kvp.Value;
            }

            // Remover efectos expirados
            foreach (int id in toRemove) {
                RemoveEffect(id);
            }
        }

        [Server]
        private void ProcessTick(ActiveEffect effect) {
            if (_stats == null) return;

            if (effect.type.HasFlag(EffectType.Poison)) {
                // DoT (Damage over Time)
                float totalDamage = effect.flatValue * effect.stackCount;
                _stats.TakeDamage(totalDamage, null);

                Debug.Log($"[StatusEffectSystem] Poison tick: {totalDamage} damage to {gameObject.name}");

            } else if (effect.type.HasFlag(EffectType.Regen)) {
                // HoT (Heal over Time)
                float totalHeal = effect.flatValue * effect.stackCount;
                _stats.Heal(totalHeal);

                Debug.Log($"[StatusEffectSystem] Regen tick: {totalHeal} heal to {gameObject.name}");
            }
        }

        // ═══════════════════════════════════════════════════════
        // QUERIES (Usadas por otros sistemas)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Verifica si esta entidad tiene algún efecto del tipo especificado.
        /// </summary>
        public bool HasEffect(EffectType type) {
            foreach (var effect in _activeEffects.Values) {
                if (effect.type.HasFlag(type)) return true;
            }
            return false;
        }

        /// <summary>
        /// Obtiene el multiplicador de velocidad considerando Slow y Speed.
        /// Retorna 1.0 si no hay efectos. < 1.0 si está slowed, > 1.0 si tiene speed boost.
        /// </summary>
        public float GetMovementSpeedMultiplier() {
            float multiplier = 1f;

            // Buscar el slow más fuerte
            float maxSlow = 0f;
            foreach (var effect in _activeEffects.Values) {
                if (effect.type.HasFlag(EffectType.Slow)) {
                    maxSlow = Mathf.Max(maxSlow, effect.percentageValue);
                }
            }

            if (maxSlow > 0) {
                multiplier = 1f - maxSlow; // 0.5 magnitude = 50% slower = multiplier 0.5
            }

            // Buscar speed boost
            float maxSpeed = 0f;
            foreach (var effect in _activeEffects.Values) {
                if (effect.type.HasFlag(EffectType.Speed)) {
                    maxSpeed = Mathf.Max(maxSpeed, effect.percentageValue);
                }
            }

            if (maxSpeed > 0) {
                multiplier += maxSpeed; // 0.3 magnitude = +30% speed
            }

            return multiplier;
        }

        /// <summary>
        /// Obtiene el multiplicador de velocidad de casteo/ataque (Haste).
        /// </summary>
        public float GetHasteMultiplier() {
            float maxHaste = 0f;

            foreach (var effect in _activeEffects.Values) {
                if (effect.type.HasFlag(EffectType.Haste)) {
                    maxHaste = Mathf.Max(maxHaste, effect.percentageValue);
                }
            }

            return 1f + maxHaste; // 0.3 = +30% cast speed
        }

        /// <summary>
        /// Obtiene el número de stacks de un efecto específico.
        /// </summary>
        public int GetStackCount(int effectID) {
            return _activeEffects.TryGetValue(effectID, out ActiveEffect effect) ? effect.stackCount : 0;
        }

        /// <summary>
        /// Obtiene todos los efectos activos (para UI).
        /// </summary>
        public Dictionary<int, ActiveEffect> GetActiveEffects() {
            return new Dictionary<int, ActiveEffect>(_activeEffects);
        }

        // ═══════════════════════════════════════════════════════
        // CLIENT VISUALS
        // ═══════════════════════════════════════════════════════

        private void OnEffectsChanged(SyncDictionaryOperation op, int key, ActiveEffect value, bool asServer) {
            // Solo clientes procesan VFX locales (sin NetworkObject)
            if (asServer) return;

            // Ignorar operación Complete (señal de sincronización completa de FishNet)
            if (op == SyncDictionaryOperation.Complete) {
                return;
            }

            Debug.Log($"[StatusEffectSystem] CLIENT: OnEffectsChanged called. Op={op}, Key={key}, EffectID={value.effectID}");

            // Si es Remove, usamos la key directamente porque el value viene vacío
            if (op == SyncDictionaryOperation.Remove) {
                DespawnLocalVFX(key);
                return;
            }

            // Validar que el effectID sea válido (solo para Add/Set)
            if (value.effectID <= 0) {
                Debug.LogWarning($"[StatusEffectSystem] Received effect with invalid ID: {value.effectID}. Op={op}, Key={key}");
                return;
            }

            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(value.effectID);
            if (data == null || data.VFXPrefab == null) return;

            // Si el VFX tiene NetworkObject, el servidor ya lo spawneó
            if (data.VFXPrefab.GetComponent<NetworkObject>() != null) return;

            // Solo spawnear VFX locales (sin NetworkObject)
            switch (op) {
                case SyncDictionaryOperation.Add:
                    SpawnLocalVFX(key, value.effectID);
                    break;

                case SyncDictionaryOperation.Remove:
                    DespawnLocalVFX(key);
                    break;
            }

            // Actualizar velocidad de animación si cambió el estado de Slow
            UpdateAnimatorVisuals();
        }

        /// <summary>
        /// Actualiza visuales afectados por status effects (ej: velocidad del Animator).
        /// </summary>
        private void UpdateAnimatorVisuals() {
            if (_animator == null) return;

            // Al momento de aplicar un slow, también se realintice la animación del animator en curso, bajemosla un 50%
            if (HasEffect(EffectType.Slow)) {
                _animator.speed = 0.5f;
            } else {
                _animator.speed = 1.0f;
            }
        }

        [ObserversRpc]
        private void RpcOnEffectApplied(int effectID) {
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(effectID);
            if (data == null) return;

            // SFX
            if (data.ApplySound != null) {
                // TODO: Integrar con AudioManager cuando esté implementado
                // AudioManager.Instance.PlaySFX(data.ApplySound, transform.position);
                Debug.Log($"[StatusEffectSystem] Playing apply sound for {data.Name}");
            }

            // Floating text (solo owner)
            if (base.IsOwner) {
                EventBus.Trigger("OnStatusApplied", data.Name, data.IsBuff);
            }
        }

        [ObserversRpc]
        private void RpcOnEffectRemoved(int effectID) {
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(effectID);
            if (data == null) return;

            Debug.Log($"[StatusEffectSystem] Effect {data.Name} removed (client notification). Executing local cleanup.");
            
            // Redundant cleanup: Force local despawn in case SyncDictionary update hasn't arrived or failed
            DespawnLocalVFX(effectID);
        }

        /// <summary>
        /// Spawnea VFX networked (con NetworkObject) en el servidor
        /// </summary>
        [Server]
        private void SpawnNetworkedVFX(int effectID, GameObject vfxPrefab) {
            if (_vfxInstances.ContainsKey(effectID)) return;

            GameObject vfx = Instantiate(vfxPrefab);
            vfx.transform.position = transform.position + Vector3.up;
            
            // Spawnear en la red
            NetworkObject vfxNetObj = vfx.GetComponent<NetworkObject>();
            FishNet.InstanceFinder.ServerManager.Spawn(vfx);
            _vfxInstances[effectID] = vfx;

            // Notificar a todos los clientes para que establezcan el parent
            RpcSetVFXParent(vfxNetObj, effectID);

            Debug.Log($"[StatusEffectSystem] Networked VFX spawned for effect {effectID}");
        }

        /// <summary>
        /// RPC para establecer el parent del VFX en todos los clientes
        /// </summary>
        [ObserversRpc]
        private void RpcSetVFXParent(NetworkObject vfxNetObj, int effectID) {
            if (vfxNetObj == null) return;

            // Establecer como hijo de este player
            vfxNetObj.transform.SetParent(transform);
            vfxNetObj.transform.localPosition = Vector3.up * 0f;

            // Cachear en clientes también (para cleanup)
            if (!base.IsServer && !_vfxInstances.ContainsKey(effectID)) {
                _vfxInstances[effectID] = vfxNetObj.gameObject;
            }

            Debug.Log($"[StatusEffectSystem] VFX parent set for effect {effectID} on {(base.IsServer ? "SERVER" : "CLIENT")}");
        }

        /// <summary>
        /// Despawnea VFX networked
        /// </summary>
        [Server]
        private void DespawnNetworkedVFX(int effectID) {
            bool foundInDict = _vfxInstances.TryGetValue(effectID, out GameObject vfx);
            
            if (foundInDict && vfx != null) {
                if (vfx.TryGetComponent(out NetworkObject netObj)) {
                    if (netObj.IsSpawned) {
                        FishNet.InstanceFinder.ServerManager.Despawn(vfx);
                        Debug.Log($"[StatusEffectSystem] Networked VFX despawned for effect {effectID}");
                    } else {
                        Debug.LogWarning($"[StatusEffectSystem] Networked VFX for effect {effectID} was not spawned on network!");
                        Destroy(vfx);
                    }
                }
                _vfxInstances.Remove(effectID);
            } else {
                // Fallback: Buscar en hijos si no está en el diccionario (Safety Net)
                Debug.LogWarning($"[StatusEffectSystem] VFX for effect {effectID} not found in dictionary. Searching hierarchy for orphans...");
                
                StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(effectID);
                if (data != null && data.VFXPrefab != null) {
                    string vfxName = data.VFXPrefab.name;
                    foreach (Transform child in transform) {
                        if (child.name.Contains(vfxName)) {
                            if (child.TryGetComponent(out NetworkObject childNetObj) && childNetObj.IsSpawned) {
                                Debug.Log($"[StatusEffectSystem] Found orphaned Networked VFX {child.name}. Despawning.");
                                FishNet.InstanceFinder.ServerManager.Despawn(child.gameObject);
                            }
                        }
                    }
                    
                    // Fallback nivel 2: Global Search (si falló el parenting)
                    CleanupOrphanedVFXGlobal(effectID);

                }
            }
        }

        /// <summary>
        /// Spawnea VFX local (sin NetworkObject) en cada cliente
        /// </summary>
        private void SpawnLocalVFX(int key, int effectID) {
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(effectID);
            if (data == null || data.VFXPrefab == null) return;

            // No spawneamos VFX duplicados
            if (_vfxInstances.ContainsKey(key)) return;

            GameObject vfx = Instantiate(data.VFXPrefab, transform);
            vfx.transform.localPosition = Vector3.up * 0f; // Sobre la cabeza
            _vfxInstances[key] = vfx;

            Debug.Log($"[StatusEffectSystem] Local VFX spawned for {data.Name}");
        }

        /// <summary>
        /// Despawnea VFX local y realiza limpieza de emergencia si es necesario
        /// </summary>
        private void DespawnLocalVFX(int key) {
            // 1. Intentar limpiar desde el diccionario
            if (_vfxInstances.TryGetValue(key, out GameObject vfx)) {
                if (vfx != null) {
                    // Si tiene NetworkObject, deberíamos esperar a FishNet, pero si sigue aquí...
                    if (vfx.GetComponent<NetworkObject>() == null) {
                        Destroy(vfx);
                    } else {
                        // Es networked. Si el servidor ya mandó remove, esto debería desaparecer.
                        // Si no desaparece, es un "fantasma".
                        StartCoroutine(VerifyAndCleanupNetworkedVFX(vfx));
                    }
                }
                _vfxInstances.Remove(key);
            }

            // 2. Limpieza de emergencia por nombre (para casos de desync o duplicados)
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(key);
            if (data != null && data.VFXPrefab != null) {
                string vfxName = data.VFXPrefab.name;
                
                // Buscar en hijos directos objetos con el mismo nombre que no estén en el diccionario
                List<GameObject> toDestroy = new List<GameObject>();
                foreach (Transform child in transform) {
                    // Chequeo simple por nombre (Armadura_Arcana(Clone) contiene Armadura_Arcana)
                    if (child.name.Contains(vfxName) && !_vfxInstances.ContainsValue(child.gameObject)) {
                        toDestroy.Add(child.gameObject);
                    }
                }

                foreach (var obj in toDestroy) {
                    Debug.LogWarning($"[StatusEffectSystem] Force cleaning up lingering VFX: {obj.name}");
                    Destroy(obj);
                }
                
                // Fallback nivel 2: Global Search (si falló el parenting)
                CleanupOrphanedVFXGlobal(key);
            }
        }

        private System.Collections.IEnumerator VerifyAndCleanupNetworkedVFX(GameObject vfx) {
            yield return new WaitForSeconds(0.5f); // Dar tiempo a FishNet para despawnear
            if (vfx != null) {
                Debug.LogWarning($"[StatusEffectSystem] Networked VFX {vfx.name} persisted after remove signal. Force destroying on client.");
                Destroy(vfx);
            }
        }

        /// <summary>
        /// Busca globalmente (en toda la escena) objetos VFX huérfanos que pertenezcan a este jugador
        /// </summary>
        private void CleanupOrphanedVFXGlobal(int effectID) {
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(effectID);
            if (data == null || data.VFXPrefab == null) return;

            string vfxName = data.VFXPrefab.name;
            
            Debug.Log($"[StatusEffectSystem] Global Cleanup: Searching for objects containing '{vfxName}' in scene...");

            // DEBUG: Usar GameObject para encontrar TODO (incluso lo que no es NetworkObject)
            var allObjects = FindObjectsOfType<GameObject>();

            foreach (var obj in allObjects) {
                if (obj.name.Contains(vfxName)) {
                    NetworkObject netObj = obj.GetComponent<NetworkObject>();
                    int ownerId = netObj != null ? netObj.OwnerId : -1;
                    
                    // Criterio de Propiedad Refinado:
                    // 1. Es explícitamente nuestro (OwnerId == base.OwnerId)
                    // 2. Es Local (netObj == null)
                    // 3. Es del Server (OwnerId == -1) -> Verificar Parentesco
                    
                    bool shouldDestroy = false;
                    string reason = "";

                    if (netObj != null && netObj.OwnerId == base.OwnerId) {
                        shouldDestroy = true; 
                        reason = "Explicit Owner";
                    }
                    else if (netObj == null) {
                        shouldDestroy = true; // Local garbage
                        reason = "Local Object";
                    }
                    else if (ownerId == -1) {
                        // Es del Server. Verificar parentesco para no borrar el de otros.
                        if (obj.transform.parent == transform) {
                            shouldDestroy = true;
                            reason = "Server Owned (My Child)";
                        } else if (obj.transform.parent != null) {
                            // Tiene padre y NO somos nosotros -> Es de otro player. NO TOCAR.
                            shouldDestroy = false;
                            reason = "Belongs to other parent";
                        } else {
                            // No tiene padre (Huérfano en root).
                            // Solo borrar si está MUY cerca (casi pegado).
                            if (Vector3.Distance(obj.transform.position, transform.position) < 2.0f) {
                                shouldDestroy = true;
                                reason = "Server Owned Orphan (Near)";
                            } else {
                                reason = "Server Owned Orphan (Too Far)";
                            }
                        }
                    }

                    if (shouldDestroy) {
                        Debug.LogWarning($"[StatusEffectSystem] DESTROYING GLOBAL ORPHAN: {obj.name}. Reason: {reason}");
                        
                        if (base.IsServer && netObj != null && netObj.IsSpawned) {
                             FishNet.InstanceFinder.ServerManager.Despawn(obj);
                        } else {
                             Destroy(obj);
                        }
                    } else {
                        // Debug.Log($"[StatusEffectSystem] Skipped {obj.name}. Reason: {reason}");
                    }
                }
            }
        }

        void OnDestroy() {
            // Cleanup de todos los VFX
            foreach (var vfx in _vfxInstances.Values) {
                if (vfx != null) Destroy(vfx);
            }
            _vfxInstances.Clear();
        }

        // ═══════════════════════════════════════════════════════
        // DEBUG
        // ═══════════════════════════════════════════════════════

#if UNITY_EDITOR
        [ContextMenu("Debug: List Active Effects")]
        private void DebugListEffects() {
            Debug.Log($"[StatusEffectSystem] Active effects on {gameObject.name}:");
            foreach (var kvp in _activeEffects) {
                StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(kvp.Value.effectID);
                string name = data != null ? data.Name : "Unknown";
                Debug.Log($"  - {name} (ID: {kvp.Key}, Stacks: {kvp.Value.stackCount}, Expires: {kvp.Value.expirationTime - Time.time:F1}s)");
            }

            if (_activeEffects.Count == 0) {
                Debug.Log("  No active effects");
            }
        }

        [ContextMenu("Test: Apply Stun (2s)")]
        private void TestApplyStun() {
            if (!base.IsServer) {
                Debug.LogWarning("Can only apply effects on server!");
                return;
            }
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(1);
            if (data != null) ApplyEffect(data);
        }

        [ContextMenu("Test: Apply Root (3s)")]
        private void TestApplyRoot() {
            if (!base.IsServer) {
                Debug.LogWarning("Can only apply effects on server!");
                return;
            }
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(2);
            if (data != null) ApplyEffect(data);
        }

        [ContextMenu("Test: Apply Slow (5s)")]
        private void TestApplySlow() {
            if (!base.IsServer) {
                Debug.LogWarning("Can only apply effects on server!");
                return;
            }
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(3);
            if (data != null) ApplyEffect(data);
        }

        [ContextMenu("Test: Apply Shield (10s)")]
        private void TestApplyShield() {
            if (!base.IsServer) {
                Debug.LogWarning("Can only apply effects on server!");
                return;
            }
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(10);
            if (data != null) ApplyEffect(data);
        }

        [ContextMenu("Test: Apply Poison (6s)")]
        private void TestApplyPoison() {
            if (!base.IsServer) {
                Debug.LogWarning("Can only apply effects on server!");
                return;
            }
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(20);
            if (data != null) ApplyEffect(data);
        }

        [ContextMenu("Test: Apply Silence (4s)")]
        private void TestApplySilence() {
            if (!base.IsServer) {
                Debug.LogWarning("Can only apply effects on server!");
                return;
            }
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(4);
            if (data != null) ApplyEffect(data);
        }

        [ContextMenu("Test: Apply Reflect (5s)")]
        private void TestApplyReflect() {
            if (!base.IsServer) {
                Debug.LogWarning("Can only apply effects on server!");
                return;
            }
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(11);
            if (data != null) ApplyEffect(data);
        }

        [ContextMenu("Test: Apply Invulnerable (3s)")]
        private void TestApplyInvulnerable() {
            if (!base.IsServer) {
                Debug.LogWarning("Can only apply effects on server!");
                return;
            }
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(12);
            if (data != null) ApplyEffect(data);
        }

        [ContextMenu("Test: Apply Regen (10s)")]
        private void TestApplyRegen() {
            if (!base.IsServer) {
                Debug.LogWarning("Can only apply effects on server!");
                return;
            }
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(21);
            if (data != null) ApplyEffect(data);
        }

        [ContextMenu("Test: Apply Speed (8s)")]
        private void TestApplySpeed() {
            if (!base.IsServer) {
                Debug.LogWarning("Can only apply effects on server!");
                return;
            }
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(30);
            if (data != null) ApplyEffect(data);
        }

        [ContextMenu("Test: Apply Haste (12s)")]
        private void TestApplyHaste() {
            if (!base.IsServer) {
                Debug.LogWarning("Can only apply effects on server!");
                return;
            }
            StatusEffectData data = StatusEffectDatabase.Instance?.GetEffect(31);
            if (data != null) ApplyEffect(data);
        }

        [ContextMenu("Test: Remove All Effects")]
        private void TestRemoveAll() {
            if (!base.IsServer) {
                Debug.LogWarning("Can only remove effects on server!");
                return;
            }

            List<int> toRemove = new List<int>(_activeEffects.Keys);
            foreach (int id in toRemove) {
                RemoveEffect(id);
            }
            Debug.Log("[StatusEffectSystem] All effects removed");
        }
#endif
    }

    // ═══════════════════════════════════════════════════════
    // SERIALIZABLE STRUCT (Para SyncDictionary)
    // ═══════════════════════════════════════════════════════

    [System.Serializable]
    public struct ActiveEffect {
        public int effectID;
        public EffectType type;
        public float expirationTime;
        public int stackCount;
        public float tickInterval;
        public float nextTickTime;

        // Magnitude específica por tipo
        public float percentageValue;  // Para Slow/Haste/Speed
        public float flatValue;         // Para Shield/DoT/HoT
    }
}

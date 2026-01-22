using System;
using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Core {

    /// <summary>
    /// Service Locator Pattern - Registro global de managers/servicios.
    /// Permite desacoplar dependencias sin usar singletons directos.
    /// </summary>
    public class ServiceLocator {

        private static ServiceLocator _instance;
        public static ServiceLocator Instance {
            get {
                if (_instance == null) {
                    _instance = new ServiceLocator();
                }
                return _instance;
            }
        }

        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        // ═══════════════════════════════════════════════════════
        // REGISTRATION
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Registra un servicio. Solo puede haber uno por tipo.
        /// </summary>
        public void Register<T>(T service) where T : class {
            Type type = typeof(T);

            if (_services.ContainsKey(type)) {
                Debug.LogWarning($"[ServiceLocator] Service {type.Name} ya está registrado. Sobrescribiendo.");
            }

            _services[type] = service;
            Debug.Log($"[ServiceLocator] Service registered: {type.Name}");
        }

        /// <summary>
        /// Desregistra un servicio
        /// </summary>
        public void Unregister<T>() where T : class {
            Type type = typeof(T);

            if (_services.ContainsKey(type)) {
                _services.Remove(type);
                Debug.Log($"[ServiceLocator] Service unregistered: {type.Name}");
            }
        }

        // ═══════════════════════════════════════════════════════
        // RETRIEVAL
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Obtiene un servicio registrado. Falla si no existe.
        /// </summary>
        public T Get<T>() where T : class {
            Type type = typeof(T);

            if (_services.TryGetValue(type, out object service)) {
                return service as T;
            }

            Debug.LogError($"[ServiceLocator] Service {type.Name} no está registrado!");
            return null;
        }

        /// <summary>
        /// Intenta obtener un servicio. Retorna false si no existe.
        /// </summary>
        public bool TryGet<T>(out T service) where T : class {
            Type type = typeof(T);

            if (_services.TryGetValue(type, out object obj)) {
                service = obj as T;
                return service != null;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// Verifica si un servicio está registrado
        /// </summary>
        public bool Has<T>() where T : class {
            return _services.ContainsKey(typeof(T));
        }

        // ═══════════════════════════════════════════════════════
        // CLEANUP
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Limpia todos los servicios (útil para testing o scene changes)
        /// </summary>
        public void Clear() {
            _services.Clear();
            Debug.Log("[ServiceLocator] All services cleared");
        }

        /// <summary>
        /// Debug: Lista todos los servicios registrados
        /// </summary>
        public void LogRegisteredServices() {
            Debug.Log($"[ServiceLocator] Registered services ({_services.Count}):");
            foreach (var kvp in _services) {
                Debug.Log($"  - {kvp.Key.Name}");
            }
        }
    }
}

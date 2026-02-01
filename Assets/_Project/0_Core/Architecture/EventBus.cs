using System;
using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Core {

    /// <summary>
    /// Event Bus Pattern - Pub/Sub para desacoplar comunicación entre sistemas.
    /// Evita dependencias directas entre componentes dispares.
    /// </summary>
    public static class EventBus {
        private static readonly Dictionary<string, Delegate> _eventTable = new Dictionary<string, Delegate>();

        // ═══════════════════════════════════════════════════════
        // SUBSCRIPTION (Sin parámetros)
        // ═══════════════════════════════════════════════════════

        public static void Subscribe(string eventName, Action handler) {
            if (!_eventTable.ContainsKey(eventName)) {
                _eventTable[eventName] = null;
            }

            _eventTable[eventName] = (Action)_eventTable[eventName] + handler;
        }

        public static void Unsubscribe(string eventName, Action handler) {
            if (_eventTable.ContainsKey(eventName)) {
                _eventTable[eventName] = (Action)_eventTable[eventName] - handler;

                if (_eventTable[eventName] == null) {
                    _eventTable.Remove(eventName);
                }
            }
        }

        public static void Trigger(string eventName) {
            if (_eventTable.TryGetValue(eventName, out Delegate d)) {
                Action callback = d as Action;
                callback?.Invoke();
            }
        }

        // ═══════════════════════════════════════════════════════
        // SUBSCRIPTION (1 Parámetro)
        // ═══════════════════════════════════════════════════════

        public static void Subscribe<T>(string eventName, Action<T> handler) {
            if (!_eventTable.ContainsKey(eventName)) {
                _eventTable[eventName] = null;
            }

            _eventTable[eventName] = (Action<T>)_eventTable[eventName] + handler;
        }

        public static void Unsubscribe<T>(string eventName, Action<T> handler) {
            if (_eventTable.ContainsKey(eventName)) {
                _eventTable[eventName] = (Action<T>)_eventTable[eventName] - handler;

                if (_eventTable[eventName] == null) {
                    _eventTable.Remove(eventName);
                }
            }
        }

        public static void Trigger<T>(string eventName, T arg) {
            if (_eventTable.TryGetValue(eventName, out Delegate d)) {
                Action<T> callback = d as Action<T>;
                callback?.Invoke(arg);
            }
        }

        // ═══════════════════════════════════════════════════════
        // SUBSCRIPTION (2 Parámetros)
        // ═══════════════════════════════════════════════════════

        public static void Subscribe<T1, T2>(string eventName, Action<T1, T2> handler) {
            if (!_eventTable.ContainsKey(eventName)) {
                _eventTable[eventName] = null;
            }

            _eventTable[eventName] = (Action<T1, T2>)_eventTable[eventName] + handler;
        }

        public static void Unsubscribe<T1, T2>(string eventName, Action<T1, T2> handler) {
            if (_eventTable.ContainsKey(eventName)) {
                _eventTable[eventName] = (Action<T1, T2>)_eventTable[eventName] - handler;

                if (_eventTable[eventName] == null) {
                    _eventTable.Remove(eventName);
                }
            }
        }

        public static void Trigger<T1, T2>(string eventName, T1 arg1, T2 arg2) {
            if (_eventTable.TryGetValue(eventName, out Delegate d)) {
                Action<T1, T2> callback = d as Action<T1, T2>;
                callback?.Invoke(arg1, arg2);
            }
        }

        // ═══════════════════════════════════════════════════════
        // SUBSCRIPTION (3 Parámetros)
        // ═══════════════════════════════════════════════════════

        public static void Subscribe<T1, T2, T3>(string eventName, Action<T1, T2, T3> handler) {
            if (!_eventTable.ContainsKey(eventName)) {
                _eventTable[eventName] = null;
            }

            _eventTable[eventName] = (Action<T1, T2, T3>)_eventTable[eventName] + handler;
        }

        public static void Unsubscribe<T1, T2, T3>(string eventName, Action<T1, T2, T3> handler) {
            if (_eventTable.ContainsKey(eventName)) {
                _eventTable[eventName] = (Action<T1, T2, T3>)_eventTable[eventName] - handler;

                if (_eventTable[eventName] == null) {
                    _eventTable.Remove(eventName);
                }
            }
        }

        public static void Trigger<T1, T2, T3>(string eventName, T1 arg1, T2 arg2, T3 arg3) {
            if (_eventTable.TryGetValue(eventName, out Delegate d)) {
                Action<T1, T2, T3> callback = d as Action<T1, T2, T3>;
                callback?.Invoke(arg1, arg2, arg3);
            }
        }

        // ═══════════════════════════════════════════════════════
        // SUBSCRIPTION (4 Parámetros)
        // ═══════════════════════════════════════════════════════

        public static void Subscribe<T1, T2, T3, T4>(string eventName, Action<T1, T2, T3, T4> handler) {
            if (!_eventTable.ContainsKey(eventName)) {
                _eventTable[eventName] = null;
            }

            _eventTable[eventName] = (Action<T1, T2, T3, T4>)_eventTable[eventName] + handler;
        }

        public static void Unsubscribe<T1, T2, T3, T4>(string eventName, Action<T1, T2, T3, T4> handler) {
            if (_eventTable.ContainsKey(eventName)) {
                _eventTable[eventName] = (Action<T1, T2, T3, T4>)_eventTable[eventName] - handler;

                if (_eventTable[eventName] == null) {
                    _eventTable.Remove(eventName);
                }
            }
        }

        public static void Trigger<T1, T2, T3, T4>(string eventName, T1 arg1, T2 arg2, T3 arg3, T4 arg4) {
            if (_eventTable.TryGetValue(eventName, out Delegate d)) {
                Action<T1, T2, T3, T4> callback = d as Action<T1, T2, T3, T4>;
                callback?.Invoke(arg1, arg2, arg3, arg4);
            }
        }

        // ═══════════════════════════════════════════════════════
        // SUBSCRIPTION (5 Parámetros)
        // ═══════════════════════════════════════════════════════

        public static void Subscribe<T1, T2, T3, T4, T5>(string eventName, Action<T1, T2, T3, T4, T5> handler) {
            if (!_eventTable.ContainsKey(eventName)) {
                _eventTable[eventName] = null;
            }

            _eventTable[eventName] = (Action<T1, T2, T3, T4, T5>)_eventTable[eventName] + handler;
        }

        public static void Unsubscribe<T1, T2, T3, T4, T5>(string eventName, Action<T1, T2, T3, T4, T5> handler) {
            if (_eventTable.ContainsKey(eventName)) {
                _eventTable[eventName] = (Action<T1, T2, T3, T4, T5>)_eventTable[eventName] - handler;

                if (_eventTable[eventName] == null) {
                    _eventTable.Remove(eventName);
                }
            }
        }

        public static void Trigger<T1, T2, T3, T4, T5>(string eventName, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5) {
            if (_eventTable.TryGetValue(eventName, out Delegate d)) {
                Action<T1, T2, T3, T4, T5> callback = d as Action<T1, T2, T3, T4, T5>;
                callback?.Invoke(arg1, arg2, arg3, arg4, arg5);
            }
        }

        // ═══════════════════════════════════════════════════════
        // CLEANUP
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Limpia todos los eventos. Útil para testing o cambio de escenas.
        /// </summary>
        public static void Clear() {
            _eventTable.Clear();
            Debug.Log("[EventBus] All events cleared");
        }

        /// <summary>
        /// Debug: Lista todos los eventos registrados
        /// </summary>
        public static void LogRegisteredEvents() {
            Debug.Log($"[EventBus] Registered events ({_eventTable.Count}):");
            foreach (var kvp in _eventTable) {
                int subscriberCount = kvp.Value?.GetInvocationList().Length ?? 0;
                Debug.Log($"  - {kvp.Key} ({subscriberCount} subscribers)");
            }
        }
    }
}

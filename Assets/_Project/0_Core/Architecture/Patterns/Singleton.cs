using UnityEngine;

namespace Genesis.Core {

    /// <summary>
    /// Singleton pattern genérico para MonoBehaviours.
    /// NO usar DontDestroyOnLoad aquí - se maneja en Bootstrap scene.
    /// </summary>
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour {

        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        public static T Instance {
            get {
                if (_applicationIsQuitting) {
                    Debug.LogWarning($"[Singleton] Instance '{typeof(T)}' ya fue destruida. Retornando null.");
                    return null;
                }

                lock (_lock) {
                    if (_instance == null) {
                        _instance = FindFirstObjectByType<T>();

                        if (_instance == null) {
                            Debug.LogError($"[Singleton] No se encontró una instancia de '{typeof(T)}' en la escena.");
                        }
                    }

                    return _instance;
                }
            }
        }

        protected virtual void Awake() {
            if (_instance == null) {
                _instance = this as T;
            } else if (_instance != this) {
                Debug.LogWarning($"[Singleton] Instancia duplicada de '{typeof(T)}' detectada. Destruyendo.");
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy() {
            if (_instance == this) {
                _instance = null;
            }
        }

        protected virtual void OnApplicationQuit() {
            _applicationIsQuitting = true;
        }
    }
}

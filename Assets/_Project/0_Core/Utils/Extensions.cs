using UnityEngine;

namespace Genesis.Core {

    /// <summary>
    /// Extension methods útiles para tipos comunes de Unity
    /// </summary>
    public static class Extensions {

        // ═══════════════════════════════════════════════════════
        // VECTOR3 EXTENSIONS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Retorna el vector sin componente Y (proyección en plano XZ)
        /// </summary>
        public static Vector3 Flat(this Vector3 v) {
            return new Vector3(v.x, 0, v.z);
        }

        /// <summary>
        /// Retorna el vector con Y modificado
        /// </summary>
        public static Vector3 WithY(this Vector3 v, float y) {
            return new Vector3(v.x, y, v.z);
        }

        /// <summary>
        /// Distancia horizontal (XZ) a otro vector
        /// </summary>
        public static float HorizontalDistanceTo(this Vector3 from, Vector3 to) {
            return MathUtils.HorizontalDistance(from, to);
        }

        // ═══════════════════════════════════════════════════════
        // TRANSFORM EXTENSIONS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Resetea posición, rotación y escala local
        /// </summary>
        public static void ResetLocal(this Transform t) {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        /// <summary>
        /// Encuentra un hijo de forma recursiva por nombre
        /// </summary>
        public static Transform FindDeep(this Transform parent, string name) {
            Transform result = parent.Find(name);
            if (result != null) return result;

            foreach (Transform child in parent) {
                result = child.FindDeep(name);
                if (result != null) return result;
            }

            return null;
        }

        /// <summary>
        /// Destruye todos los hijos de un Transform
        /// </summary>
        public static void DestroyChildren(this Transform t) {
            for (int i = t.childCount - 1; i >= 0; i--) {
                Object.Destroy(t.GetChild(i).gameObject);
            }
        }

        // ═══════════════════════════════════════════════════════
        // GAMEOBJECT EXTENSIONS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// GetComponent que no falla (retorna default si no existe)
        /// </summary>
        public static T GetComponentSafe<T>(this GameObject obj) where T : Component {
            return obj.GetComponent<T>();
        }

        /// <summary>
        /// Obtiene o agrega un componente
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component {
            T component = obj.GetComponent<T>();
            if (component == null) {
                component = obj.AddComponent<T>();
            }
            return component;
        }

        /// <summary>
        /// Verifica si el GameObject está en un layer específico
        /// </summary>
        public static bool IsInLayer(this GameObject obj, int layer) {
            return obj.layer == layer;
        }

        /// <summary>
        /// Cambia el layer de forma recursiva (útil para prefabs)
        /// </summary>
        public static void SetLayerRecursive(this GameObject obj, int layer) {
            obj.layer = layer;
            foreach (Transform child in obj.transform) {
                child.gameObject.SetLayerRecursive(layer);
            }
        }

        // ═══════════════════════════════════════════════════════
        // MONOBEHAVIOUR EXTENSIONS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Invoca un método después de un delay (wrapper para Invoke)
        /// </summary>
        public static void DelayedCall(this MonoBehaviour mono, System.Action action, float delay) {
            mono.StartCoroutine(DelayedCallCoroutine(action, delay));
        }

        private static System.Collections.IEnumerator DelayedCallCoroutine(System.Action action, float delay) {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }
    }
}

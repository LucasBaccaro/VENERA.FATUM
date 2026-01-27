using UnityEngine;

namespace Genesis.Data {
    /// <summary>
    /// Datos para disparar un texto flotante a través del EventBus.
    /// </summary>
    public struct FloatingTextData {
        public Vector3 position;
        public string text;
        public string type;
        public bool isCritical;

        public FloatingTextData(Vector3 position, string text, string type, bool isCritical = false) {
            this.position = position;
            this.text = text;
            this.type = type;
            this.isCritical = isCritical;
        }
    }
}

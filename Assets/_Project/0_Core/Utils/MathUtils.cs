using UnityEngine;

namespace Genesis.Core {

    /// <summary>
    /// Utilidades matemáticas comunes para el proyecto
    /// </summary>
    public static class MathUtils {

        /// <summary>
        /// Calcula distancia horizontal (XZ) entre dos puntos, ignorando Y
        /// </summary>
        public static float HorizontalDistance(Vector3 a, Vector3 b) {
            Vector3 aFlat = new Vector3(a.x, 0, a.z);
            Vector3 bFlat = new Vector3(b.x, 0, b.z);
            return Vector3.Distance(aFlat, bFlat);
        }

        /// <summary>
        /// Calcula el ángulo horizontal entre dos puntos (útil para targeting)
        /// </summary>
        public static float HorizontalAngle(Vector3 from, Vector3 to) {
            Vector3 direction = to - from;
            direction.y = 0;
            return Vector3.Angle(Vector3.forward, direction);
        }

        /// <summary>
        /// Verifica si un punto está dentro de un rango en el plano XZ
        /// </summary>
        public static bool IsInRange(Vector3 origin, Vector3 target, float range) {
            return HorizontalDistance(origin, target) <= range;
        }

        /// <summary>
        /// Interpola suavemente entre dos valores con smoothing
        /// </summary>
        public static float SmoothLerp(float from, float to, float speed, float deltaTime) {
            return Mathf.Lerp(from, to, 1f - Mathf.Exp(-speed * deltaTime));
        }

        /// <summary>
        /// Clampea un ángulo entre -180 y 180
        /// </summary>
        public static float ClampAngle(float angle) {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }

        /// <summary>
        /// Genera un punto aleatorio dentro de un círculo (útil para spawns)
        /// </summary>
        public static Vector3 RandomPointInCircle(Vector3 center, float radius) {
            Vector2 randomPoint = Random.insideUnitCircle * radius;
            return center + new Vector3(randomPoint.x, 0, randomPoint.y);
        }

        /// <summary>
        /// Verifica si hay línea de visión entre dos puntos
        /// </summary>
        public static bool HasLineOfSight(Vector3 from, Vector3 to, int layerMask) {
            Vector3 direction = to - from;
            float distance = direction.magnitude;

            return !Physics.Raycast(from, direction.normalized, distance, layerMask);
        }
    }
}

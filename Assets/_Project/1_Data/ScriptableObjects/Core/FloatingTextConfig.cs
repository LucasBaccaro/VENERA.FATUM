using UnityEngine;
using TMPro;

namespace Genesis.Data {
    public enum FCTAnimationMode { Vertical, Arc }

    /// <summary>
    /// Configuración visual para los números de daño flotantes (FCT).
    /// Permite editar la apariencia sin tocar código.
    /// </summary>
    [CreateAssetMenu(fileName = "FloatingTextConfig", menuName = "Genesis/System/Floating Text Config")]
    public class FloatingTextConfig : ScriptableObject {
        [Header("Animation Mode")]
        public FCTAnimationMode animationMode = FCTAnimationMode.Arc;

        [Header("Colors")]
        public Color damageColor = Color.red;
        public Color healColor = Color.green;
        public Color shieldColor = new Color(0.5f, 0.8f, 1f);
        public Color criticalColor = Color.yellow;
        public Color manaColor = Color.cyan;

        [Header("General Animation")]
        public float duration = 1.5f;
        public float floatSpeed = 1.2f; // Para modo Vertical
        public Vector2 randomOffsetRange = new Vector2(0.3f, 0.3f);
        
        [Header("Arc Animation Parameters")]
        public Vector2 horizontalVelocityRange = new Vector2(1f, 2f);
        public float upwardForce = 3f;
        public float gravity = 9.8f;

        [Header("Sorting & Depth")]
        public string sortingLayerName = "Default";
        public int sortingOrder = 100;

        [Header("Curves")]
        public AnimationCurve scaleCurve = AnimationCurve.Linear(0, 0.5f, 1, 1.2f);
        public AnimationCurve alphaCurve = AnimationCurve.Linear(0, 1, 1, 0);

        [Header("Typography")]
        public TMP_FontAsset fontAsset;
        public int fontSize = 24;
        public float criticalScaleMultiplier = 1.5f;
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace Genesis.Simulation.World
{
    /// <summary>
    /// Attach this to a LOOKS prefab to define chunk-specific environment settings.
    /// ChunkLoaderManager calls Apply() when instantiating this prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public class LooksSettings : MonoBehaviour
    {
        [Header("Skybox")]
        [Tooltip("The skybox material for this environment. Leave null to keep the current skybox.")]
        public Material Skybox;

        [Header("Ambient Light")]
        public AmbientMode AmbientMode = AmbientMode.Flat;
        [ColorUsage(false, true)]
        public Color AmbientColor = new Color(0.2f, 0.2f, 0.2f);
        [Tooltip("Used when AmbientMode is Trilight.")]
        [ColorUsage(false, true)]
        public Color AmbientEquatorColor = new Color(0.2f, 0.2f, 0.2f);
        [ColorUsage(false, true)]
        public Color AmbientGroundColor = new Color(0.1f, 0.1f, 0.1f);
        [Range(0f, 8f)]
        public float AmbientIntensity = 1f;

        [Header("Fog")]
        public bool FogEnabled = false;
        public FogMode FogMode = FogMode.ExponentialSquared;
        public Color FogColor = new Color(0.5f, 0.5f, 0.5f);
        [Tooltip("Used for Exponential fog modes.")]
        public float FogDensity = 0.01f;
        [Tooltip("Used for Linear fog mode.")]
        public float FogStartDistance = 20f;
        public float FogEndDistance = 200f;

        [Header("Reflection")]
        [Tooltip("Global reflection intensity multiplier.")]
        [Range(0f, 1f)]
        public float ReflectionIntensity = 1f;

        /// <summary>
        /// Applies these settings to Unity's global RenderSettings.
        /// Called automatically by ChunkLoaderManager after instantiation.
        /// </summary>
        public void Apply()
        {
            // Skybox
            if (Skybox != null)
                RenderSettings.skybox = Skybox;

            // Ambient
            RenderSettings.ambientMode = AmbientMode;
            RenderSettings.ambientIntensity = AmbientIntensity;
            switch (AmbientMode)
            {
                case AmbientMode.Flat:
                    RenderSettings.ambientLight = AmbientColor;
                    break;
                case AmbientMode.Trilight:
                    RenderSettings.ambientSkyColor   = AmbientColor;
                    RenderSettings.ambientEquatorColor = AmbientEquatorColor;
                    RenderSettings.ambientGroundColor  = AmbientGroundColor;
                    break;
                // AmbientMode.Skybox uses the skybox material automatically
            }

            // Fog
            RenderSettings.fog        = FogEnabled;
            RenderSettings.fogMode    = FogMode;
            RenderSettings.fogColor   = FogColor;
            RenderSettings.fogDensity = FogDensity;
            RenderSettings.fogStartDistance = FogStartDistance;
            RenderSettings.fogEndDistance   = FogEndDistance;

            // Reflections
            RenderSettings.reflectionIntensity = ReflectionIntensity;

            // Recalculate GI with the new ambient settings
            DynamicGI.UpdateEnvironment();

            Debug.Log($"[LooksSettings] Applied environment settings from '{gameObject.name}'.");
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;

namespace Genesis.Presentation.RenderFeatures
{
    public class OutlineRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class OutlineSettings
        {
            public LayerMask layerMask;
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            public Material outlineMaterial;
            [ColorUsage(false, false)]
            public Color outlineColor = Color.yellow;
            [Range(0f, 10f)] public float outlineWidth = 2.0f;
            public bool debugShowMask = false;
        }

        public OutlineSettings settings = new OutlineSettings();

        // ── Per-pass data structs ─────────────────────────────────────────────

        private class MaskPassData
        {
            public RendererListHandle rendererList;
        }

        private class OverlayPassData
        {
            public TextureHandle     maskTexture;
            public Material          outlineMaterial;
            public bool              debugShowMask;
        }

        // ─────────────────────────────────────────────────────────────────────

        class OutlinePass : ScriptableRenderPass
        {
            private OutlineSettings _settings;
            private FilteringSettings _filteringSettings;
            private readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();
            private Material _maskMaterial; // renders everything solid-white for a clean binary mask

            public OutlinePass(OutlineSettings settings)
            {
                _settings = settings;
                _filteringSettings = new FilteringSettings(RenderQueueRange.all, settings.layerMask);

                _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
                _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
                _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
                _shaderTagIds.Add(new ShaderTagId("LightweightForward"));

                // Create the override material for the mask pass
                var maskShader = Shader.Find("Hidden/Genesis/OutlineMaskObject");
                if (maskShader != null)
                    _maskMaterial = new Material(maskShader) { hideFlags = HideFlags.HideAndDontSave };
                else
                    Debug.LogWarning("[OutlineRendererFeature] Could not find 'Hidden/Genesis/OutlineMaskObject' shader!");
            }

            // ── Unity 6 / URP 17+ Native Render Graph ────────────────────────
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                // Rebuild every frame so Inspector changes are respected
                _filteringSettings = new FilteringSettings(RenderQueueRange.all, _settings.layerMask);

                Material mat = _settings.outlineMaterial;
                if (mat == null) return;

                var resourceData  = frameData.Get<UniversalResourceData>();
                var cameraData    = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();

                var cameraTarget = resourceData.activeColorTexture;
                var rtd          = cameraData.cameraTargetDescriptor;

                // ── PASS 1: Draw interactable objects (layer mask) into R8 mask ──
                var maskDesc = new TextureDesc(rtd.width, rtd.height)
                {
                    name            = "_OutlineMask",
                    colorFormat     = GraphicsFormat.R8_UNorm,
                    depthBufferBits = 0,
                    msaaSamples     = MSAASamples.None,
                    filterMode      = FilterMode.Bilinear,
                    wrapMode        = TextureWrapMode.Clamp,
                };
                TextureHandle maskTexture = renderGraph.CreateTexture(maskDesc);

                var sortingSettings = new SortingSettings(cameraData.camera)
                    { criteria = SortingCriteria.CommonOpaque };
                var drawingSettings = new DrawingSettings(_shaderTagIds[0], sortingSettings);
                for (int i = 1; i < _shaderTagIds.Count; i++)
                    drawingSettings.SetShaderPassName(i, _shaderTagIds[i]);

                // Override every object's material with solid-white → clean binary mask
                if (_maskMaterial != null)
                {
                    drawingSettings.overrideMaterial          = _maskMaterial;
                    drawingSettings.overrideMaterialPassIndex = 0;
                }

                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("Outline Mask", out var pd))
                {
                    pd.rendererList = renderGraph.CreateRendererList(
                        new RendererListParams(renderingData.cullResults, drawingSettings, _filteringSettings));

                    builder.SetRenderAttachment(maskTexture, 0);
                    builder.UseRendererList(pd.rendererList);

                    builder.SetRenderFunc((MaskPassData data, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.ClearRenderTarget(false, true, Color.clear);
                        ctx.cmd.DrawRendererList(data.rendererList);
                    });
                }

                // ── Set material properties on CPU before recording the pass ──
                mat.SetColor ("_OutlineColor", _settings.outlineColor);
                mat.SetFloat ("_OutlineWidth", _settings.outlineWidth);
                mat.SetFloat ("_Threshold",    0.5f); // binary mask: solid white vs empty

                // ── PASS 2: Blit mask → outline overlay on top of camera ───────
                // The shader is alpha-blended: outline pixels are opaque, rest = transparent.
                // We blit maskTexture AS the source (_BlitTexture inside shader).
                // No screen copy needed — we write OVER the camera using alpha blend.
                using (var builder = renderGraph.AddRasterRenderPass<OverlayPassData>("Outline Overlay", out var pd))
                {
                    pd.maskTexture     = maskTexture;
                    pd.outlineMaterial = mat;
                    pd.debugShowMask   = _settings.debugShowMask;

                    builder.SetRenderAttachment(cameraTarget, 0);
                    builder.UseTexture(maskTexture);

                    builder.SetRenderFunc((OverlayPassData data, RasterGraphContext ctx) =>
                    {
                        if (data.debugShowMask)
                        {
                            // White silhouettes — raw mask visualisation
                            Blitter.BlitTexture(ctx.cmd, data.maskTexture,
                                new Vector4(1, 1, 0, 0), 0, false);
                            return;
                        }

                        // _BlitTexture = maskTexture.
                        // Shader checks neighborhood, outputs outline or transparent.
                        Blitter.BlitTexture(ctx.cmd, data.maskTexture,
                            new Vector4(1, 1, 0, 0), data.outlineMaterial, 0);
                    });
                }
            }

            // ── Legacy Execute (not called when RecordRenderGraph is present) ─
            [System.Obsolete]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // RecordRenderGraph runs instead in Unity 6.
            }

            public override void OnCameraCleanup(CommandBuffer cmd) { }
        }

        private OutlinePass _outlinePass;

        public override void Create()
        {
            _outlinePass = new OutlinePass(settings);
            _outlinePass.renderPassEvent = settings.renderPassEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.outlineMaterial == null) return;
            renderer.EnqueuePass(_outlinePass);
        }

        protected override void Dispose(bool disposing)
        {
            _outlinePass = null;
        }
    }
}

Shader "Hidden/Genesis/InteractableOutline"
{
    Properties
    {
        // _BlitTexture is set automatically by Blitter (it will be the mask texture)
        _OutlineColor ("Outline Color", Color) = (1, 1, 0, 1)
        _OutlineWidth ("Outline Width", Float) = 2.0
        _Threshold    ("Threshold",    Float) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off
        // Outline pixels paint on top; transparent pixels leave the scene untouched.
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "OutlinePass"

            HLSLPROGRAM
            #pragma vertex   Vert   // provided by Blit.hlsl
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Blit.hlsl defines: Attributes, Varyings, Vert(), _BlitTexture, _BlitTexture_TexelSize
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _OutlineColor;
            float  _OutlineWidth;
            float  _Threshold;

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;   // Blit.hlsl names it texcoord

                // _BlitTexture = the interactable-mask (R8_UNorm), set by Blitter
                float center = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).r;

                // Pixels INSIDE the object: transparent — no outline inside
                if (center > _Threshold)
                    return float4(0, 0, 0, 0);

                // 8-directional neighbourhood check
                float2 ts  = _BlitTexture_TexelSize.xy * _OutlineWidth;
                float edge = 0;
                edge += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( ts.x,    0)).r;
                edge += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-ts.x,    0)).r;
                edge += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(   0,  ts.y)).r;
                edge += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(   0, -ts.y)).r;
                edge += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( ts.x,  ts.y)).r;
                edge += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-ts.x, -ts.y)).r;
                edge += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( ts.x, -ts.y)).r;
                edge += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-ts.x,  ts.y)).r;

                // Border pixel → draw the outline colour
                if (edge > 0.01)
                    return float4(_OutlineColor.rgb, _OutlineColor.a);

                // Not on a border → fully transparent
                return float4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}

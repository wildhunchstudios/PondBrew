Shader "EvolutionShop/Cartoon Particle"
{
    Properties { _Tint("Tint and lifetime fade", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest LEqual
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _Tint;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
        struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; };
        Varyings vert(Attributes i)
        {
            Varyings o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
            o.color = i.color * _Tint; return o;
        }
        ENDHLSL
        Pass
        {
            Name "Cartoon"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(Varyings i) : SV_Target { return i.color; }
            ENDHLSL
        }
        // Used only by the development raster audit: actual particle fragments,
        // additive float target, opaque/HUD occlusion deliberately excluded.
        Pass
        {
            Name "ParticleCoverageAudit"
            Tags { "LightMode"="EvolutionCoverageAudit" }
            Blend One One ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment coverage
            float4 coverage(Varyings i) : SV_Target { clip(i.color.a - .01); return 1; }
            ENDHLSL
        }
    }
}

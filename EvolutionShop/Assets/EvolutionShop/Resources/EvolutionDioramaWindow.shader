// AD-A Pocket Aquarium Diorama: the street seen through the shop window. A sky gradient with rooftops and trees
// (mask R), a street lamp (mask G, HDR so it blooms at night) and stars (mask B), all coloured by the time of day.
// Original project shader.
Shader "EvolutionShop/Diorama Window"
{
    Properties
    {
        _MainTex("Street mask (R silhouette, G lamp, B stars)", 2D) = "black" {}
        _SkyTop("Sky top", Color) = (0.42,0.72,0.96,1)
        _SkyBottom("Sky bottom", Color) = (0.82,0.93,1,1)
        _Silhouette("Rooftops and trees", Color) = (0.36,0.62,0.55,1)
        [HDR]_LampColor("Street lamp", Color) = (0,0,0,1)
        _StarColor("Stars", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Window"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _SkyTop, _SkyBottom, _Silhouette, _LampColor, _StarColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half4 mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half3 sky = lerp(_SkyBottom.rgb, _SkyTop.rgb, saturate(i.uv.y * 1.15 - .05));
                half3 color = lerp(sky, _Silhouette.rgb, mask.r);
                color += _StarColor.rgb * mask.b * (1 - mask.r);
                color += _LampColor.rgb * mask.g;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

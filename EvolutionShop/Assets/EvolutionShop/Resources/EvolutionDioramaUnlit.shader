// AD-A Pocket Aquarium Diorama: unlit colour for the hand-drawn flipbook particles, light shafts and glows.
// Texture x vertex colour x tint (HDR for bloom); the blend mode is a material property (alpha or additive).
// Original project shader.
Shader "EvolutionShop/Diorama Unlit"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [HDR]_BaseColor("Tint", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Source blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Destination blend", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
                half _SrcBlend, _DstBlend, _Cull;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color * _BaseColor;
                return c;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

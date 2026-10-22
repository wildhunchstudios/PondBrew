// AD-A Pocket Aquarium Diorama: toy glass and stylised water. Fresnel edge, a crisp toon glint, optional HDR glow
// for bloom (night tanks, culture fluid) and an optional wobbling top surface for water blocks.
// Original project shader on URP's lighting includes.
Shader "EvolutionShop/Diorama Glass"
{
    Properties
    {
        _BaseColor("Tint and opacity", Color) = (0.7,0.92,0.96,0.12)
        _RimColor("Edge colour", Color) = (0.86,0.98,1,1)
        _RimStrength("Edge opacity", Range(0,1)) = 0.35
        _GlossStrength("Glint", Range(0,1)) = 0.6
        [HDR]_EmissionColor("Glow", Color) = (0,0,0,1)
        _WobbleAmount("Surface wobble (m)", Range(0,0.05)) = 0
        _WobbleTop("Surface starts at local y", Float) = 0.45
        _FarSide("Far side opacity (times the tint's)", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull", Float) = 0
        // AD-D: the classic colour of a world material the look converts in place (game code reads Material.color).
        [HideInInspector]_Color("Classic colour", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _RimColor, _EmissionColor;
                half _RimStrength, _GlossStrength, _WobbleAmount, _WobbleTop, _FarSide, _Cull;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 p = v.positionOS.xyz;
                if (_WobbleAmount > 0 && p.y > _WobbleTop)
                {
                    float t = _Time.y;
                    p.y += (sin(t * 2.1 + p.x * 9.0) * cos(t * 1.7 + p.z * 7.0) + .5 * sin(t * 3.3 + (p.x + p.z) * 13.0)) * _WobbleAmount;
                }
                o.positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 n = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light mainLight = GetMainLight();
                half fresnel = pow(1 - saturate(abs(dot(n, viewDir))), 3);
                half3 color = _BaseColor.rgb * (SampleSH(n) * .8 + mainLight.color * .35);
                color = lerp(color, _RimColor.rgb, fresnel * .8);
                half3 h = normalize(mainLight.direction + viewDir);
                // A small crisp glint on the near side only: on a big dome a wide one read as a pale blob.
                half front = IS_FRONT_VFACE(face, 1, 0);
                half glint = smoothstep(.993, .997, dot(n, h)) * _GlossStrength * front;
                // A glow (night water, the hatch) shines mostly from the far side, behind the creature, so it lights the
                // container up without veiling what swims in it.
                half3 glow = _EmissionColor.rgb * lerp(1, .3, front);
                color += half3(1, 1, 1) * glint + glow;
                // Water can carry its tint on the far side (behind what swims in it) more strongly than on the near side.
                half alpha = saturate(_BaseColor.a * lerp(_FarSide, 1, front) + fresnel * _RimStrength + glint * .8 + max(glow.r, max(glow.g, glow.b)) * lerp(.15, .04, front));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

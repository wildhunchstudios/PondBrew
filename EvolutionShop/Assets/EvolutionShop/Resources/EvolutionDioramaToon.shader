// AD-A Pocket Aquarium Diorama: two-step soft toon lighting with a rim light, an optional thin outline
// (creatures and characters only), optional moving caustics (tank floors and walls) and HDR emission for bloom.
// Original project shader; built on URP's own lighting includes, no other source.
Shader "EvolutionShop/Diorama Toon"
{
    Properties
    {
        _BaseColor("Base colour", Color) = (1,1,1,1)
        _ShadeColor("Shade tint (multiplies the key light on the shaded side)", Color) = (0.52,0.5,0.66,1)
        _ShadeStep("Shade step", Range(-1,1)) = 0.02
        _ShadeSoftness("Shade softness", Range(0.001,0.5)) = 0.05
        _AmbientStrength("Ambient strength", Range(0,2)) = 0.85
        _RimColor("Rim colour", Color) = (1,0.95,0.84,1)
        _RimStrength("Rim strength", Range(0,1)) = 0.28
        _RimWidth("Rim width", Range(0.01,1)) = 0.28
        _GlossStrength("Toon highlight", Range(0,1)) = 0
        [HDR]_EmissionColor("Emission", Color) = (0,0,0,1)
        _CausticStrength("Caustics", Range(0,2)) = 0
        _CausticScale("Caustic scale", Float) = 3.4
        _OutlineColor("Outline colour", Color) = (0.07,0.17,0.17,1)
        _OutlineWidth("Outline width (pixels)", Range(0,6)) = 0
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "UniversalMaterialType"="Unlit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _ShadeColor, _RimColor, _EmissionColor, _OutlineColor;
            half _ShadeStep, _ShadeSoftness, _AmbientStrength, _RimStrength, _RimWidth, _GlossStrength;
            half _CausticStrength, _CausticScale, _OutlineWidth, _Cull;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half CausticLines(float2 p, float t)
            {
                float a = sin(p.x * 1.7 + t * 1.1 + sin(p.y * 1.3 + t * .7) * 1.2);
                float b = sin(p.y * 1.9 - t * .9 + sin(p.x * 1.1 - t * .6) * 1.3);
                float c = sin((p.x + p.y) * 1.3 + t * .8);
                float web = abs(a + b + c) / 3;
                return pow(saturate(1 - web * 2.4), 5);
            }

            half3 ToonAdditional(half3 n, Light light)
            {
                half ndl = dot(n, light.direction);
                half band = smoothstep(-.05, .25, ndl);
                return light.color * (light.distanceAttenuation * light.shadowAttenuation * band);
            }

            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 n = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                float3 positionWS = i.positionWS;
                half3 viewDir = GetWorldSpaceNormalizeViewDir(positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalWS = n;
                inputData.normalizedScreenSpaceUV = screenUV;
                half directAo = 1, indirectAo = 1;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(screenUV);
                    directAo = ao.directAmbientOcclusion; indirectAo = ao.indirectAmbientOcclusion;
                #endif
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS), positionWS, half4(1, 1, 1, 1));
                half ndl = dot(n, mainLight.direction);
                half lit = smoothstep(_ShadeStep - _ShadeSoftness, _ShadeStep + _ShadeSoftness, ndl);
                lit *= smoothstep(.3, .7, mainLight.shadowAttenuation);
                half3 albedo = _BaseColor.rgb;
                half3 light = SampleSH(n) * (_AmbientStrength * indirectAo);
                light += mainLight.color * lerp(_ShadeColor.rgb, half3(1, 1, 1), lit) * directAo;

                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        light += ToonAdditional(n, GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1)));
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(lightCount)
                        light += ToonAdditional(n, GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1)));
                    LIGHT_LOOP_END
                #endif

                half3 color = albedo * light;
                half keyLuma = saturate(dot(mainLight.color, half3(.3, .59, .11)));
                half rim = smoothstep(1 - _RimWidth, 1 - _RimWidth + .08, 1 - saturate(dot(n, viewDir)));
                color += _RimColor.rgb * rim * _RimStrength * (.45 + .55 * lit) * (.4 + .6 * keyLuma);
                half3 h = normalize(mainLight.direction + viewDir);
                color += mainLight.color * smoothstep(.986, .993, dot(n, h)) * _GlossStrength * lit;
                if (_CausticStrength > 0)
                {
                    float t = _Time.y;
                    float2 p = float2(positionWS.x + positionWS.y * .6, positionWS.z + positionWS.y * .6) * _CausticScale;
                    half caustic = max(CausticLines(p, t), CausticLines(p * 1.37 + 3.1, t * 1.21) * .7);
                    color += half3(.72, .95, 1) * caustic * _CausticStrength * (.35 + .65 * keyLuma);
                }
                // A soft knee on reflected light: a surface under several lamps keeps its hue and never passes the
                // bloom threshold, so only emission (lamps, glows, the hatch) blooms and labels stay readable.
                half peak = max(color.r, max(color.g, color.b));
                if (peak > .85) color *= (.85 + .13 * (1 - exp(-(peak - .85) / .13))) / peak;
                color += _EmissionColor.rgb;
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float4 positionCS = TransformObjectToHClip(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                float2 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
                float len = length(normalCS);
                // A constant pixel width at any distance, so far creatures still read as drawn.
                float2 offset = len > 1e-5 ? normalCS / len : float2(0, 0);
                positionCS.xy += offset * (_OutlineWidth * positionCS.w) * (2.0 / _ScreenParams.xy);
                o.positionCS = _OutlineWidth > 0 ? positionCS : float4(0, 0, 0, 0);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target { return half4(_OutlineColor.rgb, 1); }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                return o;
            }
            half4 Frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes v) { Varyings o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half Frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}

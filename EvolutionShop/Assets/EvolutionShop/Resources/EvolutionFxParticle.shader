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
        struct Attributes { float4 positionOS : POSITION; float3 uv : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; float3 uv : TEXCOORD0; };
        Varyings vert(Attributes i)
        {
            Varyings o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o;
        }
        float disc(float2 p, float radius, float feather) { return 1 - smoothstep(radius - feather, radius, length(p)); }
        half4 particle(Varyings i)
        {
            float2 p = i.uv.xy; float r = length(p); float shape = i.uv.z;
            float edge = 1 - smoothstep(.94, .99, r);
            float alpha = 0; float3 color = _Tint.rgb;
            if (shape < .5) // glassy bubble: thin rim, transparent body, asymmetric reflected glints
            {
                float rim = smoothstep(.76, .89, r) * (1 - smoothstep(.92, .98, r));
                float shade = saturate(dot(p, float2(-.55, .83)));
                float highlight = disc((p - float2(-.30, .39)) * float2(1.15, 1.6), .19, .06);
                float small = disc(p - float2(.26, -.53), .07, .035);
                alpha = (.09 + .16 * r * r + rim * (.36 + .25 * shade) + highlight * .82 + small * .45) * edge;
                color = lerp(color * (.73 + .22 * shade), float3(.96,1,1), saturate(rim * shade * .6 + highlight + small));
            }
            else if (shape < 1.5) // rounded liquid tear, shaded instead of a flat icon
            {
                float width = .58 - .25 * saturate(p.y);
                float body = length(float2(p.x / width, p.y * .98));
                alpha = (1 - smoothstep(.8, .99, body)) * edge;
                float shine = disc((p - float2(-.16, .04)) * float2(1.3, .7), .15, .06);
                color = lerp(color * (.74 + .23 * saturate(p.y + .5)), float3(.97,1,1), shine * .9);
            }
            else if (shape < 2.5) // overlapping round cloud lobes, no pointed star silhouette
            {
                float cloud = max(disc(p - float2(-.27,-.07),.53,.20), disc(p - float2(.27,-.1),.50,.20));
                cloud = max(cloud, disc(p - float2(-.03,.26),.56,.20));
                cloud = max(cloud, disc(p - float2(.03,-.30),.39,.18));
                alpha = cloud * .58 * edge;
                color = lerp(color * .78, float3(.95,1,.98), saturate(p.y * .5 + .65));
            }
            else if (shape < 3.5)
            {
                float cross = abs(p.x * p.y) * 19 + length(p) * .45;
                alpha = (1 - smoothstep(.38,.72,cross)) * edge;
                color = lerp(color, float3(1,1,.95), .55);
            }
            else { alpha = smoothstep(.79,.85,r) * (1 - smoothstep(.9,.98,r)); }
            return half4(color, saturate(alpha) * _Tint.a);
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
            half4 frag(Varyings i) : SV_Target { return particle(i); }
            ENDHLSL
        }
        Pass
        {
            Name "ParticleCoverageAudit"
            Tags { "LightMode"="EvolutionCoverageAudit" }
            Blend One One ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment coverage
            float4 coverage(Varyings i) : SV_Target { clip(particle(i).a - .01); return 1; }
            ENDHLSL
        }
    }
}

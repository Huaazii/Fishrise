Shader "Fishing/Water Depth"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [Normal] _FlowTex ("Broad flow normal", 2D) = "bump" {}
        _ShallowColor ("Shallow water", Color) = (.075,.54,.72,1)
        _MidColor ("Middle water", Color) = (.035,.40,.63,1)
        _DeepColor ("Deep water", Color) = (.016,.17,.34,1)
        _LightColor ("Ice opening light", Color) = (.35,.65,.72,1)
        _SurfaceY ("World surface Y", Float) = .35
        _DepthRange ("World water depth", Float) = 100
        _ShallowFade ("Shallow transition distance", Float) = 15
        _FlowStrength ("Flow contrast", Range(0,.3)) = .16
        _LightStrength ("Opening light strength", Range(0,.3)) = .12
        [HideInInspector] _WaterTime ("Animation phase", Float) = 0
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_FlowTex); SAMPLER(sampler_FlowTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _ShallowColor, _MidColor, _DeepColor, _LightColor, _Color, _RendererColor;
            float _SurfaceY, _DepthRange, _ShallowFade, _FlowStrength, _LightStrength, _WaterTime;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 world:TEXCOORD0; float2 uv:TEXCOORD1; half4 color:COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(world); o.world = world.xy; o.uv = v.uv;
                o.color = v.color * _Color * _RendererColor;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                // World depth stays fixed while the orthographic camera moves through the water.
                float depth = max(0, _SurfaceY - i.world.y);
                float deep = saturate(depth / max(1, _DepthRange));
                half3 water = lerp(_ShallowColor.rgb, _MidColor.rgb, 1 - exp2(-depth / max(1, _ShallowFade)));
                water = lerp(water, _DeepColor.rgb, smoothstep(.18, 1, deep));
                float2 uv = i.world * .065;
                half2 a = UnpackNormal(SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, uv + _WaterTime * float2(.007,-.003))).xy;
                half2 b = UnpackNormal(SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, uv * .73 + _WaterTime * float2(-.004,.005))).xy;
                water *= 1 + (a.x + b.y) * _FlowStrength;
                // Soft light spreads from the small ice opening and fades in the upper water column.
                float cone = 1 - smoothstep(.35, 1.1, abs(i.world.x) / (.7 + depth * .16));
                half bands = .65 + .35 * sin(i.world.x * 3 / (1 + depth * .12) + a.y * 2 + _WaterTime * .18);
                water += _LightColor.rgb * _LightStrength * cone * bands * exp2(-depth / 12);
                half ripple = .5 + .5 * sin(i.world.x * 24 + a.x * 3 + _WaterTime * 1.5);
                water += _LightColor.rgb * .055 * ripple * exp2(-depth * 16) * (1 - smoothstep(.4,.85,abs(i.world.x)));
                // Keep a clear central route; the water veil is slightly denser near the ice walls.
                water += half3(.013,.022,.028) * smoothstep(1.8,5.6,abs(i.world.x)) * (.6 + .4 * b.x);
                return half4(water * i.color.rgb, SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a * i.color.a);
            }
            ENDHLSL
        }
    }
}

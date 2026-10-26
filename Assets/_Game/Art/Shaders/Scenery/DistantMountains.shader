Shader "Fishing/Distant Mountains"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mountain artwork", 2D) = "white" {}
        _HazeColor ("Cold atmospheric haze", Color) = (.30,.43,.54,1)
        _HazeStrength ("Distance haze", Range(0,1)) = .48
        _Contrast ("Distant detail contrast", Range(0,1)) = .60
        _BlurRadius ("Fine detail softness", Range(0,4)) = 2.2
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
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_TexelSize, _HazeColor, _Color, _RendererColor;
            float _HazeStrength, _Contrast, _BlurRadius;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; half4 color:COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o; float3 world=TransformObjectToWorld(v.positionOS); o.positionCS=TransformWorldToHClip(world);
                o.uv=v.uv; o.world=world.xy; o.color=v.color*_Color*_RendererColor; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 offset=_MainTex_TexelSize.xy*_BlurRadius;
                half4 art=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*.4;
                art+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(offset.x,0))*.15;
                art+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv-float2(offset.x,0))*.15;
                art+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(0,offset.y))*.15;
                art+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv-float2(0,offset.y))*.15;
                half grey=dot(art.rgb,half3(.2126,.7152,.0722));
                half3 muted=lerp(art.rgb,grey.xxx,.3);
                muted=saturate((muted-.45)*_Contrast+.45);
                half3 distanceColor=lerp(muted,_HazeColor.rgb,_HazeStrength);
                half baseFade=smoothstep(.84,1.04,i.world.y);
                return half4(distanceColor*i.color.rgb,art.a*i.color.a*baseFade);
            }
            ENDHLSL
        }
    }
}

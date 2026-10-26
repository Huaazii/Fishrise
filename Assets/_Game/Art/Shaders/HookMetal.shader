Shader "Fishing/Hook Metal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Original hook art", 2D) = "white" {}
        _Gloss ("Metal highlight", Range(0,1)) = .48
        _SurfaceY ("Water height", Float) = .35
        _DepthRange ("Depth fading distance", Float) = 100
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
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
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
            float _Gloss, _SurfaceY, _DepthRange;
            CBUFFER_END
            float4 _SkyPosition, _LampPosition;
            half4 _SkyColor, _LampColor;
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; half4 color:COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o; float3 world=TransformObjectToWorld(v.positionOS);
                o.positionCS=TransformWorldToHClip(world); o.uv=v.uv; o.world=world.xy; o.color=v.color; return o;
            }
            half3 Light(float2 world,half3 normal,half3 albedo,float4 source,half3 color)
            {
                float2 delta=source.xy-world;
                half fade=pow(saturate(1-length(delta)/max(.01,source.w)),2);
                half3 direction=normalize(half3(delta,max(.1,source.z)));
                half diffuse=.22+.78*saturate(dot(normal,direction));
                half specular=pow(saturate(dot(normal,normalize(direction+half3(0,0,1)))),48)*_Gloss;
                return color*fade*(albedo*diffuse+half3(.9,.94,1)*specular);
            }
            half4 Frag(Varyings i):SV_Target
            {
                half4 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                float2 step=_MainTex_TexelSize.xy*1.5;
                // Alpha contours provide a small bevel; the original painted detail is retained, not treated as a height map.
                half2 gradient=half2(
                    SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv-float2(step.x,0)).a-SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(step.x,0)).a,
                    SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv-float2(0,step.y)).a-SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(0,step.y)).a);
                half3 normal=normalize(TransformObjectToWorldDir(half3(gradient*1.4,1)));
                half depth=smoothstep(0,max(1,_DepthRange),max(0,_SurfaceY-i.world.y));
                half3 albedo=tex.rgb*lerp(half3(1,1,1),half3(.66,.82,.92),depth);
                half3 color=albedo*lerp(.8,.48,depth);
                color+=Light(i.world,normal,albedo,_SkyPosition,_SkyColor.rgb);
                color+=Light(i.world,normal,albedo,_LampPosition,_LampColor.rgb);
                return half4(color*i.color.rgb,tex.a*i.color.a);
            }
            ENDHLSL
        }
    }
}

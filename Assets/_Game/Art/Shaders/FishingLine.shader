Shader "Fishing/Fishing Line"
{
    Properties
    {
        _AirTint ("Nylon above water", Color) = (.64,.75,.79,1)
        _WaterTint ("Nylon below water", Color) = (.56,.81,.88,1)
        _SurfaceY ("Water height", Float) = .35
        _DepthRange ("Depth fading distance", Float) = 100
        _Gloss ("Narrow nylon highlight", Range(0,.5)) = .2
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
            CBUFFER_START(UnityPerMaterial)
            half4 _AirTint, _WaterTint;
            float _SurfaceY, _DepthRange, _Gloss;
            CBUFFER_END
            float4 _SkyPosition, _LampPosition;
            half4 _SkyColor, _LampColor;
            struct Attributes { float3 positionOS:POSITION; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 world:TEXCOORD0; float2 uv:TEXCOORD1; half2 across:TEXCOORD2; half4 color:COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 world=TransformObjectToWorld(v.positionOS);
                o.positionCS=TransformWorldToHClip(world); o.world=world.xy; o.uv=v.uv; o.color=v.color;
                float2 tangent=TransformObjectToWorldDir(v.tangentOS.xyz).xy;
                tangent=dot(tangent,tangent)>.0001 ? normalize(tangent) : float2(0,1);
                o.across=half2(-tangent.y,tangent.x);
                return o;
            }
            half3 Light(float2 world,half3 normal,half3 albedo,float4 source,half3 color)
            {
                float2 delta=source.xy-world;
                half fade=pow(saturate(1-length(delta)/max(.01,source.w)),2);
                half3 direction=normalize(half3(delta,max(.1,source.z)));
                half diffuse=.3+.7*saturate(dot(normal,direction));
                half specular=pow(saturate(dot(normal,normalize(direction+half3(0,0,1)))),32)*_Gloss;
                return color*fade*(albedo*diffuse+specular);
            }
            half4 Frag(Varyings i):SV_Target
            {
                half radial=i.uv.y*2-1;
                half roundness=sqrt(saturate(1-radial*radial));
                half3 normal=normalize(half3(i.across*radial,roundness+.01));
                float depth=max(0,_SurfaceY-i.world.y);
                half water=1-smoothstep(_SurfaceY-.08,_SurfaceY+.08,i.world.y);
                half3 albedo=lerp(_AirTint.rgb,_WaterTint.rgb,water);
                half ambient=lerp(.84,.48,smoothstep(0,max(1,_DepthRange),depth));
                half3 color=albedo*ambient*(.65+.35*roundness);
                color+=Light(i.world,normal,albedo,_SkyPosition,_SkyColor.rgb);
                color+=Light(i.world,normal,albedo,_LampPosition,_LampColor.rgb);
                return half4(color*i.color.rgb,i.color.a*(1-smoothstep(.8,1,abs(radial))*.35));
            }
            ENDHLSL
        }
    }
}

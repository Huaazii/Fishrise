Shader "Fishing/Snow Ground"
{
    Properties
    {
        _MainTex ("Existing snow grain", 2D) = "white" {}
        _ContactStrength ("Snow contact", Range(0,.5)) = .24
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
            #include "GroundContacts.hlsl"
            #include "ProfileIce.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float _ContactStrength;
            CBUFFER_END
            float4 _SkyPosition, _LampPosition;
            half4 _SkyColor, _LampColor;
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 world:TEXCOORD0; float2 uv:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o; float3 world=TransformObjectToWorld(v.positionOS); o.positionCS=TransformWorldToHClip(world);
                o.world=world.xy; o.uv=v.uv; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                clip(-ProfileIceDistance(i.world));
                half3 snow=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2(.05+i.uv.x*.62,.92+i.uv.y*.055)).rgb;
                snow*=half3(.91,.97,1.02);
                snow=lerp(half3(.80,.86,.91),snow,.45);
                snow*=.84+_SkyColor.rgb*.35;
                half3 normal=ProfileIceBevelNormal(i.world,half3(0,.38,.925),1);
                half3 skyDirection=normalize(half3(_SkyPosition.xy-i.world,max(.1,_SkyPosition.z)));
                snow*=.77+.23*saturate(dot(normal,skyDirection));
                half roll=smoothstep(.015,.14,.98-i.world.y);
                snow*=lerp(half3(1,1,1),half3(.71,.85,.94),roll*.5);
                snow+=_SkyColor.rgb*pow(saturate(dot(normal,normalize(skyDirection+half3(0,0,1)))),20)*.055;
                float2 delta=_LampPosition.xy-i.world;
                half3 lampDirection=normalize(half3(delta,max(.1,_LampPosition.z)));
                half warmth=pow(saturate(1-length(delta)/max(.1,_LampPosition.w)),2)*(.055+.055*saturate(dot(normal,lampDirection)));
                snow+=_LampColor.rgb*warmth;
                snow*=1-SurfaceContactShadow(i.world,_ContactStrength);
                return half4(snow,1);
            }
            ENDHLSL
        }
    }
}

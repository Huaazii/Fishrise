Shader "Fishing/Submerged Ice"
{
    Properties
    {
        [PerRendererData] _MainTex ("Existing ice grain", 2D) = "white" {}
        _CausticA ("Caustics A", 2D) = "black" {}
        _CausticB ("Caustics B", 2D) = "black" {}
        [Normal] _FlowTex ("Shared water flow", 2D) = "bump" {}
        [HideInInspector] _WaterTime ("Pause-aware phase", Float) = 0
        [HideInInspector] _CausticBlend ("Frame blend", Float) = 0
        [HideInInspector] _SkyPosition ("Actual sky light", Vector) = (-3.8,5.7,5,18)
        [HideInInspector] _SkyColor ("Actual sky color", Color) = (.34,.38,.42,1)
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
            TEXTURE2D(_CausticA); SAMPLER(sampler_CausticA);
            TEXTURE2D(_CausticB); SAMPLER(sampler_CausticB);
            CBUFFER_START(UnityPerMaterial)
            float4 _SkyPosition, _SkyColor;
            float _WaterTime, _CausticBlend;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 world:TEXCOORD0; float2 uv:TEXCOORD1; half3 normal:TEXCOORD2; half4 color:COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o; float3 world=TransformObjectToWorld(v.positionOS); o.positionCS=TransformWorldToHClip(world);
                o.world=world.xy; o.uv=v.uv; o.normal=TransformObjectToWorldNormal(v.normalOS); o.color=v.color; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half2 flow=UnpackNormal(SAMPLE_TEXTURE2D(_FlowTex,sampler_FlowTex,i.world*.19+_WaterTime*float2(.014,-.009))).xy;
                half4 grain=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+flow*.0015);
                half3 normal=normalize(i.normal+half3(flow*.17,0));
                half3 light=normalize(half3(_SkyPosition.xy-i.world,max(.5,_SkyPosition.z)));
                half diffuse=saturate(dot(normal,light));
                half3 ice=grain.rgb*half3(.65,1.15,1.3)*(.72+diffuse*.38+_SkyColor.rgb*.25);
                float depth=max(0,.86-i.world.y);
                float2 uv=frac(i.world*.32+flow*.035);
                half caustic=lerp(SAMPLE_TEXTURE2D(_CausticA,sampler_CausticA,uv).r,SAMPLE_TEXTURE2D(_CausticB,sampler_CausticB,uv).r,_CausticBlend);
                ice+=half3(.20,.57,.68)*saturate((caustic-.35)*1.8)*.22*exp2(-depth/9);
                half rim=pow(1-saturate(normal.z),2);
                ice+=half3(.25,.70,.83)*rim*.15;
                ice=lerp(ice,half3(.035,.37,.59),.12+.12*saturate(depth/6));
                // Geometry authors the submerged silhouette; cropped texture alpha must not punch holes in its solid volume.
                return half4(ice*i.color.rgb,i.color.a);
            }
            ENDHLSL
        }
    }
}

Shader "Fishing/Shallow Water Refraction"
{
    Properties
    {
        [Normal] _FlowTex ("Shared surface flow", 2D) = "bump" {}
        _RefractionStrength ("Shallow refraction", Range(0,.012)) = .004
        [HideInInspector] _WaterTime ("Pause-aware phase", Float) = 0
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
            TEXTURE2D(_FlowTex); SAMPLER(sampler_FlowTex);
            TEXTURE2D_X(_CameraSortingLayerTexture); SAMPLER(sampler_LinearClamp);
            CBUFFER_START(UnityPerMaterial)
            float _WaterTime, _RefractionStrength;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 world:TEXCOORD0; float4 screen:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o; float3 world=TransformObjectToWorld(v.positionOS); o.positionCS=TransformWorldToHClip(world); o.world=world.xy;
                o.screen=ComputeScreenPos(o.positionCS); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float depth=.86-i.world.y;
                half2 a=UnpackNormal(SAMPLE_TEXTURE2D(_FlowTex,sampler_FlowTex,i.world*.20+_WaterTime*float2(.018,-.008))).xy;
                half2 b=UnpackNormal(SAMPLE_TEXTURE2D(_FlowTex,sampler_FlowTex,i.world*.37+_WaterTime*float2(-.011,.014))).xy;
                half2 flow=(a+b)*.5;
                float mask=smoothstep(0,.06,depth)*(1-smoothstep(3.5,5.85,depth));
                float2 screenUV=i.screen.xy/i.screen.w;
                float2 offset=flow*_RefractionStrength*float2(_ScaledScreenParams.y/_ScaledScreenParams.x,1);
                // Default is captured once by the existing 2D renderer; this pass never samples itself or the foreground ice.
                half3 behind=SAMPLE_TEXTURE2D_X(_CameraSortingLayerTexture,sampler_LinearClamp,saturate(screenUV+offset)).rgb;
                half3 water=behind*half3(.94,1.02,1.055);
                half sheen=pow(saturate(.65+flow.y*.65),8)*.055*exp2(-depth/1.8);
                half waterline=exp2(-abs(depth-.06+sin(i.world.x*3.7+_WaterTime*.8)*.012)*65);
                water+=half3(.36,.78,.9)*(sheen+waterline*.15);
                return half4(water,mask*.78);
            }
            ENDHLSL
        }
    }
}

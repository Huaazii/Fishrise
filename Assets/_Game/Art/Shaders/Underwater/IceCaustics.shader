Shader "Fishing/Ice Caustics"
{
    Properties
    {
        [PerRendererData] _MainTex ("Ice sprite", 2D) = "white" {}
        _CausticA ("Caustics frame A", 2D) = "black" {}
        _CausticB ("Caustics frame B", 2D) = "black" {}
        [Normal] _FlowTex ("Water surface normal", 2D) = "bump" {}
        _RefractionStrength ("Background refraction", Range(0,.025)) = .006
        _Transmission ("Ice translucency", Range(0,.65)) = .28
        _ReflectionStrength ("Moving reflection", Range(0,.8)) = .24
        _ReflectionColor ("Reflected sky light", Color) = (.48,.82,1,1)
        _TextureDistortion ("Internal ice distortion", Range(0,.02)) = .004
        _EndFade ("Soft chunk ends", Range(.01,.3)) = .14
        _CausticColor ("Caustic light", Color) = (.42,.73,.85,1)
        _CausticStrength ("Caustic strength", Range(0,.4)) = .16
        _FogColor ("Water haze", Color) = (.035,.40,.63,1)
        _DeepFogColor ("Deep water haze", Color) = (.016,.17,.34,1)
        _FogStrength ("Ice immersion", Range(0,1)) = .72
        _DepthRange ("World water depth", Float) = 100
        _SurfaceY ("World surface Y", Float) = .35
        _BottomY ("World seabed Y", Float) = -103
        [HideInInspector] _CausticBlend ("Frame blend", Float) = 0
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
            TEXTURE2D(_CausticA); SAMPLER(sampler_CausticA);
            TEXTURE2D(_CausticB); SAMPLER(sampler_CausticB);
            TEXTURE2D(_FlowTex); SAMPLER(sampler_FlowTex);
            TEXTURE2D_X(_CameraSortingLayerTexture); SAMPLER(sampler_LinearClamp);
            CBUFFER_START(UnityPerMaterial)
            float4 _CausticColor, _Color, _RendererColor, _FogColor, _DeepFogColor, _ReflectionColor;
            float _CausticStrength, _SurfaceY, _CausticBlend, _WaterTime, _FogStrength, _DepthRange;
            float _RefractionStrength, _Transmission, _ReflectionStrength, _TextureDistortion, _EndFade, _BottomY;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 world:TEXCOORD0; float2 uv:TEXCOORD1; half4 color:COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o; float3 world = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(world); o.world = world.xy; o.uv = v.uv;
                o.color = v.color * _Color * _RendererColor; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half4 ice = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv) * i.color;
                float2 flowUV = i.world * .18;
                half2 flowA = UnpackNormal(SAMPLE_TEXTURE2D(_FlowTex,sampler_FlowTex,flowUV + _WaterTime * float2(.018,-.011))).xy;
                half2 flowB = UnpackNormal(SAMPLE_TEXTURE2D(_FlowTex,sampler_FlowTex,flowUV * 1.63 + _WaterTime * float2(-.013,.008))).xy;
                half2 flow = (flowA + flowB) * .5;
                half4 warped = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,saturate(i.uv + flow * _TextureDistortion));
                ice.rgb = lerp(ice.rgb,warped.rgb * i.color.rgb,warped.a);
                float depth = max(0,_SurfaceY-i.world.y);
                float fade = .18 + .82 * exp2(-depth / 38);
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float2 offset = flow * _RefractionStrength * float2(_ScaledScreenParams.y/_ScaledScreenParams.x,1);
                half3 behind = SAMPLE_TEXTURE2D_X(_CameraSortingLayerTexture,sampler_LinearClamp,saturate(screenUV + offset)).rgb;
                // A real background colour sample bends through the ice; only the ice silhouette transmits it.
                ice.rgb = lerp(ice.rgb,behind * half3(.78,.94,1.06),_Transmission);
                half3 normal = normalize(half3(flow * 1.6,.65));
                half glint = pow(saturate(dot(normal,normalize(half3(-.45,.55,.7)))),24);
                half fresnel = pow(1-saturate(normal.z),2);
                ice.rgb += _ReflectionColor.rgb * _ReflectionStrength * (glint + fresnel * .45) * fade;
                float2 uv = frac(i.world * .29 + flow * .035 + _WaterTime * float2(.005,.002));
                half a = SAMPLE_TEXTURE2D(_CausticA,sampler_CausticA,uv).r;
                half b = SAMPLE_TEXTURE2D(_CausticB,sampler_CausticB,uv).r;
                half light = saturate((lerp(a,b,_CausticBlend) - .35) * 1.5);
                ice.rgb += _CausticColor.rgb * light * _CausticStrength * fade;
                half3 haze = lerp(_FogColor.rgb,_DeepFogColor.rgb,smoothstep(.18,1,depth/max(1,_DepthRange)));
                ice.rgb = lerp(ice.rgb,haze,_FogStrength*(.16 + .84*(1-exp2(-depth/32))));
                // Fade overlaps in sprite space, but keep the PNG's jagged inner silhouette intact.
                ice.a *= smoothstep(0,_EndFade,i.uv.y) * smoothstep(0,_EndFade,1-i.uv.y);
                ice.a *= smoothstep(_BottomY,_BottomY+.8,i.world.y) * (1-step(_SurfaceY,i.world.y));
                return ice;
            }
            ENDHLSL
        }
    }
}

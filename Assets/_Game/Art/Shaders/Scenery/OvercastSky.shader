Shader "Fishing/Overcast Sky"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _HorizonColor ("Horizon", Color) = (.38,.49,.57,1)
        _ZenithColor ("Cloud ceiling", Color) = (.09,.145,.20,1)
        [HideInInspector] _WaterTime ("Pause-aware phase", Float) = 0
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
            CBUFFER_START(UnityPerMaterial)
            float4 _HorizonColor, _ZenithColor, _Color, _RendererColor;
            float _WaterTime;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 world:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o; float3 world=TransformObjectToWorld(v.positionOS);
                o.positionCS=TransformWorldToHClip(world); o.world=world.xy; o.color=v.color*_Color*_RendererColor; return o;
            }
            float Hash(float2 p)
            {
                // Avoid amplifying mobile GPU trigonometric rounding into visible noise-cell seams.
                float3 q=frac(float3(p.x,p.y,p.x)*.1031);
                q+=dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }
            float Noise(float2 p)
            {
                float2 a=floor(p), f=frac(p); f=f*f*f*(f*(f*6-15)+10);
                return lerp(lerp(Hash(a),Hash(a+float2(1,0)),f.x),lerp(Hash(a+float2(0,1)),Hash(a+1),f.x),f.y);
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.world*float2(.17,.70)+float2(_WaterTime*.003,0);
                p+=float2(sin(i.world.y*.23),sin(i.world.x*.31))*.28;
                // Rotate octave grids independently so stretched cells do not line up as rectangular sheets.
                float2 middle=float2(p.x*.8-p.y*.6,p.x*.6+p.y*.8)*2.13+float2(8.2,3.1);
                float2 detail=float2(p.x*.6+p.y*.8,-p.x*.8+p.y*.6)*4.37+13.7;
                float cloud=Noise(p)*.57+Noise(middle)*.29+Noise(detail)*.14;
                float ceiling=smoothstep(1.2,15,i.world.y);
                half3 sky=lerp(_HorizonColor.rgb,_ZenithColor.rgb,ceiling);
                sky*=lerp(.70,1.15,smoothstep(.20,.80,cloud));
                sky+=half3(.055,.068,.077)*(cloud-.5)*smoothstep(1.5,6,i.world.y);
                return half4(sky*i.color.rgb,i.color.a);
            }
            ENDHLSL
        }
    }
}

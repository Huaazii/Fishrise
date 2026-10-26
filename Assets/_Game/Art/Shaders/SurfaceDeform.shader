Shader "Fishing/Surface Deformed Lit"
{
    Properties
    {
        [PerRendererData] _MainTex("Art",2D)="white"{}
        _MaskTex("Mask",2D)="white"{}
        _Tint("Tint",Color)=(1,1,1,1)
        _SpriteBounds("Bounds",Vector)=(0,0,1,1)
        _Deform("Kind amplitude phase head",Vector)=(0,0,0,1)
        _SupportProfile("Measured support heights",Vector)=(.155,.081,.0065,.049)
    }
    SubShader
    {
        Tags{"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        float4 _SpriteBounds, _Deform, _SupportProfile;
        half4 _Tint;
        float4 _SkyPosition, _LampPosition;
        half4 _SkyColor, _LampColor;
        float2 BodyUV(float3 p){return saturate((p.xy-_SpriteBounds.xy)/max(_SpriteBounds.zw,.001));}
        float3 Bend(float3 p)
        {
            float2 q=BodyUV(p);
            if(_Deform.x<.5)
            {
                float tail=_Deform.w>.5 ? saturate((_Deform.w-q.x)/max(.1,_Deform.w)) : saturate((q.x-_Deform.w)/max(.1,1-_Deform.w));
                p.y+=_Deform.y*sin(_Deform.z+tail*4.5)*tail*tail;
            }
            else if(_Deform.x<1.5)
            {p.x+=_Deform.y*q.y*q.y; p.y-=abs(_Deform.y)*q.y*.12;}
            else if(_Deform.x<2.5)
            {float t=saturate(p.x/max(.001,_SpriteBounds.x+_SpriteBounds.z)); p.y+=_Deform.y*t*t;}
            // Remove the baked perspective offset at the support points while keeping the upper artwork intact.
            if(_Deform.x>.5 && _Deform.x<1.5)
            {
                float floorY=q.x<.30 ? lerp(_SupportProfile.x,_SupportProfile.y,saturate((q.x-.135)/.165)) :
                    q.x<.53 ? lerp(_SupportProfile.y,_SupportProfile.z,saturate((q.x-.30)/.23)) : lerp(_SupportProfile.z,_SupportProfile.w,saturate((q.x-.53)/.32));
                p.y-=floorY*(1-smoothstep(.20,.45,q.y));
            }
            else if(_Deform.x>2.5)
            {
                float floorY=q.x<.60 ? lerp(.255,0,saturate((q.x-.05)/.55)) : lerp(0,.255,saturate((q.x-.60)/.35));
                p.y-=floorY*(1-smoothstep(.45,.78,q.y));
            }
            return p;
        }
        half3 CurvedNormal(float2 q)
        {
            half curve=_Deform.x<.5 ? (q.y-.5)*1.35 : 0;
            return normalize(half3(0,curve,1));
        }
        ENDHLSL
        Pass
        {
            Tags{"LightMode"="Universal2D"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif
            struct A{float3 p:POSITION;float2 uv:TEXCOORD0;half4 c:COLOR;};
            struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;half2 lightUV:TEXCOORD1;float2 body:TEXCOORD2;float3 world:TEXCOORD3;half4 c:COLOR;};
            V Vert(A a)
            {
                V o; o.body=BodyUV(a.p); float3 p=Bend(a.p); o.world=TransformObjectToWorld(p);
                o.p=TransformWorldToHClip(o.world); o.uv=a.uv; o.c=a.c*_Tint;
                o.lightUV=half2(ComputeScreenPos(o.p/o.p.w).xy); return o;
            }
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"
            half Sheen(float3 world,half3 normal,float4 source,half3 color)
            {
                float2 d=source.xy-world.xy; half fade=pow(saturate(1-length(d)/max(.01,source.w)),2);
                half3 l=normalize(half3(d,max(.1,source.z)));
                return pow(saturate(dot(normal,normalize(l+half3(0,0,1)))),28)*fade*max(color.r,max(color.g,color.b));
            }
            half4 Frag(V i):SV_Target
            {
                half4 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.c;
                SurfaceData2D s; InputData2D d;
                InitializeSurfaceData(tex.rgb,tex.a,SAMPLE_TEXTURE2D(_MaskTex,sampler_MaskTex,i.uv),s);
                InitializeInputData(i.uv,i.lightUV,d); half4 lit=CombinedShapeLightShared(s,d);
                if(_Deform.x<.5)
                {
                    half3 n=normalize(TransformObjectToWorldDir(CurvedNormal(i.body)));
                    lit.rgb+=.10*(Sheen(i.world,n,_SkyPosition,_SkyColor.rgb)*half3(.8,.91,1)+Sheen(i.world,n,_LampPosition,_LampColor.rgb)*half3(1,.76,.44))*tex.a;
                }
                return lit;
            }
            ENDHLSL
        }
        Pass
        {
            Tags{"LightMode"="NormalsRendering"}
            HLSLPROGRAM
            #pragma vertex VertNormal
            #pragma fragment FragNormal
            struct A{float3 p:POSITION;float2 uv:TEXCOORD0;};
            struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float2 body:TEXCOORD1;};
            V VertNormal(A a){V o;o.p=TransformObjectToHClip(Bend(a.p));o.uv=a.uv;o.body=BodyUV(a.p);return o;}
            half4 FragNormal(V i):SV_Target
            {
                half3 normal=normalize(TransformObjectToWorldDir(CurvedNormal(i.body)));
                // URP 2D normals face the camera (-Z), unlike the local +Z used by the sheen calculation.
                normal.z=-normal.z;
                return half4(normal*.5+.5,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a*_Tint.a);
            }
            ENDHLSL
        }
    }
}

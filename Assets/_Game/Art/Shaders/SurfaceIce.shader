Shader "Fishing/Surface Ice Lit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Ice artwork", 2D) = "white" {}
        _NormalStrength ("Fixed ice face normals", Range(0,1)) = .65
        _IceGloss ("Bare ice highlight", Range(0,.5)) = .16
        _SnowGloss ("Snow highlight", Range(0,.1)) = .018
        _ContactStrength ("Soft ground contact", Range(0,.5)) = .24
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
        half4 _Color, _RendererColor;
        float _NormalStrength, _IceGloss, _SnowGloss, _ContactStrength;
        CBUFFER_END
        // These values belong to each environment instance, rather than the shared material asset.
        float4 _SkyPosition, _LampPosition;
        half4 _SkyColor, _LampColor;
        #include "Scenery/GroundContacts.hlsl"
        #include "Scenery/ProfileIce.hlsl"
        struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float2 uv:TEXCOORD0;
            float2 world:TEXCOORD1;
            half2 lightingUV:TEXCOORD2;
            half4 color:COLOR;
        };
        Varyings Vert(Attributes v)
        {
            Varyings o;
            float3 world = TransformObjectToWorld(v.positionOS);
            o.positionCS = TransformWorldToHClip(world);
            o.world = world.xy; o.uv = v.uv;
            o.lightingUV = ComputeScreenPos(o.positionCS / o.positionCS.w).xy;
            o.color = v.color * _Color * _RendererColor;
            return o;
        }
        half SnowMask(half3 albedo)
        {
            // Colour separates snow/frost from blue ice; baked photographic shadows are not used as height.
            half brightness = dot(albedo,half3(.2126,.7152,.0722));
            return (1-smoothstep(.07,.24,albedo.b-albedo.r)) * smoothstep(.38,.72,brightness);
        }
        half4 ProfileArtwork(Varyings i)
        {
            float depth=.98-i.world.y;
            clip(-ProfileIceDistance(i.world));
            float u=i.world.x<0 ? .12+i.uv.x*.55 : .35+i.uv.x*.50;
            half3 cliff=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2(u,.62-.48*saturate(depth/2.6))).rgb;
            half3 snow=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2(u,.955)).rgb;
            half3 color=lerp(cliff,snow,1-smoothstep(.07,.15,depth));
            half wet=1-smoothstep(.70,.86,i.world.y);
            color*=lerp(half3(1,1,1),half3(.75,.96,1.10),wet*.35);
            half inner=(1-smoothstep(.77,1.08,abs(i.world.x)))*smoothstep(.10,.30,depth)*(1-smoothstep(1.05,1.55,depth));
            color*=lerp(half3(1,1,1),half3(.64,.83,.94),inner*.6);
            half originalAlpha=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;
            // Flatten the former perspective opening; keep its natural lower ice silhouette only well below the snow cap.
            half alpha=lerp(1,originalAlpha,smoothstep(1.4,1.9,depth));
            return half4(color,alpha)*i.color;
        }
        half3 FaceNormal(Varyings i, half snow)
        {
            half facet = sin(i.world.x*3.8) * .2 + sin(i.world.x*8.3+i.world.y*.6) * .055;
            half3 front = normalize(half3(facet,.06,1));
            half top = smoothstep(.60,.84,i.uv.y) * snow;
            half3 face = normalize(lerp(front,half3(.025,.73,.68),top));
            return ProfileIceBevelNormal(i.world,normalize(lerp(half3(0,0,1),face,_NormalStrength)),_NormalStrength);
        }
        half3 Highlight(float2 world, half3 normal, half snow, float4 source, half3 color)
        {
            float2 delta = source.xy-world;
            half falloff = pow(saturate(1-length(delta)/max(.01,source.w)),2);
            half3 direction = normalize(half3(delta,max(.1,source.z)));
            half facing = saturate(dot(normal,normalize(direction+half3(0,0,1))));
            half specular = lerp(pow(facing,48)*_IceGloss,pow(facing,8)*_SnowGloss,snow);
            return color * specular * falloff;
        }
        ENDHLSL
        Pass
        {
            Name "SurfaceLighting"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
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
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"
            half4 Frag(Varyings i):SV_Target
            {
                half4 artwork = ProfileArtwork(i);
                half snow = SnowMask(artwork.rgb);
                SurfaceData2D surface; InputData2D input;
                InitializeSurfaceData(artwork.rgb,artwork.a,half4(1,1,1,1),surface);
                InitializeInputData(i.uv,i.lightingUV,input);
                half4 lit = CombinedShapeLightShared(surface,input);
                lit.rgb *= 1-SurfaceContactShadow(i.world,_ContactStrength);
                half3 normal = FaceNormal(i,snow);
                lit.rgb += Highlight(i.world,normal,snow,_SkyPosition,_SkyColor.rgb);
                lit.rgb += Highlight(i.world,normal,snow,_LampPosition,_LampColor.rgb);
                return lit;
            }
            ENDHLSL
        }
        Pass
        {
            Name "SurfaceNormals"
            Tags { "LightMode"="NormalsRendering" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Normals
            half4 Normals(Varyings i):SV_Target
            {
                half4 artwork = ProfileArtwork(i);
                half3 normal = FaceNormal(i,SnowMask(artwork.rgb));
                // The fixed front-facing camera looks along +Z; encode world normals for URP's normal buffer.
                return half4(half3(normal.xy,-normal.z)*.5+.5,artwork.a);
            }
            ENDHLSL
        }
    }
}

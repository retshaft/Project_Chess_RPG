Shader "Checkmate/Environment/Stylized Background"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,2)) = 0.6
        _OcclusionMap("Occlusion (Green)", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 0.7
        [ToggleUI] _UseMaskMap("Use Packed Tile Mask", Float) = 0
        _MaskMap("Tile Mask (R Metal / G AO / A Smoothness)", 2D) = "white" {}
        _MetalTint("Masked Metal Tint", Color) = (1,1,1,1)
        _ShadowTint("Cool Shadow Tint", Color) = (0.48,0.65,0.73,1)
        _SunTint("Warm Light Tint", Color) = (1,0.95,0.83,1)
        _ShadeThreshold("Light Transition", Range(-0.5,0.8)) = 0.15
        _ShadeSoftness("Transition Softness", Range(0.02,1)) = 0.35
        _ShadeStrength("Stylization", Range(0,1)) = 0.7
        _AmbientStrength("Ambient Strength", Range(0,2)) = 1
        _ShadowFill("Painted Shadow Fill", Range(0,0.5)) = 0.12
        _SpecularStrength("Soft Highlight Strength", Range(0,1)) = 0.12
        _Smoothness("Highlight Smoothness", Range(0,1)) = 0.35
        _Saturation("Texture Saturation", Range(0,2)) = 1.08
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)
        _EmissionMap("Emission Mask", 2D) = "white" {}
        _HazeColor("Distance Haze Color", Color) = (0.53,0.72,0.79,1)
        _HazeStart("Haze Start (World Units)", Float) = 30
        _HazeEnd("Haze End (World Units)", Float) = 140
        _HazeStrength("Distance Haze Strength", Range(0,1)) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Cutout", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2
        [HideInInspector] _Surface("Surface", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "StylizedBackgroundInput.hlsl"
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half4 fogAndVertexLight : TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 5);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v,o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS,v.tangentOS);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=n.normalWS;
                o.tangentWS=half4(n.tangentWS,v.tangentOS.w*GetOddNegativeScale());
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap);
                o.fogAndVertexLight=half4(ComputeFogFactor(p.positionCS.z),VertexLighting(p.positionWS,n.normalWS));
                OUTPUT_LIGHTMAP_UV(v.lightmapUV,unity_LightmapST,o.lightmapUV);
                OUTPUT_SH(n.normalWS,o.vertexSH);
                return o;
            }
            half3 ShadeLight(Light light, half3 n, half3 view, half3 albedo, half2 surface)
            {
                half ndl=dot(n,light.direction);
                half band=smoothstep(_ShadeThreshold-_ShadeSoftness,_ShadeThreshold+_ShadeSoftness,ndl);
                half diffuse=lerp(saturate(ndl),band,_ShadeStrength);
                half attenuation=light.distanceAttenuation*light.shadowAttenuation;
                half3 h=SafeNormalize(light.direction+view);
                half spec=pow(saturate(dot(n,h)),exp2(3+surface.y*7));
                // Tint only illuminated energy; shadows are filled by ambient/GI below.
                return (albedo*_SunTint.rgb*diffuse+spec*_SpecularStrength*surface.x*saturate(ndl))
                    *light.color*attenuation;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
                Alpha(base.a,half4(1,1,1,1),_Cutoff);
                half luminance=dot(base.rgb,half3(0.2126,0.7152,0.0722));
                half3 albedo=max(0,lerp(luminance.xxx,base.rgb,_Saturation));
                half3 nt=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
                half3 n=NormalizeNormalPerPixel(TransformTangentToWorld(nt,
                    half3x3(i.tangentWS.xyz,cross(i.normalWS,i.tangentWS.xyz)*i.tangentWS.w,i.normalWS)));
                half3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                half4 mask=SAMPLE_TEXTURE2D(_MaskMap,sampler_MaskMap,i.uv);
                albedo*=lerp(half3(1,1,1),_MetalTint.rgb,mask.r*_UseMaskMap);
                half aoSource=lerp(SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,i.uv).g,mask.g,_UseMaskMap);
                half ao=lerp(1,aoSource,_OcclusionStrength);
                // Packed metal controls restrained highlight strength, not full PBR reflections.
                half2 surface=half2(lerp(1,lerp(.35,1,mask.r),_UseMaskMap),_Smoothness*lerp(1,mask.a,_UseMaskMap));
                half4 shadowMask=SAMPLE_SHADOWMASK(i.lightmapUV);
                float4 shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    shadowCoord=ComputeScreenPos(TransformWorldToHClip(i.positionWS));
                #endif
                Light main=GetMainLight(shadowCoord,i.positionWS,shadowMask);
                half3 gi=SAMPLE_GI(i.lightmapUV,i.vertexSH,n);
                MixRealtimeAndBakedGI(main,n,gi);
                half3 color=albedo*gi*lerp(half3(1,1,1),_ShadowTint.rgb,_ShadeStrength)*_AmbientStrength*ao;
                color+=ShadeLight(main,n,view,albedo,surface);
                half mainBand=smoothstep(_ShadeThreshold-_ShadeSoftness,_ShadeThreshold+_ShadeSoftness,dot(n,main.direction));
                color+=albedo*_ShadowTint.rgb*main.color*main.distanceAttenuation
                    *_ShadowFill*_ShadeStrength*(1-mainBand*main.shadowAttenuation)*ao;
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData=(InputData)0;
                    inputData.positionWS=i.positionWS;
                    inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                    #if USE_CLUSTER_LIGHT_LOOP
                        UNITY_LOOP for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            color+=ShadeLight(GetAdditionalLight(lightIndex,i.positionWS,shadowMask),n,view,albedo,surface);
                        }
                    #endif
                    uint count=GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(count)
                        color+=ShadeLight(GetAdditionalLight(lightIndex,i.positionWS,shadowMask),n,view,albedo,surface);
                    LIGHT_LOOP_END
                #elif defined(_ADDITIONAL_LIGHTS_VERTEX)
                    color+=albedo*i.fogAndVertexLight.yzw;
                #endif
                color+=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,i.uv).rgb*_EmissionColor.rgb;
                float haze=smoothstep(_HazeStart,max(_HazeStart+0.01,_HazeEnd),distance(GetCameraPositionWS(),i.positionWS))*_HazeStrength;
                color=lerp(color,_HazeColor.rgb,haze);
                return half4(MixFog(color,i.fogAndVertexLight.x),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex UniversalVertexMeta
            #pragma fragment BackgroundMeta
            #pragma shader_feature EDITOR_VISUALIZATION
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"
            half4 BackgroundMeta(Varyings i) : SV_Target
            {
                half4 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
                Alpha(base.a,half4(1,1,1,1),_Cutoff);
                MetaInput m=(MetaInput)0;
                half luminance=dot(base.rgb,half3(.2126,.7152,.0722));
                m.Albedo=max(0,lerp(luminance.xxx,base.rgb,_Saturation));
                half metal=SAMPLE_TEXTURE2D(_MaskMap,sampler_MaskMap,i.uv).r*_UseMaskMap;
                m.Albedo*=lerp(half3(1,1,1),_MetalTint.rgb,metal);
                m.Emission=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,i.uv).rgb*_EmissionColor.rgb;
                return UniversalFragmentMeta(i,m);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

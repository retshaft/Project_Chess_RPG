Shader "Checkmate/Water/Shallow Water"
{
    Properties
    {
        [Header(Depth and Absorption)]
        _Absorption ("RGB absorption per metre", Vector) = (0.65,0.15,0.07,0)
        _Density ("Absorption density", Range(0,3)) = 0.65
        _Scattering ("Scattering colour", Color) = (0.075,0.48,0.53,1)
        _SurfaceTint ("Minimum surface tint", Color) = (0.06,0.34,0.34,1)
        _SurfaceTintStrength ("Minimum surface tint strength", Range(0,0.5)) = 0.14
        _MaxThickness ("Maximum optical path (m)", Float) = 12
        [Header(Refraction)]
        _Refraction ("Distortion strength", Range(0,0.1)) = 0.018
        _MaxOffset ("Maximum screen UV offset", Range(0,0.04)) = 0.012
        _EdgeFade ("Screen edge fade", Range(0.01,0.2)) = 0.07
        _WaveRefraction ("Large wave distortion influence", Range(0,2)) = 1
        [Header(Normal Layer 1)]
        [Normal][NoScaleOffset] _NormalA ("Normal A", 2D) = "bump" {}
        _LayerA ("Tiling / direction X Y / speed", Vector) = (0.55,1,0.3,0.035)
        _StrengthA ("Strength A", Range(0,1)) = 0.16
        [Header(Normal Layer 2)]
        [Normal][NoScaleOffset] _NormalB ("Normal B", 2D) = "bump" {}
        _LayerB ("Tiling / direction X Y / speed", Vector) = (0.83,-0.4,1,0.025)
        _StrengthB ("Strength B", Range(0,1)) = 0.12
        [Header(Normal Layer 3 High and Ultra)]
        [Normal][NoScaleOffset] _NormalC ("Normal C", 2D) = "bump" {}
        _LayerC ("Tiling / direction X Y / speed", Vector) = (1.65,0.65,-1,0.017)
        _StrengthC ("Strength C", Range(0,1)) = 0.055
        [Header(Optional Small Waves)]
        _WaveAmplitude ("Amplitude metres (0 disables)", Range(0,0.25)) = 0
        _WaveFrequency ("Frequency", Range(0.1,8)) = 2
        _WaveSpeed ("Speed", Range(0,3)) = 0.7
        _WaveDirection ("Primary wave direction X Z", Vector) = (1,0.3,0,0)
        [Toggle] _PixelWaves ("Per pixel wave normals for large surfaces", Float) = 0
        [Header(Caustics)]
        [NoScaleOffset] _CausticsTex ("Seamless caustics", 2D) = "black" {}
        _CausticsA ("Scale / rotation radians / speed / distortion", Vector) = (0.28,0.24,0.025,0.09)
        _CausticsB ("Scale / rotation radians / speed / distortion", Vector) = (0.37,1.12,-0.018,0.065)
        _CausticsIntensity ("Caustics intensity", Range(0,5)) = 1.1
        _CausticsDepth ("Depth attenuation metres", Range(0.1,8)) = 2.5
        [Toggle] _CausticsShadows ("Receive floor shadows", Float) = 1
        [Header(Reflection)]
        _Reflection ("Environment reflection strength", Range(0,2)) = 0.85
        _F0 ("Normal incidence reflectance", Range(0,0.1)) = 0.02
        _Roughness ("Perceptual roughness", Range(0.08,0.8)) = 0.24
        [Header(Specular)]
        [Enum(Off,0,Subtle,1,Strong,2)] _StylizedSpecular ("Specular styling", Float) = 1
        _Specular ("Sun highlight intensity", Range(0,3)) = 0.8
        _SpecularLimit ("Sun highlight soft limit", Range(0.1,16)) = 3
        [Header(Contact and Foam)]
        [Enum(Soft,0,Foam,1)] _ContactMode ("Contact mode", Float) = 0
        _ContactWidth ("Contact width metres", Range(0.005,0.5)) = 0.12
        _ContactIntensity ("Soft contact intensity", Range(0,1)) = 0.08
        _ContactColor ("Contact colour", Color) = (0.7,0.86,0.8,1)
        _FoamIntensity ("Foam intensity (0 disables)", Range(0,1)) = 0
        _FoamBreakup ("Foam breakup", Range(0,1)) = 0.55
        _CrestFoam ("Wave crest foam (waves only)", Range(0,1)) = 0
        _ContactRipples ("Organic contact foam coverage", Range(0,1)) = 0
        [Header(Distance and Quality)]
        _FadeStart ("Detail fade start metres", Float) = 12
        _FadeEnd ("Detail fade end metres", Float) = 45
        [Enum(Low,0,Medium,1,High,2,Ultra,3)] _Quality ("Quality (uniform branch)", Float) = 2
        [Header(Debug)]
        [Enum(Off,0,Thickness,1,Fresnel,2,RefractionMask,3,Normal,4,Caustics,5,Contact,6,Foam,7)] _DebugView ("Debug view", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "ShallowWater"
            Tags { "LightMode"="UniversalForward" }
            // Scene colour is already composited below. Alpha blending it again would double transmission.
            Blend One Zero
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            TEXTURE2D(_NormalA); SAMPLER(sampler_NormalA);
            TEXTURE2D(_NormalB); SAMPLER(sampler_NormalB);
            TEXTURE2D(_NormalC); SAMPLER(sampler_NormalC);
            TEXTURE2D(_CausticsTex); SAMPLER(sampler_CausticsTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Absorption, _Scattering, _SurfaceTint, _LayerA, _LayerB, _LayerC, _CausticsA, _CausticsB, _ContactColor, _WaveDirection;
                float _Density, _MaxThickness, _Refraction, _MaxOffset, _EdgeFade, _SurfaceTintStrength;
                float _StrengthA, _StrengthB, _StrengthC, _WaveAmplitude, _WaveFrequency, _WaveSpeed;
                float _CausticsIntensity, _CausticsDepth, _CausticsShadows, _Reflection, _F0, _Roughness;
                float _StylizedSpecular, _Specular, _SpecularLimit, _ContactMode, _ContactWidth, _ContactIntensity;
                float _FoamIntensity, _FoamBreakup, _CrestFoam, _FadeStart, _FadeEnd, _Quality, _DebugView;
                float _WaveRefraction, _PixelWaves, _ContactRipples;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; };
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                half fog : TEXCOORD3;
                float wave : TEXCOORD4;
                half3 baseNormalWS : TEXCOORD5;
            };
            void AddWave(float2 p, float2 direction, float k, float weight, float phaseOffset,
                         inout float height, inout float2 gradient)
            {
                // Dispersive speeds avoid a mechanically repeating pair of equally paced sine waves.
                float phase = dot(p,direction)*k - _Time.y*_WaveSpeed*sqrt(9.81*k) + phaseOffset;
                float s,c; sincos(phase,s,c);
                height += weight*s;
                gradient += weight*k*c*direction;
            }
            void WaveSurface(float2 p, out float height, out float2 slope)
            {
                float2 d = normalize(_WaveDirection.xy + float2(0.0001,0));
                float2 perpendicular = float2(-d.y,d.x);
                height = 0; slope = 0;
                AddWave(p,d,_WaveFrequency,1,0,height,slope);
                AddWave(p,d*0.6+perpendicular*0.8,_WaveFrequency*1.73,0.48,1.7,height,slope);
                AddWave(p,-d*0.8+perpendicular*0.6,_WaveFrequency*2.91,0.24,3.1,height,slope);
                AddWave(p,d*0.28-perpendicular*0.96,_WaveFrequency*4.37,0.12,0.8,height,slope);
            }
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float height = 0; float2 slope = 0;
                UNITY_BRANCH if (_WaveAmplitude > 0)
                {
                    WaveSurface(p.xz,height,slope);
                }
                o.wave = height/1.84;
                p.y += o.wave*_WaveAmplitude;
                slope *= _WaveAmplitude/1.84;
                VertexNormalInputs ni = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.baseNormalWS = ni.normalWS;
                o.normalWS = normalize(ni.normalWS + float3(-slope.x, 0, -slope.y));
                // Keep the detail normal basis orthogonal to the displaced surface.
                float3 displacedTangent = normalize(ni.tangentWS-o.normalWS*dot(ni.tangentWS,o.normalWS));
                o.tangentWS = half4(displacedTangent, v.tangentOS.w * GetOddNegativeScale());
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            float DeviceDepth(float raw)
            {
                #if UNITY_REVERSED_Z
                    return raw;
                #else
                    return lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
                #endif
            }
            float3 Reconstruct(float2 uv, float raw) { return ComputeWorldSpacePosition(uv, DeviceDepth(raw), UNITY_MATRIX_I_VP); }
            float2 FlowUV(float2 uv, float4 layer) { return uv * layer.x + normalize(layer.yz + 0.00001) * layer.w * _Time.y; }
            half3 RNM(half3 a, half3 b) { a.z += 1; b.xy = -b.xy; return normalize(a * dot(a,b) / max(a.z,0.001h) - b); }
            float FoamNoise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                float4 h = frac(sin(float4(dot(cell,float2(127.1,311.7)),
                    dot(cell+float2(1,0),float2(127.1,311.7)),
                    dot(cell+float2(0,1),float2(127.1,311.7)),
                    dot(cell+1,float2(127.1,311.7))))*43758.5453);
                return lerp(lerp(h.x,h.y,f.x),lerp(h.z,h.w,f.x),f.y);
            }
            float2 CausticUV(float2 p, float4 layer)
            {
                float s, c; sincos(layer.y,s,c);
                p = float2(c*p.x-s*p.y,s*p.x+c*p.y) * layer.x;
                p += layer.w * sin(p.yx * 3.7 + _Time.y * layer.z * 3);
                return p + _Time.y * layer.z * float2(0.71,0.43);
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float raw = SampleSceneDepth(uv);
                float3 floorWS = Reconstruct(uv, raw);
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                // World-space ray length is valid for both perspective and orthographic cameras.
                float thickness = clamp(dot(i.positionWS - floorWS, view), 0, _MaxThickness);
                float contact = 1 - smoothstep(0, max(_ContactWidth,0.001), thickness);
                float distanceToCamera = distance(GetCameraPositionWS(), i.positionWS);
                half detail = 1 - smoothstep(_FadeStart, max(_FadeEnd,_FadeStart+0.01), distanceToCamera);
                half3 na = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalA,sampler_NormalA,FlowUV(i.positionWS.xz,_LayerA)),_StrengthA * detail);
                half3 nb = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalB,sampler_NormalB,FlowUV(i.positionWS.xz,_LayerB)),_StrengthB * detail);
                half3 nt = RNM(na,nb);
                UNITY_BRANCH if (_Quality >= 2)
                    nt = RNM(nt,UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalC,sampler_NormalC,FlowUV(i.positionWS.xz,_LayerC)),_StrengthC * detail));
                half3 n0 = normalize(i.normalWS);
                float crest = i.wave;
                UNITY_BRANCH if (_PixelWaves > 0.5 && _WaveAmplitude > 0 && _Quality >= 1)
                {
                    float height; float2 slope;
                    WaveSurface(i.positionWS.xz,height,slope);
                    // Re-evaluate slope rather than interpolating aliased normals across giant triangles.
                    slope *= _WaveAmplitude * detail / 1.84;
                    n0 = normalize(i.baseNormalWS + float3(-slope.x,0,-slope.y));
                    crest = height/1.84;
                }
                half3 tangent = normalize(i.tangentWS.xyz);
                tangent = normalize(tangent - n0*dot(tangent,n0));
                half3 bitangent = normalize(cross(n0,tangent)) * i.tangentWS.w;
                half3 normal = normalize(nt.x*tangent + nt.y*bitangent + nt.z*n0);
                float2 edge = min(uv,1-uv);
                float edgeMask = smoothstep(0,_EdgeFade,min(edge.x,edge.y));
                float refrMask = saturate(thickness / 0.65) * edgeMask * detail * (1-contact*0.65);
                // Include the actual displacement slope; subtracting n0 alone cancelled the large waves.
                float3 distortionNormal = normal-n0 + (n0-normalize(i.baseNormalWS))*_WaveRefraction;
                float2 offset = mul((float3x3)UNITY_MATRIX_V,distortionNormal).xy * _Refraction * refrMask;
                offset.x *= _ScaledScreenParams.y/_ScaledScreenParams.x;
                offset *= min(1,_MaxOffset / max(length(offset),0.00001));
                if (_Quality < 0.5) offset = 0;
                float2 refrUV = saturate(uv + offset);
                UNITY_BRANCH if (_Quality >= 1 && _Refraction > 0)
                {
                    float refrRaw = SampleSceneDepth(refrUV);
                    float3 candidate = Reconstruct(refrUV,refrRaw);
                    // Reject foreground and above-water samples; retain the original floor and colour.
                    float eyeDelta = TransformWorldToView(i.positionWS).z - TransformWorldToView(candidate).z;
                    float valid = step(0.015,eyeDelta) * step(candidate.y,i.positionWS.y + 0.01);
                    refrUV = lerp(uv,refrUV,valid);
                    floorWS = lerp(floorWS,candidate,valid);
                    refrMask *= valid;
                    thickness = clamp(distance(i.positionWS,floorWS),0,_MaxThickness);
                }
                half3 scene = SampleSceneColor(refrUV);
                half3 transmittance = exp(-max(_Absorption.rgb,0) * _Density * thickness);
                Light sun = GetMainLight();
                float verticalDepth = max(0,i.positionWS.y-floorWS.y);
                half caustics = 0;
                UNITY_BRANCH if (_CausticsIntensity > 0)
                {
                    // Refract incident sunlight into water before projecting to the receiver.
                    float3 lightRay = refract(-sun.direction,float3(0,1,0),1.0/1.333);
                    float2 projected = floorWS.xz - lightRay.xz * verticalDepth / max(-lightRay.y,0.15);
                    half ca = SAMPLE_TEXTURE2D(_CausticsTex,sampler_CausticsTex,CausticUV(projected,_CausticsA)).r;
                    half cb = SAMPLE_TEXTURE2D(_CausticsTex,sampler_CausticsTex,CausticUV(projected,_CausticsB)).r;
                    caustics = min(ca,cb) * 2.3;
                    float3 floorNormal = normalize(cross(ddy(floorWS),ddx(floorWS)) + float3(0,0.00001,0));
                    half receiver = abs(dot(floorNormal,sun.direction));
                    half shadow = 1;
                    UNITY_BRANCH if (_CausticsShadows > 0.5 && _Quality >= 2)
                        shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(floorWS));
                    caustics *= _CausticsIntensity * smoothstep(0.015,0.16,verticalDepth) * exp(-thickness / _CausticsDepth) * detail * receiver * shadow * saturate(sun.direction.y * 3);
                }
                half3 transmitted = scene * (1 + caustics * sun.color) * transmittance + _Scattering.rgb * (1-transmittance);
                // A paper-thin optical path still has a visible surface colour. This prevents
                // a copied water object from looking like an invisible overlay in a new scene.
                transmitted = lerp(transmitted, _SurfaceTint.rgb, max(_SurfaceTintStrength, 0.22));
                half fresnel = _F0 + (1-_F0) * pow(1-saturate(dot(normal,view)),5);
                half3 reflection = GlossyEnvironmentReflection(reflect(-view,normal),i.positionWS,_Roughness,1,uv) * _Reflection;
                BRDFData brdf;
                half alpha = 1;
                InitializeBRDFData(half3(0,0,0),0,half3(0.02,0.02,0.02),1-_Roughness,alpha,brdf);
                half highlight = DirectBRDFSpecular(brdf,normal,sun.direction,view) * 0.02 * saturate(dot(normal,sun.direction));
                half style = _StylizedSpecular < 0.5 ? 1 : (_StylizedSpecular < 1.5 ? 1.12 : 1.6);
                highlight = pow(max(highlight,0),style);
                highlight = _SpecularLimit * highlight / (_SpecularLimit + highlight);
                half surfaceShadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(i.positionWS));
                half3 color = lerp(transmitted,reflection,fresnel) + highlight * _Specular * sun.color * surfaceShadow;
                half foam = 0;
                UNITY_BRANCH if (_FoamIntensity > 0 || (_CrestFoam > 0 && _WaveAmplitude > 0))
                {
                    half noise = SAMPLE_TEXTURE2D(_CausticsTex,sampler_CausticsTex,i.positionWS.xz*1.3+_Time.y*float2(0.02,-0.014)).r;
                    half breakup = smoothstep(_FoamBreakup,_FoamBreakup+0.15,noise);
                    foam = (_ContactMode > 0.5 ? contact*_FoamIntensity : 0) * breakup;
                    if (_WaveAmplitude > 0) foam += smoothstep(0.6,0.95,crest)*_CrestFoam*breakup;
                    UNITY_BRANCH if (_ContactRipples > 0 && _ContactMode > 0.5)
                    {
                        // Advected patches, independent of the caustic net. No periodic contour rings.
                        float2 flow = i.positionWS.xz + _Time.y*float2(0.055,-0.035);
                        float broad = FoamNoise(flow*2.1);
                        float fine = FoamNoise(flow*9.3 + broad*2.0 - _Time.y*0.045);
                        float patch = broad*0.7 + fine*0.3;
                        float coverage = smoothstep(0.48,0.72,patch);
                        float width = max(_ContactWidth,0.001)*(0.35+0.85*broad);
                        float fringe = 1-smoothstep(0,width,thickness);
                        float pulse = 0.65+0.35*sin(_Time.y*0.85+broad*6.28);
                        float organic = fringe*coverage*pulse*_FoamIntensity;
                        foam = lerp(foam,organic,_ContactRipples);
                    }
                    foam *= detail;
                }
                color += _ContactColor.rgb * contact * _ContactIntensity * (0.75+0.25*sin(thickness*70-_Time.y*2));
                color = lerp(color,_ContactColor.rgb,saturate(foam));
                // Opaque colour already contains scene fog. Fade only the water contribution to avoid double fogging.
                // URP returns 0 from ComputeFogIntensity when fog is disabled.
                // That means no fog contribution, not zero water visibility.
                half waterVisibility = 1;
                #if defined(FOG_LINEAR_KEYWORD_DECLARED)
                    if (FOG_LINEAR) waterVisibility = ComputeFogIntensity(i.fog);
                #endif
                #if defined(FOG_EXP_KEYWORD_DECLARED)
                    if (FOG_EXP) waterVisibility = ComputeFogIntensity(i.fog);
                #endif
                #if defined(FOG_EXP2_KEYWORD_DECLARED)
                    if (FOG_EXP2) waterVisibility = ComputeFogIntensity(i.fog);
                #endif
                // Fog must not reveal the unattenuated seabed again at long distances.
                // The scene term already has fog; fog only the remaining water contribution.
                half3 sceneWeight = transmittance * (1-max(_SurfaceTintStrength,0.22))
                    * (1-fresnel) * (1-saturate(foam));
                color = scene*sceneWeight + (color-scene*sceneWeight)*waterVisibility
                    + unity_FogColor.rgb*(1-sceneWeight)*(1-waterVisibility);
                if (_DebugView == 1) color = saturate(thickness/2).xxx;
                else if (_DebugView == 2) color = fresnel.xxx;
                else if (_DebugView == 3) color = refrMask.xxx;
                else if (_DebugView == 4) color = normal*0.5+0.5;
                else if (_DebugView == 5) color = caustics.xxx;
                else if (_DebugView == 6) color = contact.xxx;
                else if (_DebugView == 7) color = foam.xxx;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

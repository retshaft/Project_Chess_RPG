#ifndef CHECKMATE_BACKGROUND_INPUT
#define CHECKMATE_BACKGROUND_INPUT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor, _ShadowTint, _SunTint, _EmissionColor, _HazeColor;
half4 _MetalTint;
half _Cutoff, _Surface, _BumpScale, _OcclusionStrength;
half _ShadeThreshold, _ShadeSoftness, _ShadeStrength, _AmbientStrength, _ShadowFill;
half _SpecularStrength, _Smoothness, _Saturation, _HazeStrength;
half _UseMaskMap;
float _HazeStart, _HazeEnd;
CBUFFER_END
TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
#endif

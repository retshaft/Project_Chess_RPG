Shader "Checkmate/Environment/Ruin Triplanar"
{
 Properties
 {
  [MainTexture] _BaseMap("Surface texture",2D)="white"{}
  [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
  _WorldScale("Repeats per metre",Float)=.65
  _Metallic("Metallic",Range(0,1))=0
  _Smoothness("Smoothness",Range(0,1))=.22
  _Cull("Cull",Float)=2
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
  CBUFFER_START(UnityPerMaterial)
  float4 _BaseMap_ST, _BaseColor;
  float _WorldScale,_Metallic,_Smoothness,_Cull;
  CBUFFER_END
  struct A {float4 p:POSITION;float3 n:NORMAL;};
  struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half fog:TEXCOORD2;};
  V Vert(A i){V o;o.w=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(i.n);o.fog=ComputeFogFactor(o.p.z);return o;}
  ENDHLSL
  Pass
  {
   Name "ForwardLit"
   Tags {"LightMode"="UniversalForwardOnly"}
   ZWrite On Cull [_Cull]
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 3.5
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   half4 Frag(V i):SV_Target
   {
    half3 n=normalize(i.n),weights=pow(abs(n),4);weights/=max(dot(weights,1),.001);
    float3 p=i.w*_WorldScale;
    half3 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.yz).rgb*weights.x;
    tex+=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb*weights.y;
    tex+=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb*weights.z;
    SurfaceData s=(SurfaceData)0;s.albedo=tex*_BaseColor.rgb;s.metallic=_Metallic;s.smoothness=_Smoothness;s.normalTS=half3(0,0,1);s.occlusion=1;s.alpha=1;
    InputData d=(InputData)0;d.positionWS=i.w;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.w);d.shadowCoord=TransformWorldToShadowCoord(i.w);d.bakedGI=SampleSH(n);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);d.shadowMask=1;
    half4 c=UniversalFragmentPBR(d,s);c.rgb=MixFog(c.rgb,i.fog);return c;
   }
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly"
   Tags {"LightMode"="DepthOnly"}
   ZWrite On ColorMask R Cull [_Cull]
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Depth
   half4 Depth(V i):SV_Target {return 0;}
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}

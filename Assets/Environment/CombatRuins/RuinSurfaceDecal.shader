Shader "Checkmate/Environment/Ruin Surface Decal"
{
    Properties
    {
        _BaseMap("Moss asset",2D)="white"{}
        _MaskMap("Leak asset",2D)="white"{}
        _Tint("Tint",Color)=(.3,.35,.16,1)
        _Mode("0 Moss / 1 Water stain",Float)=0
        _Strength("Coverage",Range(0,1))=.7
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent"}
        Pass
        {
            Name "Surface Decal"
            Tags {"LightMode"="UniversalForwardOnly"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Mode, _Strength;
            CBUFFER_END
            struct A {float4 p:POSITION;float3 n:NORMAL;};
            struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half fog:TEXCOORD2;};
            V Vert(A i){V o; o.w=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(i.n);o.fog=ComputeFogFactor(o.p.z);return o;}
            half4 Frag(V i):SV_Target
            {
                half3 n=normalize(i.n);
                half4 moss=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.w.xz*.65);
                half broad=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.w.xz*.12+float2(.17,.41)).g;
                half mask=smoothstep(.075,.19,broad)*smoothstep(.15,.7,n.y);
                half3 color=moss.rgb*_Tint.rgb;
                if(_Mode>.5)
                {
                    float2 uv=float2(abs(n.x)>.5?i.w.z:i.w.x,-i.w.y)*float2(.32,.2);
                    half leak=SAMPLE_TEXTURE2D(_MaskMap,sampler_MaskMap,uv).r;
                    mask=saturate(leak*2)* (1-smoothstep(.15,.7,abs(n.y)));
                    mask*=smoothstep(-1.4,.1,i.w.y)*(1-smoothstep(1.0,4.0,i.w.y));
                    color=_Tint.rgb;
                }
                Light l=GetMainLight(TransformWorldToShadowCoord(i.w));
                color*=SampleSH(n)+l.color*saturate(dot(n,l.direction))*l.shadowAttenuation;
                return half4(MixFog(color,i.fog),saturate(mask*_Strength));
            }
            ENDHLSL
        }
    }
}

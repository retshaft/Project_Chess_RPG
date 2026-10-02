Shader "Checkmate/Ruins/BoardEnergyBarrier"
{
    Properties
    {
        [HDR] _EnergyColor("Energy Color", Color) = (0.12, 1.5, 1.8, 1)
        _Opacity("Surface Opacity", Range(0, 1)) = 0.18
        _FrontVisibility("Camera-side Visibility", Range(0, 1)) = 0.12
        _PatternScale("Cell Density", Float) = 2.4
        _FlowSpeed("Flow Speed", Float) = 0.25
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _EnergyColor;
                float _Opacity, _FrontVisibility, _PatternScale, _FlowSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 view = normalize(_WorldSpaceCameraPos - input.positionWS);
                // Quad normal faces the board: near-side panels are seen from their back.
                float farSide = smoothstep(-0.12, 0.12, dot(normalize(input.normalWS), view));
                float visibility = lerp(_FrontVisibility, 1.0, farSide);
                float x = input.positionWS.x + input.positionWS.z;
                // Two offset lattices form regular hexagons in world space.
                float2 cell = float2(x, input.positionWS.y) * _PatternScale;
                float2 period = float2(1.0, 1.7320508);
                float2 a = cell - period * floor(cell / period + 0.5);
                float2 b = cell - period * (floor(cell / period) + 0.5);
                float2 hex = abs(dot(a, a) < dot(b, b) ? a : b);
                float boundary = 0.5 - max(hex.x, dot(hex, float2(0.5, 0.8660254)));
                float antialias = max(fwidth(boundary), 0.001);
                float grid = 1.0 - smoothstep(0.018, 0.018 + antialias, boundary);
                float wave = pow(0.5 + 0.5 * sin(input.positionWS.y * 5.0 - _Time.y * _FlowSpeed * 12.0 + sin(x * 0.65)), 10.0);
                float topFade = 1.0 - smoothstep(0.65, 1.0, input.uv.y);
                float baseGlow = exp(-input.uv.y * 16.0);
                float verticalEdge = pow(abs(input.uv.x * 2.0 - 1.0), 48.0);
                float energy = (0.13 + grid * 0.65 + wave * 0.45 + baseGlow * 1.2 + verticalEdge * 0.4) * topFade;
                float alpha = min(0.28, energy * _Opacity) * visibility;
                return half4(_EnergyColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}

Shader "Checkmate/Environment/Ruins Atmosphere Sky"
{
    Properties
    {
        _Zenith("Zenith", Color) = (0.15,0.43,0.7,1)
        _Horizon("Horizon / Fog Match", Color) = (0.64,0.76,0.82,1)
        _CloudLight("Cloud sunlight", Color) = (0.97,0.96,0.90,1)
        _CloudShade("Cloud shade", Color) = (0.56,0.67,0.77,1)
        _SunDirection("Sun direction", Vector) = (0.3,0.7,-0.4,0)
        _Coverage("Cloud coverage", Range(0,1)) = 0.46
        _Exposure("Exposure", Range(0,4)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Zenith, _Horizon, _CloudLight, _CloudShade, _SunDirection;
            float _Coverage, _Exposure;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.direction = v.positionOS.xyz;
                return o;
            }
            float Hash(float3 p)
            {
                p = frac(p * .1031); p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float Noise(float3 p)
            {
                float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(i),Hash(i+float3(1,0,0)),f.x),
                    lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),
                    lerp(Hash(i+float3(0,1,1)),Hash(i+1),f.x),f.y),f.z);
            }
            float Fbm(float3 p)
            {
                float n=0, a=.53;
                [unroll] for(int k=0;k<6;k++) { n+=a*Noise(p); p=p*2.07+float3(3.1,7.7,2.3); a*=.48; }
                return n;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d=normalize(i.direction), sun=normalize(_SunDirection.xyz);
                float elevation=saturate(d.y);
                float3 sky=lerp(_Horizon.rgb,_Zenith.rgb,pow(elevation,.45));
                float mu=saturate(dot(d,sun));
                sky+=float3(1,.85,.62)*pow(mu,24)*.12;
                // Direction-space noise wraps continuously around the whole sphere, including poles.
                float3 p=d*7.5+float3(14.2,3.7,-9.5);
                float baseCloud=Fbm(p);
                float detail=Fbm(p*3.4+19.7);
                float shape=baseCloud*.85+detail*.15;
                float threshold=lerp(.73,.32,_Coverage);
                float alpha=smoothstep(threshold-.055,threshold+.12,shape);
                alpha*=smoothstep(-.025,.16,d.y);
                float lighting=saturate(.62+(baseCloud-Fbm(p+sun*.24))*3.5);
                float3 cloud=lerp(_CloudShade.rgb,_CloudLight.rgb,lighting);
                cloud+=pow(mu,10)*float3(.15,.13,.09)*(1-alpha);
                sky=lerp(sky,cloud,alpha*.94);
                float disk=smoothstep(cos(.009),cos(.006),mu);
                sky+=disk*float3(5,4.5,3.5)*(1-alpha);
                // Below the horizon, blend to the same atmospheric color as distant geometry.
                sky=lerp(sky,_Horizon.rgb,1-smoothstep(-.07,.025,d.y));
                return half4(sky*_Exposure,1);
            }
            ENDHLSL
        }
    }
}

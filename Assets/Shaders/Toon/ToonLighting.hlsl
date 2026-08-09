#ifndef TOON_LIGHTING_INCLUDED
#define TOON_LIGHTING_INCLUDED

#define ToonDiffuse_half ToonDiffuse_float
#define ToonSpecular_half ToonSpecular_float

// ─── 2단계 셀 셰이딩 ─────────────────────────────────────────────────
void ToonDiffuse_float(
    float NdotL,
    float ShadowAtten,
    float ShadowMask,
    float Threshold1,
    float Softness1,
    float Threshold2,
    out float Step1,
    out float Step2)
{
    float softness2   = Softness1 * 0.5;
    float halfLambert = NdotL * 0.5 + 0.5;   // -1~1 → 0~1 리맵
    float litValue    = halfLambert * ShadowMask * ShadowAtten;
    Step1 = smoothstep(Threshold1 - Softness1, Threshold1 + Softness1, litValue);
    Step2 = smoothstep(Threshold2 - softness2,  Threshold2 + softness2,  litValue);
}

// ─── 툰 스페큘러 (Blinn-Phong) ───────────────────────────────────────
void ToonSpecular_float(
    float3 Normal,
    float3 ViewDir,
    float3 LightDir,
    float  Power,
    float  Threshold,
    float  Softness,
    out float Specular)
{
    float3 H     = normalize(LightDir + ViewDir);
    float  NdotH = saturate(dot(Normal, H));
    float  spec  = pow(NdotH, Power);
    Specular = smoothstep(Threshold - Softness, Threshold + Softness, spec);
}

#endif // TOON_LIGHTING_INCLUDED

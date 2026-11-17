// BubbleR — versión para URP.
// El paquete original usa un surface shader "Standard" del pipeline Built-in, que en URP
// no compila (las burbujas salen rosas). Esto replica el mismo efecto en HLSL de URP:
// rebote del vértice, cambio de color iridiscente y luz PBR con transparencia "fade".
// Para la versión con textura, el shader define BUBBLER_TEXTURED antes de incluir esto.

#ifndef BUBBLER_URP_INCLUDED
#define BUBBLER_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Todas las propiedades en UnityPerMaterial para que el SRP Batcher agrupe los materiales.
CBUFFER_START(UnityPerMaterial)
    half4 _MainColor;
    float4 _MainTexture_ST;
    half _Glossiness;
    half _Metal;
    half _BounceAmplitude;
    half _BounceFrequency;
    half4 _ColorShiftPeak;
    half4 _ColorShiftBand;
    half _ColorShifting;
    half4 _ColorShiftNoise;   // (multiplicador, escala, velocidad, sin uso)
    float _ColorShiftMode;    // 0 = ADD, 1 = MUL, 2 = SUB (BubbleR.BlendMode)
CBUFFER_END

#ifdef BUBBLER_TEXTURED
    TEXTURE2D(_MainTexture);
    SAMPLER(sampler_MainTexture);
#endif

struct BubbleAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    half4  color      : COLOR;
    float4 texcoord   : TEXCOORD0;   // z = AgePercent en partículas (custom vertex streams)
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// ---------------------------------------------------------------- Rebote (igual al original)
float BubbleTimeFunction(float timeScale, float offset)
{
    float t = offset + _Time.y * timeScale;
    // Tres senos para que el rebote no se vea periódico.
    float s = sin(t * 6.0) + sin(t * 2.72) + sin(t * 1.541);
    return s / 3.0;
}

// Deforma la burbuja en espacio de objeto: se estira en Y y se aplasta en XZ y viceversa.
float3 BubbleDeform(BubbleAttributes v)
{
    float offset = 0.0;
#if defined(UNITY_INSTANCING_ENABLED)
    // Con instancing cada burbuja rebota con un desfase distinto.
    offset = unity_InstanceID * 0.631;
#endif
    float vMul = BubbleTimeFunction(_BounceFrequency, offset) * _BounceAmplitude * (1.0 - v.texcoord.z);
    float hMul = -vMul;

    float3 p = v.positionOS.xyz;
    p.y  += v.normalOS.y  * vMul * 0.1;
    p.xz += v.normalOS.xz * hMul * 0.1;
    return p;
}

// ---------------------------------------------------------------- Color iridiscente (igual al original)
float BubbleMod289(float x)   { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float4 BubbleMod289(float4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float4 BubblePerm(float4 x)   { return BubbleMod289(((x * 34.0) + 1.0) * x); }

float BubbleNoise(float3 p)
{
    float3 a = floor(p);
    float3 d = p - a;
    d = d * d * (3.0 - 2.0 * d);

    float4 b = a.xxyy + float4(0.0, 1.0, 0.0, 1.0);
    float4 k1 = BubblePerm(b.xyxy);
    float4 k2 = BubblePerm(k1.xyxy + b.zzww);

    float4 c = k2 + a.zzzz;
    float4 k3 = BubblePerm(c);
    float4 k4 = BubblePerm(c + 1.0);

    float4 o1 = frac(k3 * (1.0 / 41.0));
    float4 o2 = frac(k4 * (1.0 / 41.0));

    float4 o3 = o2 * d.z + o1 * (1.0 - d.z);
    float2 o4 = o3.yw * d.x + o3.xz * (1.0 - d.x);

    return o4.y * d.y + o4.x * (1.0 - d.y);
}

float BubbleNoiseFromNormal(float3 n, float scale, float speed)
{
    float t = _Time.y * speed;
    float val = sin(t + (0.17 + n.x * 3.17 * scale));
    val += sin(t + (0.11 + n.y * 2.57 * scale));
    val += sin(t + n.z * 3.7 * scale);
    val += sin(t + n.x * 0.45 + n.y * 0.67 + n.z * 1.46);
    return val;
}

float BubbleCenter(float val, float center, float range)
{
    return 1.0 - saturate(pow(abs(val - center), 2.0) / (range * 0.5));
}

float3 BubbleShiftColor(float3 positionWS, float3 normalWS, float3 viewDirWS, float noiseMultiplier)
{
    float3 basePos = 0.0;
#if defined(SPATIAL_NOISE)
    basePos = positionWS * _ColorShiftNoise.y;
#endif

    float3 noiseVector = sin(0.15 + basePos * float3(0.231, 0.15, 0.16))
                       + (sin(basePos * float3(0.31, 0.5, 0.9) + normalWS * 2.1) + sin(basePos * 0.04 + normalWS));

    float colorShift = saturate(1.0 - abs(dot(viewDirWS, normalWS)));
    float shiftNoise = BubbleNoise(basePos);
    float angularShiftNoise = BubbleNoiseFromNormal(noiseVector, _ColorShiftNoise.y, _ColorShiftNoise.z);
    colorShift += (angularShiftNoise + shiftNoise) * noiseMultiplier;

    float3 peak = saturate(_ColorShiftPeak.xyz);
    float3 band = max(0.0, _ColorShiftBand.xyz);

    return float3(BubbleCenter(colorShift, peak.x * _ColorShifting, band.x),
                  BubbleCenter(colorShift, peak.y * _ColorShifting, band.y),
                  BubbleCenter(colorShift, peak.z * _ColorShifting, band.z));
}

// Color base + alfa de la burbuja (textura opcional × color × color de vértice).
half4 BubbleBaseColor(float2 uv, half4 vertexColor, out half texAlpha)
{
    half4 col = _MainColor * vertexColor;
    texAlpha = 1.0;
#ifdef BUBBLER_TEXTURED
    half4 tex = SAMPLE_TEXTURE2D(_MainTexture, sampler_MainTexture, uv);
    col *= tex;
    texAlpha = tex.a;
#endif
    return col;
}

// Trama de Bayer 4×4 para las sombras semitransparentes (reemplaza _DitherMaskLOD del Built-in).
static const float BUBBLE_BAYER[16] = { 0.0,  8.0,  2.0, 10.0,
                                        12.0,  4.0, 14.0,  6.0,
                                         3.0, 11.0,  1.0,  9.0,
                                        15.0,  7.0, 13.0,  5.0 };

float BubbleDither(float2 pixel)
{
    uint2 p = uint2(pixel) % 4;
    return (BUBBLE_BAYER[p.y * 4 + p.x] + 0.5) / 16.0;
}

#endif

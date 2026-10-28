// =============================================================================
// NostalgIA — Biblioteca común de la Estación Frutiger Aero (rama experimental)
// Archivo: FrutigerCommon.hlsl
//
// Funciones compartidas por los shaders NOS/Aero*: gradiente de cielo (para que el
// agua, las burbujas y el vidrio reflejen exactamente el mismo cielo que el skybox),
// interferencia de película delgada (iridiscencia de burbujas), cáusticas y un
// sombreado de burbuja reutilizado por la malla instanciada y por las partículas.
//
// Requiere que el shader que lo incluya haya incluido antes Core.hlsl de URP.
// Las variables _NOS_* son GLOBALES (las escribe FrutigerAtmosphere con
// Shader.SetGlobal*): no pertenecen a ningún UnityPerMaterial y no rompen el
// SRP Batcher.
// =============================================================================

#ifndef NOS_FRUTIGER_COMMON_INCLUDED
#define NOS_FRUTIGER_COMMON_INCLUDED

#include "Assets/_Project/Art/Shaders/Include/NostalgIA_Common.hlsl"

// -----------------------------------------------------------------------------
// Globales del ambiente (FrutigerAtmosphere)
// -----------------------------------------------------------------------------
half4 _NOS_SkyZenith;    // Color del cénit.
half4 _NOS_SkyHorizon;   // Color del horizonte.
half4 _NOS_SkyGround;    // Color bajo el horizonte (reflejo del mar lejano).
half4 _NOS_SunTint;      // Color del sol en reflejos y brillos (HDR).
float _NOS_WaterLevel;   // Altura del agua en metros (cáusticas bajo esa cota).

// -----------------------------------------------------------------------------
// Cielo
// -----------------------------------------------------------------------------

// Gradiente de cielo para una dirección de mundo normalizada.
//   arriba:  lerp(horizonte, cénit, y^0,55)  — el exponente < 1 aclara rápido sobre el horizonte
//   abajo:   lerp(..., suelo, saturate(−3y)) — transición corta bajo el horizonte
//   sol:     halo pow(dot,8)·0,35 + núcleo pow(dot,64)·0,6
half3 NOS_SkyGradient(half3 dir, half3 sunDir)
{
    half up = saturate(dir.y);
    half3 sky = lerp(_NOS_SkyHorizon.rgb, _NOS_SkyZenith.rgb, pow(up, 0.55h));
    sky = lerp(sky, _NOS_SkyGround.rgb, saturate(-dir.y * 3.0h));
    half sunDot = saturate(dot(dir, sunDir));
    sky += _NOS_SunTint.rgb * (pow(sunDot, 8.0h) * 0.35h + pow(sunDot, 64.0h) * 0.6h);
    return sky;
}

// -----------------------------------------------------------------------------
// Iridiscencia
// -----------------------------------------------------------------------------

// Interferencia de película delgada (jabón, n ≈ 1,33).
// Diferencia de camino óptico: OPD = 2·n·d·cos θt, con cos θt = √(1 − sin²θi / n²).
// La intensidad reflejada para cada longitud de onda λ oscila como ½ + ½·cos(2π·OPD/λ);
// evaluamos λ = 650, 530 y 450 nm para R, G y B. 'thickness' va en micrómetros.
half3 NOS_ThinFilm(float cosTheta, float thickness)
{
    const float n = 1.33;
    float sinT2 = (1.0 - cosTheta * cosTheta) / (n * n);
    float cosT = sqrt(saturate(1.0 - sinT2));
    float opd = 2.0 * n * thickness * cosT;
    float3 phase = opd / float3(0.65, 0.53, 0.45);
    return (half3)(0.5 + 0.5 * cos(NOS_TWO_PI * phase));
}

// Sombreado completo de una burbuja de jabón, en color PREMULTIPLICADO
// (mezcla Blend One OneMinusSrcAlpha: resultado = color + destino·(1 − α)).
//   N, V, L   : normal, vista y sol en mundo (normalizados)
//   viewN     : normal en espacio de vista (para los reflejos de "ventana" fijos en pantalla)
//   seed      : aleatorio por burbuja (desfasa los remolinos de la película)
// Términos:
//   Fresnel  F = (1 − N·V)³ : la burbuja casi solo se ve en el borde
//   película : grosor d = d₀·(0,55 + 0,9·ruido) con ruido 3D animado → remolinos de color
//   reflejo  : cielo en la dirección reflejada R = reflect(−V, N)
//   sol      : pow(R·L, 180) — destello muy concentrado
//   ventana  : elipse suave arriba a la izquierda en espacio de vista, como el reflejo de
//              una ventana en las ilustraciones Frutiger Aero, más un brillo menor opuesto
half4 NOS_BubbleShade(float3 N, float3 V, float3 L, half3 viewN, float seed,
                      half thickness, half iridescence, half reflection,
                      half baseAlpha, half edgeAlpha, half specular, half windowHighlight, half3 tint)
{
    half NdotV = saturate(dot(N, V));
    half rim = 1.0h - NdotV;
    half fresnel = rim * rim * rim;

    float swirl = NOS_ValueNoise3D(N * 2.3 + float3(seed * 17.0, _Time.y * 0.35, seed * 5.0));
    half3 film = NOS_ThinFilm(NdotV, thickness * (0.55 + 0.9 * swirl));

    float3 R = reflect(-V, N);
    half3 sky = NOS_SkyGradient((half3)R, (half3)L);
    half sun = pow(saturate(dot(R, L)), 180.0h) * specular;

    half2 w1 = (viewN.xy - half2(-0.38h, 0.42h)) * half2(1.7h, 2.6h);
    half window = saturate(1.0h - dot(w1, w1));
    window = window * window * windowHighlight;
    half2 w2 = (viewN.xy - half2(0.45h, -0.5h)) * 4.0h;
    half window2 = saturate(1.0h - dot(w2, w2)) * windowHighlight * 0.45h;
    // Los reflejos de ventana solo existen en la cara visible de la esfera (viewN.z > 0).
    half facing = saturate(viewN.z * 4.0h);
    window *= facing;
    window2 *= facing;

    half3 color = (sky * reflection + film * iridescence) * fresnel + tint * baseAlpha;
    color += (sun * _NOS_SunTint.rgb) + (window + window2);
    half alpha = saturate(baseAlpha + fresnel * edgeAlpha + sun + window * 0.8h);
    return half4(color, alpha);
}

// -----------------------------------------------------------------------------
// Cáusticas
// -----------------------------------------------------------------------------

// Patrón de cáusticas: diferencia de dos campos de ruido que se desplazan en sentidos
// opuestos. Donde ambos valen casi lo mismo, |n₁ − n₂| ≈ 0, y 1 − |n₁ − n₂| elevado a una
// potencia alta deja solo esas líneas finas: una red de filamentos de luz que se deforma.
half NOS_Caustics(float2 p, float time)
{
    float n1 = NOS_ValueNoise2D(p + float2(time * 0.31, time * 0.17));
    float n2 = NOS_ValueNoise2D(p * 1.27 + float2(-time * 0.23, time * 0.29) + 7.3);
    half web = (half)(1.0 - abs(n1 - n2));
    web = pow(web, 10.0h);
    return web;
}

#endif // NOS_FRUTIGER_COMMON_INCLUDED

// =============================================================================
// NostalgIA — Entradas del shader NOS/AeroGloss
// Archivo: AeroGlossInput.hlsl
//
// Búfer por material incluido por TODOS los pases (Forward, ShadowCaster, DepthOnly y
// DepthNormals) para que el SRP Batcher vea el mismo UnityPerMaterial en cada uno.
// =============================================================================

#ifndef NOS_AERO_GLOSS_INPUT_INCLUDED
#define NOS_AERO_GLOSS_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    half4 _ColorTop;          // Color en la parte alta (gradiente de altura).
    half4 _ColorBottom;       // Color en la parte baja.
    half4 _SideColor;         // Color de las pendientes (tierra / arena en las islas).
    half4 _SpeckleColor;      // Motas (flores, brillos de pasto).
    half4 _RimColor;          // Color del borde iluminado por el cielo.
    half4 _EmissionColor;     // Emisión HDR (anclajes, núcleos brillantes).
    float4 _HeightRange;      // x: altura mínima, y: altura máxima del gradiente (m).
    half _SlopeSharpness;     // Nitidez de la transición techo/pendiente.
    half _SpeckleDensity;     // Motas por metro.
    half _SpeckleAmount;      // Fracción de celdas con mota (0–1).
    half _Gloss;              // Brillo especular Blinn-Phong (0–1).
    half _ClearcoatStrength;  // Intensidad del brillo "plástico" nítido.
    half _ClearcoatPower;     // Exponente del brillo nítido.
    half _RimPower;           // Exponente del borde.
    half _RimStrength;        // Intensidad del borde.
    half _Wrap;               // Envoltura de la difusa (0 = Lambert; 0,5 = suave tipo nube).
    half _EmissionPulse;      // Frecuencia del pulso de la emisión en Hz (0 = fija).
    half _CausticStrength;    // Intensidad de las cáusticas bajo el agua.
    float _CausticScale;      // Escala del patrón de cáusticas (por metro).
CBUFFER_END

#endif // NOS_AERO_GLOSS_INPUT_INCLUDED

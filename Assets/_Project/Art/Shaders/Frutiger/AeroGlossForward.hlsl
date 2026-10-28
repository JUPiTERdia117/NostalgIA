// =============================================================================
// NostalgIA — Pase Forward del shader NOS/AeroGloss
// Archivo: AeroGlossForward.hlsl
//
// Superficie "plástico brillante" de la estética Frutiger Aero, usada en colinas, islas,
// plataformas, copas de árboles, nubes 3D y anclajes:
//   albedo  = gradiente por altura → color de pendiente según la normal → motas
//   difusa  = envuelta: max(0, (N·L + w) / (1 + w)) · sombra   (w = 0: Lambert)
//   ambiente= armónicos esféricos de URP (SampleSH)
//   especular Blinn-Phong + capa "clearcoat" nítida (el brillo blanco del plástico)
//   borde   = (1 − N·V)^p · color del cielo, más fuerte en las caras que miran arriba
//   cáusticas bajo _NOS_WaterLevel (opcional, _CAUSTICS)
//   sombreado plano opcional (_FLAT_SHADING): normal de cara reconstruida con derivadas
//   de pantalla, n = normalize(cross(∂p/∂x, ∂p/∂y)), que da el aspecto low poly facetado
//   sin duplicar vértices en la malla.
// Solo considera la luz principal: la Estación es un exterior iluminado por el sol, y así
// se evitan las variantes de luces adicionales (menos memoria de shaders en móvil).
// =============================================================================

#ifndef NOS_AERO_GLOSS_FORWARD_INCLUDED
#define NOS_AERO_GLOSS_FORWARD_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Assets/_Project/Art/Shaders/Frutiger/FrutigerCommon.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float3 positionWS : TEXCOORD0;
    half3 normalWS    : TEXCOORD1;
    half fogFactor    : TEXCOORD2;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD3;
#endif
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings AeroGlossVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionWS = positionInputs.positionWS;
    output.positionCS = positionInputs.positionCS;
    output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
    output.fogFactor = (half)ComputeFogFactor(positionInputs.positionCS.z);
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(positionInputs);
#endif
    return output;
}

half4 AeroGlossFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float3 positionWS = input.positionWS;
    half3 vertexNormal = normalize(input.normalWS);
    half3 N = vertexNormal;
#if defined(_FLAT_SHADING)
    // Normal de la cara a partir de las derivadas de la posición; se orienta hacia el mismo
    // hemisferio que la normal interpolada porque el signo del producto cruz depende de la
    // convención de pantalla de cada API gráfica.
    half3 faceNormal = (half3)normalize(cross(ddx(positionWS), ddy(positionWS)));
    N = faceNormal * (dot(faceNormal, vertexNormal) < 0.0h ? -1.0h : 1.0h);
#endif
    half3 V = (half3)GetWorldSpaceNormalizeViewDir(positionWS);

    // ---- Albedo -------------------------------------------------------------
    half heightT = (half)saturate((positionWS.y - _HeightRange.x) / max(_HeightRange.y - _HeightRange.x, 0.001));
    half3 albedo = lerp(_ColorBottom.rgb, _ColorTop.rgb, heightT);
    // Pendientes: N.y alto = cima (color de altura); N.y bajo = ladera (color de pendiente).
    half slope = saturate((0.75h - N.y) * _SlopeSharpness);
    albedo = lerp(albedo, _SideColor.rgb, slope * _SideColor.a);
    // Motas (flores): celdas de 1/_SpeckleDensity m; en una fracción _SpeckleAmount de ellas se
    // dibuja un punto redondo de radio 0,22 celda, desplazado al azar dentro de la celda, con
    // borde suavizado por fwidth para que no parpadee a distancia.
    float2 speckCoord = positionWS.xz * _SpeckleDensity;
    float2 speckCell = floor(speckCoord);
    float2 jitter = float2(NOS_Hash21(speckCell + 3.1), NOS_Hash21(speckCell + 7.7)) - 0.5;
    float dotDistance = length(frac(speckCoord) - 0.5 - jitter * 0.5);
    float dotEdge = max(fwidth(dotDistance), 1e-4);
    half dotMask = (half)(1.0 - smoothstep(0.22 - dotEdge, 0.22 + dotEdge, dotDistance));
    half speck = (half)step(1.0 - _SpeckleAmount, NOS_Hash21(speckCell)) * dotMask * (1.0h - slope);
    albedo = lerp(albedo, _SpeckleColor.rgb, speck * _SpeckleColor.a);

    // ---- Luz principal con sombras ---------------------------------------------
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
#else
    float4 shadowCoord = float4(0, 0, 0, 0);
#endif
    Light mainLight = GetMainLight(shadowCoord, positionWS, half4(1, 1, 1, 1));
    half3 L = mainLight.direction;
    half attenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
    half3 lightColor = mainLight.color * attenuation;

    // Difusa envuelta (Wrap Lighting): con _Wrap > 0 la luz abraza más allá del terminador (N·L = 0)
    // para que las colinas y las nubes 3D nunca tengan sombras negras duras.
    half NdotL = dot(N, L);
    half wrapped = saturate((NdotL + _Wrap) / (1.0h + _Wrap));
    half3 diffuse = albedo * lightColor * wrapped;
    // SampleSH evalúa los armónicos esféricos del cielo y de las sondas de luz (Light Probes).
    half3 ambient = albedo * SampleSH(N);

    // Doble capa especular Blinn-Phong: una base ancha controlada por _Gloss y una segunda capa
    // muy estrecha (_ClearcoatPower) que imita el barniz de plástico pulido Frutiger Aero.
    half3 H = normalize(L + V);
    half NdotH = saturate(dot(N, H));
    half specPower = exp2(_Gloss * 10.0h + 1.0h);                 // 2 … 2048
    half3 specular = lightColor * (pow(NdotH, specPower) * _Gloss + pow(NdotH, _ClearcoatPower) * _ClearcoatStrength);

    // Luz de borde (Fresnel): realza la silueta con el color del cielo, ponderada por (0,55 + 0,45·N.y)
    // para que brille más en las crestas superiores que en las caras que miran al suelo.
    half NdotV = saturate(dot(N, V));
    half rim = pow(1.0h - NdotV, _RimPower) * _RimStrength * (0.55h + 0.45h * saturate(N.y));
    half3 rimColor = _RimColor.rgb * rim;

    // Pulso senoidal opcional para checkpoints, plataformas de salto y dispensadores.
    half pulse = _EmissionPulse > 0.0h ? (0.65h + 0.35h * sin(_Time.y * NOS_TWO_PI * _EmissionPulse)) : 1.0h;
    half3 emission = _EmissionColor.rgb * pulse;

    half3 color = diffuse + ambient + specular + rimColor + emission;

#if defined(_CAUSTICS)
    // Cáusticas: solo bajo el agua; se atenúan con la profundidad (exp(−0,25·d)) y con la
    // orientación hacia arriba (la luz del sol entra desde la superficie).
    half depthBelow = (half)max(0.0, _NOS_WaterLevel - positionWS.y);
    half underwater = step(0.001h, depthBelow);
    half caustic = NOS_Caustics(positionWS.xz * _CausticScale, _Time.y) * underwater * exp(-0.25h * depthBelow) * saturate(N.y);
    color += lightColor * caustic * _CausticStrength;
#endif

    color = MixFog(color, input.fogFactor);
    return half4(color, 1.0h);
}

#endif // NOS_AERO_GLOSS_FORWARD_INCLUDED

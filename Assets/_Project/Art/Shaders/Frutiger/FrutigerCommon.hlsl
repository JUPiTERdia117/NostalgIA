// =============================================================================
// NostalgIA — Biblioteca común de la Estación Frutiger Aero (rama experimental)
// Archivo: FrutigerCommon.hlsl
//
// Funciones compartidas por los shaders NOS/Aero*: gradiente de cielo (para que el
// agua, las burbujas y el vidrio reflejen exactamente el mismo cielo que el skybox),
// interferencia de película delgada (iridiscencia de burbujas), cáusticas y un
// sombreado de burbuja reutilizado por la malla instanciada y por las partículas.
//
// Hoy lo incluyen NOS_AeroGlass, NOS_AeroWater, NOS_AeroBubble, NOS_AeroBubbleSprite y
// AeroGlossForward.hlsl. Si cambio una fórmula aquí, cambia en todos a la vez; por eso
// cada función recibe todo lo que necesita por parámetro y no lee texturas.
//
// Requiere que el shader que lo incluya haya incluido antes Core.hlsl de URP.
// Las variables _NOS_* son GLOBALES (las escribe FrutigerAtmosphere con
// Shader.SetGlobal*): no pertenecen a ningún UnityPerMaterial y no rompen el
// SRP Batcher.
// =============================================================================

// Guarda de inclusión: si dos archivos incluyen esta biblioteca en el mismo shader,
// la segunda vez el preprocesador se salta todo y no hay funciones duplicadas.
#ifndef NOS_FRUTIGER_COMMON_INCLUDED
#define NOS_FRUTIGER_COMMON_INCLUDED

// Traigo la biblioteca general del proyecto: de ahí salen NOS_TWO_PI, los hashes,
// NOS_ValueNoise2D/3D, NOS_Fbm2D y la paleta coseno.
#include "Assets/_Project/Art/Shaders/Include/NostalgIA_Common.hlsl"

// -----------------------------------------------------------------------------
// Globales del ambiente (FrutigerAtmosphere)
// -----------------------------------------------------------------------------
// Las declaro fuera de cualquier CBUFFER a propósito. FrutigerAtmosphere.cs copia los
// tres colores del material del skybox (NOS/AeroSky) y el tinte del sol con
// Shader.SetGlobalColor, así no repito valores en cada material. Los colores van en
// half porque son números pequeños (0 a ~2 en HDR) y en móvil half ahorra registros;
// la altura del agua va en float porque es una posición en metros.
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
// Es la misma cuenta que hace NOS/AeroSky en su fragment shader (sin nubes ni bruma),
// para que un reflejo en el vidrio o en una burbuja tenga el color del cielo real que
// está detrás. Como 'dir' mide 1, dir.y es el seno de la elevación: 0 en el horizonte,
// 1 en el cénit y negativo hacia abajo.
// Ejemplo: con dir.y = 0,25 → 0,25^0,55 ≈ 0,47, así que a solo 14° sobre el horizonte
// ya voy casi a mitad de camino hacia el azul del cénit.
half3 NOS_SkyGradient(half3 dir, half3 sunDir)
{
    // saturate(dir.y) deja en 0 todo lo que mira hacia abajo, para que pow no reciba negativos.
    half up = saturate(dir.y);
    half3 sky = lerp(_NOS_SkyHorizon.rgb, _NOS_SkyZenith.rgb, pow(up, 0.55h));
    // Por debajo del horizonte, −3y llega a 1 cuando y = −0,33 (unos 19° hacia abajo):
    // ahí ya es todo color "suelo". Si subo el 3, la línea del horizonte se vuelve más dura.
    sky = lerp(sky, _NOS_SkyGround.rgb, saturate(-dir.y * 3.0h));
    // dot(dir, sunDir) es el coseno del ángulo entre lo que miro y el sol.
    // pow(·, 8) da un halo ancho (a 20° del sol todavía vale ~0,61) y pow(·, 64) un núcleo
    // apretado (a 20° ya vale ~0,02). Sumar los dos imita el resplandor real alrededor del sol.
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
// La idea física: la luz rebota en la cara de afuera y en la de adentro de la película.
// Las dos ondas recorren distinto camino; si llegan en fase se suman (color brillante) y
// si llegan desfasadas se cancelan. Como cada color tiene distinta λ, cada canal se
// enciende a distinto grosor y aparecen las bandas de colores de las burbujas.
// Uso float aquí porque OPD/λ puede valer varias unidades y el coseno necesita esa
// precisión para no producir bandas escalonadas en móvil.
half3 NOS_ThinFilm(float cosTheta, float thickness)
{
    const float n = 1.33;
    // Ley de Snell: sin θt = sin θi / n. Como sin² = 1 − cos², calculo sin²θt directo.
    float sinT2 = (1.0 - cosTheta * cosTheta) / (n * n);
    // saturate protege el sqrt de valores negativos por redondeo.
    float cosT = sqrt(saturate(1.0 - sinT2));
    float opd = 2.0 * n * thickness * cosT;
    // Divido entre λ en micrómetros (0,65 / 0,53 / 0,45) para tener la fase en "ciclos".
    float3 phase = opd / float3(0.65, 0.53, 0.45);
    // ½ + ½·cos lleva la oscilación de [−1, 1] a [0, 1], que es un color válido.
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
// Premultiplicado quiere decir que el color ya viene multiplicado por su propia opacidad.
// Gracias a eso puedo sumar brillos (sol, ventana) que se ven aunque la burbuja casi no
// tenga alfa, cosa que con la mezcla alfa clásica sería imposible.
half4 NOS_BubbleShade(float3 N, float3 V, float3 L, half3 viewN, float seed,
                      half thickness, half iridescence, half reflection,
                      half baseAlpha, half edgeAlpha, half specular, half windowHighlight, half3 tint)
{
    // Fresnel al cubo con multiplicaciones (más barato que pow): de frente rim = 0 y la
    // burbuja es casi invisible; de canto rim = 1 y se ve el contorno.
    half NdotV = saturate(dot(N, V));
    half rim = 1.0h - NdotV;
    half fresnel = rim * rim * rim;

    // Ruido 3D muestreado sobre la normal: así el patrón queda pegado a la esfera.
    // El seed mueve cada burbuja a otra zona del ruido y _Time.y·0,35 lo hace fluir despacio.
    float swirl = NOS_ValueNoise3D(N * 2.3 + float3(seed * 17.0, _Time.y * 0.35, seed * 5.0));
    // El grosor varía entre 0,55·d₀ y 1,45·d₀: lo suficiente para recorrer varias bandas de color.
    half3 film = NOS_ThinFilm(NdotV, thickness * (0.55 + 0.9 * swirl));

    // reflect(−V, N) da hacia dónde rebota mi línea de vista; con eso consulto el cielo.
    float3 R = reflect(-V, N);
    half3 sky = NOS_SkyGradient((half3)R, (half3)L);
    // Exponente 180: el destello solo aparece cuando R apunta casi exacto al sol (unos 5°).
    half sun = pow(saturate(dot(R, L)), 180.0h) * specular;

    // Reflejo de "ventana": viewN.xy es la normal vista desde la cámara (−1 a 1 en cada eje).
    // Resto el centro de la elipse (−0,38; 0,42 = arriba a la izquierda) y escalo cada eje
    // para estirarla. 1 − dot(w, w) es 1 en el centro y 0 en el borde de la elipse.
    half2 w1 = (viewN.xy - half2(-0.38h, 0.42h)) * half2(1.7h, 2.6h);
    half window = saturate(1.0h - dot(w1, w1));
    // Elevar al cuadrado endurece la caída para que parezca un reflejo con forma.
    window = window * window * windowHighlight;
    // Segundo brillo más pequeño abajo a la derecha, al 45 %, como luz rebotada.
    half2 w2 = (viewN.xy - half2(0.45h, -0.5h)) * 4.0h;
    half window2 = saturate(1.0h - dot(w2, w2)) * windowHighlight * 0.45h;
    // Los reflejos de ventana solo existen en la cara visible de la esfera (viewN.z > 0).
    half facing = saturate(viewN.z * 4.0h);
    window *= facing;
    window2 *= facing;

    // Color: reflejo del cielo e iridiscencia solo en el borde (·fresnel), más un tinte
    // tenue en toda la burbuja, más los brillos sumados encima.
    half3 color = (sky * reflection + film * iridescence) * fresnel + tint * baseAlpha;
    color += (sun * _NOS_SunTint.rgb) + (window + window2);
    // El alfa crece donde hay borde o brillo, para que esos puntos tapen un poco el fondo.
    half alpha = saturate(baseAlpha + fresnel * edgeAlpha + sun + window * 0.8h);
    return half4(color, alpha);
}

// -----------------------------------------------------------------------------
// Cáusticas
// -----------------------------------------------------------------------------

// Patrón de cáusticas: diferencia de dos campos de ruido que se desplazan en sentidos
// opuestos. Donde ambos valen casi lo mismo, |n₁ − n₂| ≈ 0, y 1 − |n₁ − n₂| elevado a una
// potencia alta deja solo esas líneas finas: una red de filamentos de luz que se deforma.
// El segundo ruido va a escala 1,27 y desplazado 7,3 para que no coincida con el primero;
// si usara la misma escala, los dos campos se verían como copias y la red no aparecería.
half NOS_Caustics(float2 p, float time)
{
    float n1 = NOS_ValueNoise2D(p + float2(time * 0.31, time * 0.17));
    float n2 = NOS_ValueNoise2D(p * 1.27 + float2(-time * 0.23, time * 0.29) + 7.3);
    half web = saturate((half)(1.0 - abs(n1 - n2)));
    // pow 10: con diferencia 0,05 → 0,95^10 ≈ 0,60; con 0,2 → 0,8^10 ≈ 0,11. Solo quedan hilos.
    web = pow(web, 10.0h);
    return web;
}

#endif // NOS_FRUTIGER_COMMON_INCLUDED

// =============================================================================
// NostalgIA — Entradas del shader NOS/AeroGloss
// Archivo: AeroGlossInput.hlsl
//
// Búfer por material incluido por TODOS los pases (Forward, ShadowCaster, DepthOnly y
// DepthNormals) para que el SRP Batcher vea el mismo UnityPerMaterial en cada uno.
//
// Por qué lo separé en su propio archivo: el SRP Batcher solo agrupa dibujos cuando el
// bloque UnityPerMaterial tiene exactamente las mismas variables, en el mismo orden, en
// todos los pases del shader. Si cada pase declarara su propia copia y alguien agregara
// una propiedad solo en el Forward, el batcher marcaría el shader como incompatible y
// cada isla volvería a costar una llamada de dibujo completa. Con un único include eso
// no puede pasar.
//
// Cómo funciona el SRP Batcher en dos líneas: los valores de cada material viven en un
// búfer constante que se queda guardado en la GPU. Mientras no cambie el material, Unity
// no vuelve a subirlos; entre un objeto y otro solo cambia la matriz del objeto. Por eso
// todo lo que se edita en el Inspector tiene que estar dentro de este bloque.
//
// half vs float: dejé en half (16 bits) los colores, intensidades y exponentes, porque
// en móvil se calculan más rápido y con menos registros, y 3 decimales sobran para un
// color. Dejé en float (32 bits) lo que se multiplica por posiciones de mundo en metros
// (_HeightRange y _CausticScale): con half, a 100 m del origen la precisión cae a
// ~6 cm y el gradiente o las cáusticas se verían escalonados. _SpeckleDensity también
// se multiplica por la posición, pero lo hago en float dentro del fragmento al promover
// la operación, así que su valor (3, 4…) cabe sin problema en half.
// =============================================================================

#ifndef NOS_AERO_GLOSS_INPUT_INCLUDED
#define NOS_AERO_GLOSS_INPUT_INCLUDED

// Core.hlsl trae las macros de URP (CBUFFER_START, TransformObjectToWorld, _Time…).
// Lo incluyo aquí para que los pases de sombra y profundidad, que no incluyen el
// Forward, también tengan todo lo necesario.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// CBUFFER_START(UnityPerMaterial) es el nombre exacto que busca el SRP Batcher.
// Ojo: las variables globales _NOS_* (nivel del agua, cielo) NO van aquí; viven en
// FrutigerCommon.hlsl y las escribe un script con Shader.SetGlobal*.
CBUFFER_START(UnityPerMaterial)
    half4 _ColorTop;          // Color en la parte alta (gradiente de altura).
    half4 _ColorBottom;       // Color en la parte baja.
    half4 _SideColor;         // Color de las pendientes (tierra / arena en las islas). Alfa = cuánto se mezcla.
    half4 _SpeckleColor;      // Motas (flores, brillos de pasto). Alfa = cuánto se mezcla (0 las apaga).
    half4 _RimColor;          // Color del borde iluminado por el cielo.
    half4 _EmissionColor;     // Emisión HDR (anclajes, núcleos brillantes). Valores > 1 alimentan el Bloom.
    float4 _HeightRange;      // x: altura mínima, y: altura máxima del gradiente (m). z y w no se usan.
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

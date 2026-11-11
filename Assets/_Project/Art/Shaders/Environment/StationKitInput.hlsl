// =============================================================================
// NostalgIA — Entradas del shader NOS/StationKit
// Archivo: StationKitInput.hlsl
//
// Aquí declaro las propiedades del material. Este archivo lo incluyen LOS CUATRO pases
// del shader (Forward, ShadowCaster, DepthOnly y DepthNormals) por una razón concreta:
// el SRP Batcher solo agrupa materiales si el bloque `UnityPerMaterial` es idéntico en
// todos los pases. Si un pase declarara una propiedad de más o de menos, el material
// caería a la ruta lenta (un cambio de estado de GPU por objeto).
//
// El shader no usa texturas: todo el patrón se calcula con la posición en el mundo. Por
// eso las piezas de ProBuilder del nivel no necesitan UV bien hechas.
// =============================================================================

#ifndef NOS_STATION_KIT_INPUT_INCLUDED
#define NOS_STATION_KIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Bloque de constantes por material. Primero los vectores (16 bytes cada uno) y al final
// los escalares, para que la GPU los empaquete en registros de 16 bytes sin huecos.
CBUFFER_START(UnityPerMaterial)
    half4 _WallColorTop;     // Color base de todas las caras (muros, piso y techo).
    half4 _LineColor;        // Color emisivo de la rejilla y del código.
    float _LineWidth;        // Ancho de las líneas de la rejilla, como fracción de celda.
    half _LineEmission;      // Multiplicador de brillo de la rejilla (arriba de 1 entra al Bloom).
    half _CodeDensity;       // Fracción de segmentos de "código" encendidos en los muros (0 a 1).
    half _CodeEmission;      // Multiplicador de brillo del código.
    float _ScanSpeed;        // Velocidad de la banda de escaneo que sube por los muros, en m/s.
    half _ScanEmission;      // Brillo de esa banda.
CBUFFER_END

#endif // NOS_STATION_KIT_INPUT_INCLUDED

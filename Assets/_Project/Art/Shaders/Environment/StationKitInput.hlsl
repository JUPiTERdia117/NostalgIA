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

// Bloque de constantes por material. Puse primero los vectores (16 bytes cada uno) y al
// final los escalares, para que la GPU los empaquete en registros de 16 bytes sin huecos.
CBUFFER_START(UnityPerMaterial)
    half4 _FloorColorA;      // Tono A de las baldosas del piso.
    half4 _FloorColorB;      // Tono B de las baldosas (cada baldosa elige un punto entre A y B).
    half4 _WallColorBottom;  // Color de la base de los muros.
    half4 _WallColorTop;     // Color de la parte alta de los muros.
    half4 _CeilingColor;     // Color de las caras que miran hacia abajo (techos).
    half4 _LineColor;        // Color emisivo de la rejilla y del código.
    float _GridScale;        // Celdas de rejilla por metro (1 = una celda por metro, como el kit).
    float _LineWidth;        // Ancho de las líneas de la rejilla, como fracción de celda.
    float _PixelsPerMeter;   // Resolución del grano pixelado (en Primavera, 32 px por metro).
    half _GrainStrength;     // Qué tanto se nota el grano sobre el color.
    half _LineEmission;      // Multiplicador de brillo de la rejilla (arriba de 1 entra al Bloom).
    half _CodeDensity;       // Fracción de segmentos de "código" encendidos en los muros (0 a 1).
    half _CodeEmission;      // Multiplicador de brillo del código.
    float _ScanSpeed;        // Velocidad de la banda de escaneo que sube por los muros, en m/s.
    half _ScanEmission;      // Brillo de esa banda.
    float _WallHeight;       // Altura en metros donde el muro ya es completamente _WallColorTop.
    half _Smoothness;        // Brillo especular (qué tan "pulido" se ve con la luz).
    half _DamagePulseTint;   // Cuánto se tiñe de rojo la rejilla cuando el jugador recibe daño (0 lo apaga).
CBUFFER_END

// Variable GLOBAL que escribe PostFXController con Shader.SetGlobalFloat cuando el jugador
// recibe daño. Al ser global no vive en el bloque por material y no rompe el SRP Batcher:
// un solo valor llega a todos los materiales del kit a la vez.
float _NOS_DamagePulse;

#endif // NOS_STATION_KIT_INPUT_INCLUDED

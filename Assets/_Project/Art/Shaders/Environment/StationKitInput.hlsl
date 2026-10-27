// =============================================================================
// NostalgIA — Entradas del shader NOS/StationKit
// Archivo: StationKitInput.hlsl
// Tarea: F1-B02 (biblioteca de materiales base por Estación)
//
// Este archivo declara el búfer de propiedades por material. Lo incluyen TODOS los
// pases del shader (Forward, ShadowCaster, DepthOnly y DepthNormals) para cumplir la
// regla del SRP Batcher: el contenido de `UnityPerMaterial` debe ser idéntico en cada
// pase; de lo contrario el material deja de ser compatible y se dibuja por la ruta
// lenta, con un SetPass por objeto (TDD §8.1.1, convención 1).
//
// El shader es completamente procedural (sin texturas): las piezas ProBuilder del
// nivel no necesitan UV coherentes porque el patrón se proyecta en espacio de mundo.
// =============================================================================

#ifndef NOS_STATION_KIT_INPUT_INCLUDED
#define NOS_STATION_KIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Búfer constante por material. Se agrupan los vectores al principio y los escalares
// al final para que el empaquetado en registros de 16 bytes no deje huecos.
CBUFFER_START(UnityPerMaterial)
    half4 _FloorColorA;      // Tono A de las baldosas del piso.
    half4 _FloorColorB;      // Tono B de las baldosas del piso (variación por celda).
    half4 _WallColorBottom;  // Color de la base de los muros (gradiente vertical).
    half4 _WallColorTop;     // Color de la parte alta de los muros.
    half4 _CeilingColor;     // Color de las caras orientadas hacia abajo.
    half4 _LineColor;        // Color emisivo de la rejilla y del código.
    float _GridScale;        // Celdas de rejilla por metro (1 = rejilla del kit de 1 m).
    float _LineWidth;        // Ancho de las líneas de la rejilla, en fracción de celda.
    float _PixelsPerMeter;   // Resolución del grano pixelado (guía §5.1: 32 px/m).
    half _GrainStrength;     // Intensidad del grano pixelado sobre el albedo.
    half _LineEmission;      // Multiplicador HDR de la emisión de la rejilla.
    half _CodeDensity;       // Fracción de segmentos de "código" encendidos en los muros (0–1).
    half _CodeEmission;      // Multiplicador HDR de los segmentos de código.
    float _ScanSpeed;        // Velocidad de la banda de escaneo vertical, en m/s.
    half _ScanEmission;      // Intensidad de la banda de escaneo.
    float _WallHeight;       // Altura en metros a la que el gradiente del muro llega a _WallColorTop.
    half _Smoothness;        // Brillo especular (Blinn-Phong).
    half _DamagePulseTint;   // Cuánto responde la rejilla al pulso global de daño (0 desactiva).
CBUFFER_END

// Propiedad global escrita por PostFXController (Shader.SetGlobalFloat). Al ser global
// no forma parte del búfer por material y no rompe el SRP Batcher (TDD §8.1.2).
float _NOS_DamagePulse;

#endif // NOS_STATION_KIT_INPUT_INCLUDED

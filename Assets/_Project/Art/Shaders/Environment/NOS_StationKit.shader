// =============================================================================
// NostalgIA — Shader NOS/StationKit
// Archivo: NOS_StationKit.shader
//
// Es el material base de las piezas modulares de las Estaciones (pisos, muros, techos
// de ProBuilder). Sustituye al gris por defecto de ProBuilder y le da al Tutorial su
// look Webcore: baldosas azules, rejilla de neón, código en los muros y una banda de
// escaneo que sube.
//
// El shader está repartido en tres archivos:
//   - NOS_StationKit.shader (este): propiedades del Inspector y los cuatro pases.
//   - StationKitInput.hlsl: el bloque de propiedades por material (UnityPerMaterial).
//   - StationKitForwardPass.hlsl: el patrón procedural y la iluminación.
//
// Las fórmulas principales (el detalle está en StationKitForwardPass.hlsl):
//   - Proyección por eje dominante: cada cara usa el plano de mundo hacia el que mira.
//   - Tono por baldosa: hash(floor(posición · escala)).
//   - Rejilla con antialiasing: 1 − saturate((|frac(p − 0,5) − 0,5| − ancho/2) / fwidth(p)).
//   - Banda de escaneo: exp(−8 · frac(0,1·y − 0,1·velocidad·t)).
//
// Rendimiento: todas las propiedades están en UnityPerMaterial (mismo bloque en los cuatro
// pases), así que el SRP Batcher agrupa todas las piezas. Si a algún objeto le pongo un
// MaterialPropertyBlock para variarle un color, ese objeto sale del SRP Batcher; lo
// reservo para casos puntuales.
// =============================================================================

Shader "NOS/StationKit"
{
    Properties
    {
        // Todos los colores y valores se pueden mover en el Inspector sin tocar código.
        [Header(Piso)]
        _FloorColorA ("Tono A del piso", Color) = (0.039, 0.165, 0.62, 1)
        _FloorColorB ("Tono B del piso", Color) = (0.024, 0.063, 0.169, 1)
        _CeilingColor ("Techo (caras hacia abajo)", Color) = (0.024, 0.063, 0.169, 1)

        [Header(Muros)]
        _WallColorBottom ("Base del muro", Color) = (0.549, 0.561, 0.588, 1)
        _WallColorTop ("Parte alta del muro", Color) = (0.039, 0.165, 0.62, 1)
        _WallHeight ("Altura del gradiente (m)", Float) = 6

        [Header(Rejilla y codigo)]
        [HDR] _LineColor ("Color emisivo de rejilla y código", Color) = (0.227, 0.42, 1, 1)
        _GridScale ("Celdas por metro", Float) = 1
        _LineWidth ("Ancho de línea (fracción de celda)", Range(0.005, 0.2)) = 0.03
        _LineEmission ("Emisión de la rejilla", Range(0, 4)) = 0.6
        _CodeDensity ("Densidad de código en muros", Range(0, 1)) = 0.35
        _CodeEmission ("Emisión del código", Range(0, 4)) = 0.5
        _ScanSpeed ("Velocidad de escaneo (m/s)", Float) = 1.5
        _ScanEmission ("Emisión de la banda de escaneo", Range(0, 2)) = 0.25

        [Header(Superficie)]
        _PixelsPerMeter ("Grano: píxeles por metro", Float) = 32
        _GrainStrength ("Intensidad del grano", Range(0, 0.2)) = 0.04
        _Smoothness ("Brillo", Range(0, 1)) = 0.3
        _DamagePulseTint ("Respuesta al pulso de daño", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
            "IgnoreProjector" = "True"
            "Queue" = "Geometry"
        }
        LOD 300

        // ---------------------------------------------------------------------
        // Pase principal: iluminación Forward / Forward+.
        // ---------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5                       // Pido shader model 3.5 porque los hashes PCG usan enteros sin signo.
            #pragma vertex StationKitVertex
            #pragma fragment StationKitFragment

            // Variantes de URP: copié el mismo conjunto que usa SimpleLit (sombras, luces
            // adicionales, lightmaps, niebla, instancing) para que el kit reciba luz igual que
            // cualquier material estándar. No agregué variantes propias para no multiplicar la
            // cantidad de shaders compilados.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Assets/_Project/Art/Shaders/Environment/StationKitInput.hlsl"
            #include "Assets/_Project/Art/Shaders/Environment/StationKitForwardPass.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        // Sombras: reutilizo el pase estándar de URP. Como el kit es opaco y sin recortes,
        // no necesito nada especial; solo incluyo mi Input para que el bloque de propiedades coincida.
        // ---------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Assets/_Project/Art/Shaders/Environment/StationKitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        // Solo profundidad: la usa URP para el depth priming y para efectos de pantalla en PC.
        // ---------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Assets/_Project/Art/Shaders/Environment/StationKitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        // Profundidad y normales: lo pide el SSAO del renderer de PC.
        // ---------------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #pragma multi_compile_instancing

            #include "Assets/_Project/Art/Shaders/Environment/StationKitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    // Si la GPU no soporta shader model 3.5 (OpenGL ES 2), URP muestra su shader de error magenta.
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

// =============================================================================
// NostalgIA — Shader NOS/StationKit
// Archivo: NOS_StationKit.shader
// Tarea: F1-B02 — biblioteca de materiales base por Estación; sustituye al material
// gris por defecto de ProBuilder en las escenas de Estación (Guía de estilo §6).
//
// Fórmulas principales (detalle en StationKitForwardPass.hlsl):
//   proyección triplanar dura por eje dominante, tono por celda = hash(floor(p·escala)),
//   rejilla con antialiasing: 1 − saturate((|frac(p−0,5)−0,5| − ancho/2) / fwidth(p)),
//   banda de escaneo: exp(−8·frac(0,1·y − 0,1·v·t)).
//
// Compatibilidad: SRP Batcher (todas las propiedades en UnityPerMaterial, mismo búfer
// en los cuatro pases) y MaterialPropertyBlock (cualquier propiedad puede variar por
// renderer, a costa de sacar ese objeto del SRP Batcher; TDD §8.1.2).
// =============================================================================

Shader "NOS/StationKit"
{
    Properties
    {
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
            #pragma target 3.5                       // Se requieren enteros sin signo para los hashes PCG.
            #pragma vertex StationKitVertex
            #pragma fragment StationKitFragment

            // Palabras clave de URP (mismo conjunto que SimpleLit, sin opciones de material).
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
        // Proyección de sombras: usamos el pase estándar de URP (sin alpha test).
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
        // Profundidad: necesaria para depth priming y efectos de pantalla en PC.
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
        // Profundidad y normales: requerido por SSAO (renderer de PC).
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

    // Si la GPU no admite target 3.5 (OpenGL ES 2), URP usa su shader de error.
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

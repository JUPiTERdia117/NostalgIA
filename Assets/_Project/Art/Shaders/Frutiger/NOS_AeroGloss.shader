// =============================================================================
// NostalgIA — Shader NOS/AeroGloss
// Superficie de plástico brillante Frutiger Aero (detalle en AeroGlossForward.hlsl).
// Compatible con SRP Batcher (mismo UnityPerMaterial en los cuatro pases) y con GPU
// Instancing. Proyecta y recibe sombras de la luz principal.
//
// Estructura en 3 archivos:
//   - NOS_AeroGloss.shader: propiedades del Inspector y los 4 pases de URP (ForwardLit,
//     ShadowCaster, DepthOnly y DepthNormals).
//   - AeroGlossInput.hlsl: el bloque CBUFFER_START(UnityPerMaterial) único que incluyen
//     los 4 pases para que el SRP Batcher nunca pierda compatibilidad.
//   - AeroGlossForward.hlsl: toda la matemática de color, pendientes, motas, iluminación
//     envuelta, doble especular Blinn-Phong + Clearcoat, rim Fresnel y cáusticas.
// =============================================================================

Shader "NOS/AeroGloss"
{
    Properties
    {
        [Header(Color)]
        _ColorTop ("Color alto", Color) = (0.49, 0.85, 0.34, 1)
        _ColorBottom ("Color bajo", Color) = (0.18, 0.72, 0.55, 1)
        _HeightRange ("Rango de altura (x min, y max)", Vector) = (0, 6, 0, 0)
        _SideColor ("Color de pendiente (a = mezcla)", Color) = (0.96, 0.89, 0.66, 1)
        _SlopeSharpness ("Nitidez de pendiente", Range(0.5, 12)) = 4
        _SpeckleColor ("Motas (a = mezcla)", Color) = (1, 1, 1, 0)
        _SpeckleDensity ("Motas por metro", Float) = 3
        _SpeckleAmount ("Cantidad de motas", Range(0, 1)) = 0.08

        [Header(Brillo)]
        _Gloss ("Brillo", Range(0, 1)) = 0.55
        _ClearcoatStrength ("Brillo plástico", Range(0, 3)) = 0.8
        _ClearcoatPower ("Nitidez del brillo plástico", Range(16, 2048)) = 400
        _Wrap ("Envoltura de la difusa", Range(0, 1)) = 0.15
        _RimColor ("Color de borde", Color) = (0.75, 0.93, 1, 1)
        _RimPower ("Exponente de borde", Range(0.5, 8)) = 3
        _RimStrength ("Intensidad de borde", Range(0, 2)) = 0.45

        [Header(Emision)]
        [HDR] _EmissionColor ("Emisión", Color) = (0, 0, 0, 1)
        _EmissionPulse ("Pulso de emisión (Hz)", Range(0, 4)) = 0

        [Header(Opciones)]
        [Toggle(_FLAT_SHADING)] _FlatShading ("Sombreado plano (low poly)", Float) = 0
        [Toggle(_CAUSTICS)] _Caustics ("Cáusticas bajo el agua", Float) = 0
        _CausticStrength ("Intensidad de cáusticas", Range(0, 3)) = 1.2
        _CausticScale ("Escala de cáusticas", Float) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "IgnoreProjector" = "True" }
        LOD 300

        // Pase 1: dibujo principal con sol, armónicos esféricos, brillos y cáusticas.
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex AeroGlossVertex
            #pragma fragment AeroGlossFragment

            #pragma shader_feature_local _FLAT_SHADING
            #pragma shader_feature_local _CAUSTICS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Assets/_Project/Art/Shaders/Frutiger/AeroGlossInput.hlsl"
            #include "Assets/_Project/Art/Shaders/Frutiger/AeroGlossForward.hlsl"
            ENDHLSL
        }

        // Pase 2: proyecta sombras de las islas y edificios en el mapa de sombras del sol.
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
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Assets/_Project/Art/Shaders/Frutiger/AeroGlossInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Pase 3: llena _CameraDepthTexture para que NOS/AeroWater calcule la profundidad del agua y la espuma.
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
            #pragma multi_compile_instancing
            #include "Assets/_Project/Art/Shaders/Frutiger/AeroGlossInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // Pase 4: llena profundidad y normales si algún efecto de cámara (SSAO) lo solicita en PC.
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
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #pragma multi_compile_instancing
            #include "Assets/_Project/Art/Shaders/Frutiger/AeroGlossInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

// Copyright (c) 2016 Unity Technologies. MIT license - license_unity.txt
// #NVJOB NX Shaders / SC Shaders (Legacy). MIT license - license_nvjob.txt
// #NVJOB Nicholas Veselov - https://nvjob.github.io
// Versión URP para NostalgIA: misma lógica que el surface shader original,
// reescrita en HLSL para URP. La iluminación está en Simple Boids/NX_URP.hlsl.

// Fondo bajo el agua: la oclusión se desliza en dos direcciones para simular la luz que atraviesa las olas.
Shader "#NVJOB/SC Shaders (Legacy)/For Water"
{
    Properties
    {
        [Header(Basic Settings)][Space(5)]
        _Color("Main Color", Color) = (1,1,1,1)
        _MainTex("Base (RGB) Gloss (A)", 2D) = "white" {}
        _Saturation("Saturation", Range(0, 5)) = 1
        _Brightness("Brightness", Range(0, 5)) = 1
        _Contrast("Contrast", Range(0, 5)) = 1
        [Header(Specular Settings)][Space(5)]
        _SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
        _Shininess("Shininess", Range(0.03, 1)) = 0.078125
        [Header(Occlusion Settings)][Space(5)]
        _OcclusionMap("Occlusionmap", 2D) = "white" {}
        _OcclusionMapUv("Occlusionmap Uv", Range(0.01, 50)) = 1
        _IntensityOc("Intensity Occlusion", Range(-20, 20)) = 1
        _SpeedOcclusion("Speed Occlusion", float) = 1.0
        [Header(Normalmap Settings)][Space(5)]
        _BumpMap("Normalmap", 2D) = "bump" {}
        _IntensityNm("Intensity Normalmap", Range(-20, 20)) = 1
        [Header(Reflection Settings)][Space(5)]
        _ReflectColor("Reflection Color", Color) = (1,1,1,0.5)
        _Cube("Reflection Cubemap", Cube) = "" {}
        _IntensityRef("Intensity Reflection", Range(0, 20)) = 1
        _BiasNormal("Bias Normal", Range(-5, 5)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #pragma target 3.5
        #define _SPECULAR_COLOR      // Activa el brillo especular de Blinn-Phong en URP.
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _BumpMap_ST;
            half4 _Color, _SpecColor, _ReflectColor;
            half _Saturation, _Brightness, _Contrast, _Shininess;
            half _IntensityNm, _IntensityRef, _BiasNormal;
            half _OcclusionMapUv, _IntensityOc, _SpeedOcclusion;
        CBUFFER_END

        #define NX_SC
        #define NX_SC_WATER
        #include "../../../Simple Boids/NX_URP.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex NXVert
            #pragma fragment NXFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex NXShadowVert
            #pragma fragment NXDepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex NXDepthVert
            #pragma fragment NXDepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex NXDepthVert
            #pragma fragment NXDepthNormalsFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}

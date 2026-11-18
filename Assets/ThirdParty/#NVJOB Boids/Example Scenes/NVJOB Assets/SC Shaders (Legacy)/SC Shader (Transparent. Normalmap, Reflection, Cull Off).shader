// Copyright (c) 2016 Unity Technologies. MIT license - license_unity.txt
// #NVJOB NX Shaders / SC Shaders (Legacy). MIT license - license_nvjob.txt
// #NVJOB Nicholas Veselov - https://nvjob.github.io
// Versión URP para NostalgIA: misma lógica que el surface shader original,
// reescrita en HLSL para URP. La iluminación está en Simple Boids/NX_URP.hlsl.

// Transparente con mezcla alfa. No escribe profundidad, así que no tiene pases de profundidad.
Shader "#NVJOB/SC Shaders (Legacy)/Transparent, Normalmap, Reflection, Cull Off"
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
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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
        CBUFFER_END

        #define NX_SC
        #define NX_SC_TRANSPARENT
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
    }
}

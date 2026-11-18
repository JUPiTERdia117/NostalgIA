// Copyright (c) 2016 Unity Technologies. MIT license - license_unity.txt
// #NVJOB Simple Boids. MIT license - license_nvjob.txt
// #NVJOB NX Shaders. MIT license - license_nvjob.txt
// #NVJOB Nicholas Veselov - https://nvjob.github.io
// #NVJOB Simple Boids v1.1.1 - https://nvjob.github.io/unity/nvjob-boids
// Versión URP para NostalgIA: misma lógica que el surface shader original,
// reescrita en HLSL para URP. La iluminación está en NX_URP.hlsl.

// Pez de los cardúmenes: mueve los vértices para que nade (cola y cuerpo ondulan)
// y se ilumina con Blinn-Phong, reflejo de cubemap y normal de detalle.
Shader "#NVJOB/NX Shaders/Boids/Fish"
{
    Properties
    {
        [Header(Basic Settings)][Space(5)]
        [HDR]_Color("Main Color", Color) = (1,1,1,1)
        _MainTex("Base (RGB) Gloss (A)", 2D) = "white" {}
        _Saturation("Saturation", Range(0, 5)) = 1
        _Brightness("Brightness", Range(0, 5)) = 1
        _Contrast("Contrast", Range(0, 5)) = 1
        [Header(Specular Settings)][Space(5)]
        _SpecMap("Base (RGB) Gloss (A)", 2D) = "white" {}
        _SpecMapUV("Specularmap Uv", float) = 1
        [HDR]_SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
        _Shininess("Shininess", Range(0.03, 2)) = 0.078125
        _SpecMapInts("Intensity SpecularMap", Range(0, 5)) = 1
        [Header(Occlusion Settings)][Space(5)]
        _OcclusionMap("Occlusionmap", 2D) = "white" {}
        _OcclusionMapUv("Occlusionmap Uv", Range(0.01, 50)) = 1
        _IntensityOc("Intensity Occlusion", Range(-20, 20)) = 1
        [Header(Normalmap Settings)][Space(5)]
        _BumpMap("Normalmap", 2D) = "bump" {}
        _IntensityNm("Intensity Normalmap", Range(-20, 20)) = 1
        _BumpMapD("Normalmap Detail", 2D) = "bump" {}
        _BumpMapDUV("Normalmap Detail Uv", float) = 1
        _IntensityNmD("Intensity Normalmap Detail", Range(-20, 20)) = 1
        [Header(Reflection Settings)][Space(5)]
        [HDR]_ReflectColor("Reflection Color", Color) = (1,1,1,0.5)
        _EmissionTex("Emission (RGB) Gloss (A)", 2D) = "white" {}
        _Cube("Reflection Cubemap", Cube) = "" {}
        _IntensityRef("Intensity Reflection", Range(0, 20)) = 1
        _SaturationRef("Saturation Reflection", Range(0, 5)) = 1
        _ContrastRef("Contrast Reflection", Range(0, 5)) = 1
        _BiasNormal("Bias Normal", Range(-5, 5)) = 1
        [Header(Swimming Fish)][Space(5)]
        _SwimmingPower("Swimming Power", Range(0, 50)) = 1
        _SwimmingScale("Swimming Scale", Range(0.001, 50)) = 1
        _SwimmingSpeed("Swimming Speed", Range(0, 50)) = 2
        _WaveBody("Wave Body", Range(-10, 10)) = 0.3
        _Wobble("Wobble", Range(-50, 50)) = 1
        _WaveY("Wave Y", Range(0, 30)) = 0.3
        _WaveYSpeed("Wave Y Speed", Range(0, 30)) = 1
        [HideInInspector] _ScriptControl("Script Control", float) = 0
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

        // Mismo bloque en todos los pases para que el SRP Batcher agrupe a los peces.
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _BumpMap_ST;
            half4 _Color, _SpecColor, _ReflectColor;
            half _Saturation, _Brightness, _Contrast;
            half _SpecMapUV, _Shininess, _SpecMapInts;
            half _OcclusionMapUv, _IntensityOc;
            half _IntensityNm, _BumpMapDUV, _IntensityNmD;
            half _IntensityRef, _SaturationRef, _ContrastRef, _BiasNormal;
            half _SwimmingPower, _SwimmingScale, _SwimmingSpeed, _WaveBody, _Wobble, _WaveY, _WaveYSpeed, _ScriptControl;
        CBUFFER_END

        // Nado: el eje Z del modelo va de la cabeza a la cola.
        //   flap = sin((t + desfase) · velocidad) · fuerza   → qué tanto se dobla en este instante.
        //   zf   = z² − z · WaveBody                        → la cola se mueve más que la cabeza.
        // La altura del pez en el mundo cambia el desfase, así no aletean todos a la vez.
        float3 FishSwim(float3 p)
        {
            float3 origin = TransformObjectToWorld(float3(1, 1, 1));
            float t = _Time.y;
            float flap = sin((t + sin(origin.y * 0.5) + _ScriptControl) * _SwimmingSpeed) * _SwimmingPower;
            float zf = p.z * p.z - p.z * _WaveBody;
            p.x += sin(zf / _SwimmingScale) * flap;                                // Cola de lado a lado.
            p.y += sin(zf / 10.0) * flap * _Wobble;                                // Cuerpo que se tambalea.
            p.y += sin((t + sin((origin.x + origin.y) * _WaveY * 0.6)) * _WaveYSpeed) * _WaveY;  // Sube y baja.
            return p;
        }
        #define NX_DEFORM(p) p = FishSwim(p)
        #define NX_BOIDS
        #include "NX_URP.hlsl"
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

        // Sin pase de sombras a propósito: son muchos peces pequeños y la sombra casi no se nota.
        // (El original solo tenía la sombra rígida de su Fallback, sin aleteo).
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

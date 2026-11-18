// Copyright (c) 2016 Unity Technologies. MIT license - license_unity.txt
// #NVJOB Simple Boids. MIT license - license_nvjob.txt
// #NVJOB NX Shaders. MIT license - license_nvjob.txt
// #NVJOB Nicholas Veselov - https://nvjob.github.io
// #NVJOB Simple Boids v1.1.1 - https://nvjob.github.io/unity/nvjob-boids
// Versión URP para NostalgIA: misma lógica que el surface shader original,
// reescrita en HLSL para URP. La iluminación está en NX_URP.hlsl.

// Ave o mariposa de las parvadas: mueve los vértices para que aletee
// y se ilumina igual que el pez (Blinn-Phong, cubemap y normal de detalle).
Shader "#NVJOB/NX Shaders/Boids/Bird"
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
        [Header(Bird Flapping)][Space(5)]
        _FlappingSpeed("Flapping Speed", Range(0, 50)) = 10
        _FlappYPower("Flapping Y Power", Range(0, 50)) = 2
        _FlappYOffset("Flapping Y Offset", Range(-15, 15)) = 0.1
        _FlappXPower("Flapping X Power", Range(0, 50)) = 1
        _FlappXOffset("Flapping X Offset", Range(-15, 15)) = 0.1
        _FlappXCenter("Flapping X Center Indent", Range(0, 15)) = 0.1
        _FlappZPower("Flapping Z Power", Range(-10, 10)) = 0.1
        _WaveY("Wave Y", Range(0, 30)) = 0
        _WaveYSpeed("Wave Y Speed", Range(0, 30)) = 1
        [Toggle(BUTTERFLY)]
        _FillWithRed("Butterfly", Float) = 0
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
            half _Saturation, _Brightness, _Contrast;
            half _SpecMapUV, _Shininess, _SpecMapInts;
            half _OcclusionMapUv, _IntensityOc;
            half _IntensityNm, _BumpMapDUV, _IntensityNmD;
            half _IntensityRef, _SaturationRef, _ContrastRef, _BiasNormal;
            half _FlappingSpeed, _FlappYPower, _FlappYOffset, _FlappXPower, _FlappXOffset, _FlappXCenter, _FlappZPower, _WaveY, _WaveYSpeed;
        CBUFFER_END

        // Aleteo: el eje X del modelo va de ala a ala, así que |x| es la distancia al cuerpo.
        //   flap = sin(y/5) · (y + offset) · ala · cos((t + desfase) · velocidad)
        // Las aves doblan el ala con |x|³ (la punta se mueve mucho más que la base);
        // la mariposa usa |x|/2 (el ala completa se mueve como una placa).
        float3 BirdFlap(float3 p)
        {
            float3 origin = TransformObjectToWorld(float3(1, 1, 1));
            float t = _Time.y;
            float yf = p.y + _FlappYOffset;
            float xf = abs(p.x) + _FlappXOffset;
            #ifdef BUTTERFLY
                xf *= 0.5;
                float phase = 0.2;
            #else
                xf = xf * xf * xf;
                float phase = 0.5;
            #endif
            float flap = sin(p.y / 5.0) * yf * xf * cos((t + sin(origin.y * phase)) * _FlappingSpeed);
            p.y += flap * _FlappYPower;
            if (abs(p.x) > _FlappXCenter) p.x -= flap * _FlappXPower * p.x;   // Las alas se recogen al subir.
            p.z += flap * _FlappZPower;
            if (_WaveY > 0) p.y += sin((t + cos((origin.x + origin.z) * _WaveY * 0.1) * 3.0) * _WaveYSpeed) * _WaveY;
            return p;
        }
        #define NX_DEFORM(p) p = BirdFlap(p)
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
            #pragma shader_feature_local_vertex BUTTERFLY
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            ENDHLSL
        }

        // Sin pase de sombras a propósito, igual que el pez (muchos objetos pequeños).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex NXDepthVert
            #pragma fragment NXDepthFrag
            #pragma shader_feature_local_vertex BUTTERFLY
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
            #pragma shader_feature_local_vertex BUTTERFLY
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}

// #NVJOB Dynamic Sky Lite. MIT license - license_nvjob.txt
// #NVJOB Nicholas Veselov - https://nvjob.github.io
// Versión URP para NostalgIA: misma lógica que el surface shader original,
// reescrita en HLSL para URP. No usa luces: el color sale tal cual (era "Nolight").

// Nubes: mezcla tres texturas de ruido (canales R, G y B) que se mueven a distinta
// velocidad con el desplazamiento global del cielo (_SkyShaderUv*, lo escribe el script).
Shader "#NVJOB/Dynamic Sky Lite (Standard)/Clouds Type 2"
{
    Properties
    {
        [Header(R Channel)]
        _TextureUvR("R Uv Scale", Float) = 1
        _IntensityR("R Intensity", Range(-10, 10)) = 1.5
        _TextureVR("R Motion Vector", Vector) = (0.9, 1, 0, 0)
        [NoScaleOffset]_TextureR("Texture R", 2D) = "white" {}
        [Space(5)][Header(G Channel)]
        _TextureUvG("G Uv Scale", Float) = 1
        _IntensityG("G Intensity", Range(-10, 10)) = 1.5
        _TextureVG("G Motion Vector", Vector) = (1.3, 1.2, 0, 0)
        [NoScaleOffset]_TextureG("Texture G", 2D) = "white" {}
        [Space(5)][Header(B Channel)]
        _TextureUvB("B Uv Scale", Float) = 1
        _IntensityB("B Intensity", Range(-10, 10)) = -0.5
        _TextureVB("B Motion Vector", Vector) = (-1, -1, 0, 0)
        [NoScaleOffset]_TextureB("Texture B", 2D) = "white" {}
        [Space(5)][Header(General)]
        _Color("Main Color", Color) = (1,1,1,1)
        _IntensityInput("Intensity Input", Range(-20, 20)) = 1.6
        _Fluffiness("Fluffiness", Range(-1, 1)) = 0.75
        _IntensityOutput("Intensity Output", Range(-20, 20)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+500" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _TextureVR, _TextureVG, _TextureVB;
                half _IntensityR, _IntensityG, _IntensityB, _IntensityInput, _IntensityOutput, _Fluffiness;
                half _TextureUvR, _TextureUvG, _TextureUvB;
            CBUFFER_END

            // Desplazamiento del cielo: lo escribe cada frame el script (variables globales).
            half _SkyShaderUvX, _SkyShaderUvZ;

            TEXTURE2D(_TextureR); SAMPLER(sampler_TextureR);
            TEXTURE2D(_TextureG); SAMPLER(sampler_TextureG);
            TEXTURE2D(_TextureB); SAMPLER(sampler_TextureB);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv - 1.0;           // Igual que el original: UV de 0..1 a −1..0.
                return output;
            }

            // Una capa de nube: su textura, movida en su propia dirección.
            half Layer(TEXTURE2D_PARAM(tex, samp), float2 uv, half2 motion, half scale, int channel)
            {
                uv += float2(_SkyShaderUvX * motion.x, _SkyShaderUvZ * motion.y);
                return SAMPLE_TEXTURE2D(tex, samp, uv * scale)[channel];
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 clouds = _Color;
                clouds *= Layer(TEXTURE2D_ARGS(_TextureR, sampler_TextureR), input.uv, _TextureVR.xy, _TextureUvR, 0) * _IntensityR;
                clouds *= Layer(TEXTURE2D_ARGS(_TextureG, sampler_TextureG), input.uv, _TextureVG.xy, _TextureUvG, 1) * _IntensityG;
                clouds *= Layer(TEXTURE2D_ARGS(_TextureB, sampler_TextureB), input.uv, _TextureVB.xy, _TextureUvB, 2) * _IntensityB;
                clouds *= _IntensityInput;

                // "Fluffiness" sube o baja el contraste; normalize redondea el color como en el original.
                half4 color = normalize((clouds - 0.5) * _Fluffiness + 0.5) * _IntensityOutput;
                return half4(color.rgb, clouds.a);
            }
            ENDHLSL
        }
    }
}

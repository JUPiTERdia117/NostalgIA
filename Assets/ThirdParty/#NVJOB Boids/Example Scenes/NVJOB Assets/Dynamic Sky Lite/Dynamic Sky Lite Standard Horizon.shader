// #NVJOB Dynamic Sky Lite. MIT license - license_nvjob.txt
// #NVJOB Nicholas Veselov - https://nvjob.github.io
// Versión URP para NostalgIA: misma lógica que el surface shader original,
// reescrita en HLSL para URP. No usa luces: el color sale tal cual (era "Nolight").

// Horizonte: degradado vertical en el mundo entre dos alturas (abajo y arriba).
Shader "#NVJOB/Dynamic Sky Lite (Standard)/Horizon"
{
    Properties
    {
        [Header(Top Horizon)]
        _Level1Color("Color", Color) = (0.65,0.86,0.63,1)
        _Level1("Height Level", Float) = 10
        [Space(5)]
        [Header(Bottom Horizon)]
        _Level0Color("Color", Color) = (0.37,0.78,0.92,1)
        _Level0("Height Level", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+501" "IgnoreProjector" = "True" }
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
                half4 _Level0Color, _Level1Color;
                float _Level0, _Level1;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS : SV_POSITION; float heightWS : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.heightWS = positionWS.y;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // El original dejaba el color sin definir entre las dos alturas; aquí se mezcla
                // de forma continua: abajo de Level0 → color de abajo, arriba de Level1 → color de arriba.
                float t = saturate((input.heightWS - _Level0) / (_Level1 - _Level0));
                return lerp(_Level0Color, _Level1Color, t);
            }
            ENDHLSL
        }
    }
}

// =============================================================================
// NostalgIA — Shader NOS/AeroGodRay
// Haces de luz volumétricos falsos: conos abiertos aditivos que bajan del cielo.
//   frente:   pow(|N·V|, 2)           → el haz brilla en el centro y se desvanece en la silueta
//   altura:   v^1,5·smoothstep(0, 0,15, 1 − v) (UV.y = 0 abajo, 1 arriba): intenso arriba,
//             tenue al llegar al suelo, sin borde duro en la base
//   estrías:  0,55 + 0,45·ruido(ángulo·20, t·0,2) → rayos dentro del haz que cambian lento
//   cercanía: saturate((distancia − 8) / 20) → se apaga al acercarse o atravesarlo con la cámara
//
// Por qué usamos conos aditivos en vez de niebla volumétrica por raymarching:
//   - El raymarching volumétrico real exige decenas de muestras de sombra por píxel y hunde
//     los FPS en móvil. Con una malla cónica semiabierta (FR_GodRay) y este producto de 4
//     máscaras suaves logramos los rayos solares de Frutiger Aero a costo de un solo pase simple.
// =============================================================================

Shader "NOS/AeroGodRay"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.97, 0.85, 1)
        _Intensity ("Intensidad", Range(0, 2)) = 0.22
        _StreakScale ("Estrías", Float) = 20
        _StreakSpeed ("Velocidad de estrías", Float) = 0.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardAdditive"
            Tags { "LightMode" = "UniversalForward" }
            // Mezcla aditiva (One One): suma luz pura al fondo y no depende del orden de dibujo.
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RayVertex
            #pragma fragment RayFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Include/NostalgIA_Common.hlsl"

            // Bloque UnityPerMaterial para compatibilidad con SRP Batcher.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                float _StreakScale;
                float _StreakSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings RayVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 RayFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 toCamera = GetCameraPositionWS() - input.positionWS;
                float distance = length(toCamera);
                half3 V = (half3)(toCamera / max(distance, 0.001));
                // Al revés que Fresnel: aquí quiero que el borde de la malla cónica valga 0 para que
                // nunca se note la arista geométrica del cono. |N·V|² vale 1 en el centro y 0 en el canto.
                half facing = abs(dot(normalize(input.normalWS), V));
                facing *= facing;

                half v = (half)input.uv.y;
                // v^1,5 atenúa hacia abajo (v = 0) y smoothstep(0, 0,15, 1 − v) suaviza el corte superior (v = 1).
                half height = pow(saturate(v), 1.5h) * smoothstep(0.0h, 0.15h, 1.0h - v);
                half streaks = (half)(0.55 + 0.45 * NOS_ValueNoise2D(float2(input.uv.x * _StreakScale, _Time.y * _StreakSpeed)));
                // Entre 28 m y 8 m de la cámara se apaga suavemente para no cegar al jugador si cruza el haz.
                half nearFade = (half)saturate((distance - 8.0) / 20.0);

                half3 color = _Color.rgb * facing * height * streaks * nearFade * _Intensity;
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, 0.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

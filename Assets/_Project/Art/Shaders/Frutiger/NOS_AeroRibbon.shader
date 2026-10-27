// =============================================================================
// NostalgIA — Shader NOS/AeroRibbon
// Cintas de luz tipo aurora de los fondos de pantalla Aero: tiras curvas con un degradado
// que fluye a lo largo, filamentos finos y bordes suaves. Aditivo (no necesita orden).
//   a lo ancho:  (1 − |2v − 1|)²            → máximo en el centro de la cinta
//   a lo largo:  smoothstep en los extremos  → la cinta aparece y desaparece sin cortes
//   flujo:       f = u·repeticiones − t·velocidad
//   brillo:      0,6 + 0,4·sin(2π·f + 3v)
//   filamentos:  pow(|sin(40v + 3f)|, 24)    → hilos de luz que se desplazan
//   color:       paleta coseno a lo largo de la cinta (cian → blanco → lima)
// =============================================================================

Shader "NOS/AeroRibbon"
{
    Properties
    {
        [HDR] _ColorA ("Color inicial", Color) = (0.2, 0.9, 1, 1)
        [HDR] _ColorB ("Color final", Color) = (0.55, 1, 0.45, 1)
        _Intensity ("Intensidad", Range(0, 4)) = 1.2
        _FlowSpeed ("Velocidad de flujo", Float) = 0.15
        _Repeat ("Repeticiones del flujo", Float) = 3
        _Filaments ("Filamentos", Range(0, 2)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardAdditive"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RibbonVertex
            #pragma fragment RibbonFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Include/NostalgIA_Common.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                half _Intensity;
                float _FlowSpeed;
                float _Repeat;
                half _Filaments;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fogFactor : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings RibbonVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 RibbonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float u = input.uv.x;
                float v = input.uv.y;
                half across = (half)(1.0 - abs(v * 2.0 - 1.0));
                across *= across;
                half along = (half)(smoothstep(0.0, 0.1, u) * smoothstep(1.0, 0.9, u));
                float flow = u * _Repeat - _Time.y * _FlowSpeed;
                half shimmer = (half)(0.6 + 0.4 * sin(NOS_TWO_PI * flow + v * 3.0));
                half filaments = (half)pow(abs(sin(v * 40.0 + flow * 3.0)), 24.0) * _Filaments;

                half3 gradient = lerp(_ColorA.rgb, _ColorB.rgb, (half)u);
                gradient = lerp(gradient, half3(1, 1, 1), (half)(0.35 * pow(across, 4.0h)));
                half3 color = gradient * (across * shimmer + filaments * across) * along * _Intensity;
                // En aditivo, la niebla atenúa la contribución hacia negro.
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, 0.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

// =============================================================================
// NostalgIA — Shader NOS/AeroFoam
// Anillo de espuma alrededor de cada isla (malla en forma de corona al nivel del agua).
// Funciona también sin textura de profundidad (móvil), a diferencia de la espuma de NOS/AeroWater.
//   UV.x = ángulo alrededor de la isla (0–1), UV.y = distancia radial (0 en la orilla, 1 afuera)
//   banda:   (1 − v)²                         → espuma densa en la orilla
//   olas:    pow(saturate(sin(2π·(v·4 − t·0,35))), 6)·(1 − v) → crestas concéntricas que se alejan
//   rotura:  ruido(u·60, v·4 − t·0,5) > umbral → bordes deshilachados
// =============================================================================

Shader "NOS/AeroFoam"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Opacity ("Opacidad", Range(0, 1)) = 0.85
        _WaveSpeed ("Velocidad de las crestas", Float) = 0.35
        _Breakup ("Rotura", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-40" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardFoam"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FoamVertex
            #pragma fragment FoamFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Include/NostalgIA_Common.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Opacity;
                float _WaveSpeed;
                half _Breakup;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fogFactor : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings FoamVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 FoamFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float u = input.uv.x;
                float v = input.uv.y;
                float t = _Time.y;
                half band = (half)((1.0 - v) * (1.0 - v));
                half crests = (half)(pow(saturate(sin(NOS_TWO_PI * (v * 4.0 - t * _WaveSpeed))), 6.0) * (1.0 - v));
                float n = NOS_ValueNoise2D(float2(u * 60.0, v * 4.0 - t * 0.5));
                half breakup = (half)step(_Breakup, n * 0.6 + (1.0 - v) * 0.6);
                half alpha = saturate(band * 0.8h + crests) * breakup * _Opacity;
                half3 color = MixFog(_Color.rgb, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

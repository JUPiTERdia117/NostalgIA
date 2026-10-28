// =============================================================================
// NostalgIA — Shader NOS/AeroFoam
// Anillo de espuma alrededor de cada isla (malla en forma de corona al nivel del agua).
// Funciona también sin textura de profundidad (móvil), a diferencia de la espuma de NOS/AeroWater.
//   UV.x = ángulo alrededor de la isla (0–1), UV.y = distancia radial (0 en la orilla, 1 afuera)
//   banda:   (1 − v)²                         → espuma densa en la orilla
//   olas:    pow(saturate(sin(2π·(v·4 − t·0,35))), 6)·(1 − v) → crestas concéntricas que se alejan
//   rotura:  ruido(u·60, v·4 − t·0,5) > umbral → bordes deshilachados
//
// Por qué existe este shader si NOS/AeroWater ya tiene espuma por profundidad:
//   - En móvil apagamos _CameraDepthTexture para ahorrar ancho de banda, así que la espuma
//     por profundidad de NOS/AeroWater queda en 0. Este anillo de malla (FR_FoamRing) rodea
//     cada isla justo a ras del agua y garantiza que siempre se vea una orilla blanca viva
//     tanto en PC como en teléfonos de 4 GB de RAM.
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
        // Transparent-40 = cola 2960: se dibuja justo después del agua (2950) y antes que
        // las burbujas (3000), para que se mezcle encima de la superficie del mar.
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

            // Bloque UnityPerMaterial para compatibilidad con SRP Batcher: todas las coronas
            // de espuma de las islas comparten el mismo material y se agrupan sin cambiar estado.
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
                // u recorre la vuelta completa a la isla (0 a 1); v va de la arena (0) hacia mar adentro (1).
                float u = input.uv.x;
                float v = input.uv.y;
                float t = _Time.y;
                // Banda pegada a la orilla: (1 − v)² cae rápido (en v = 0,5 ya vale 0,25 y en v = 1 llega a 0).
                half band = (half)((1.0 - v) * (1.0 - v));
                // Crestas concéntricas: v·4 mete 4 anillos en el ancho de la corona; restar t·_WaveSpeed
                // hace que los anillos viajen desde la orilla hacia afuera. Elevar a la 6ª potencia adelgaza
                // los anillos para que queden líneas finas de espuma, y (1 − v) los apaga al alejarse.
                half crests = (half)(pow(saturate(sin(NOS_TWO_PI * (v * 4.0 - t * _WaveSpeed))), 6.0) * (1.0 - v));
                // Ruido estirado 60 veces a lo largo del perímetro para recortar agujeros orgánicos en la espuma.
                float n = NOS_ValueNoise2D(float2(u * 60.0, v * 4.0 - t * 0.5));
                // Cerca de la orilla (1 − v grande) es casi seguro que supere _Breakup; lejos solo sobreviven
                // las manchas donde el ruido n es alto.
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

// =============================================================================
// NostalgIA — Shader NOS/AeroRainbow
// Arcoíris procedural sobre un cuadro: el radio desde el centro inferior del cuadro decide
// el tono. Con r = |uv − (0,5; 0)|·2 y la banda [r₀, r₁]:
//   t = (r − r₀)/(r₁ − r₀)  → tono por paleta coseno de fase (0; 0,33; 0,67), de rojo a violeta
//   máscara = smoothstep en ambos bordes de la banda
//   el arco se desvanece hacia los pies (uv.y bajo) y titila con un leve brillo que recorre la
//   banda. Aditivo y suave para no tapar el cielo.
// =============================================================================

Shader "NOS/AeroRainbow"
{
    Properties
    {
        _InnerRadius ("Radio interior", Range(0, 1)) = 0.72
        _OuterRadius ("Radio exterior", Range(0, 1)) = 0.92
        _Intensity ("Intensidad", Range(0, 2)) = 0.45
        _Softness ("Suavidad del borde", Range(0.001, 0.2)) = 0.04
    }

    SubShader
    {
        // Transparent-100 = cola 2900: se dibuja antes del agua y de las cintas para integrarse
        // como parte del cielo del horizonte.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardAdditive"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RainbowVertex
            #pragma fragment RainbowFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Include/NostalgIA_Common.hlsl"

            // Bloque UnityPerMaterial para compatibilidad con SRP Batcher.
            CBUFFER_START(UnityPerMaterial)
                half _InnerRadius;
                half _OuterRadius;
                half _Intensity;
                half _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings RainbowVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 RainbowFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Centro del semicírculo en (0,5; 0) = mitad de la base del Quad. Multiplico X por 2
                // para que u ∈ [0, 1] cubra [−1, 1], igual que v ∈ [0, 1] cubre de la base hasta arriba.
                float2 p = (input.uv - float2(0.5, 0.0)) * float2(2.0, 1.0);
                half r = (half)length(p);
                // t normaliza la posición dentro del grosor del arcoíris: 0 en el borde interior, 1 en el exterior.
                half t = saturate((r - _InnerRadius) / max(_OuterRadius - _InnerRadius, 0.001h));
                // Doble smoothstep: sube de 0 a 1 al entrar al radio interior y baja de 1 a 0 al salir del exterior.
                half band = smoothstep(_InnerRadius, _InnerRadius + _Softness, r) * smoothstep(_OuterRadius, _OuterRadius - _Softness, r);
                // Paleta coseno: t = 0 rojo … t = 1 violeta (fase desplazada para empezar en rojo).
                half3 hue = NOS_CosinePalette(t * 0.8h, half3(0.5h, 0.5h, 0.5h), half3(0.5h, 0.5h, 0.5h), half3(1.0h, 1.0h, 1.0h), half3(0.0h, 0.33h, 0.67h));
                // Desvanece las dos patas inferiores del arco cerca del agua (uv.y < 0,35).
                half feet = smoothstep(0.0h, 0.35h, (half)input.uv.y);
                half twinkle = (half)(0.85 + 0.15 * sin(_Time.y * 0.8 + input.uv.x * 6.0));
                return half4(hue * band * feet * twinkle * _Intensity, 0.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

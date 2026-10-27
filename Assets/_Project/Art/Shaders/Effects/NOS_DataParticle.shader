// =============================================================================
// NostalgIA — Shader NOS/DataParticle
// Archivo: NOS_DataParticle.shader
// Pilar 2 (TDD §8.2): material aditivo propio para las partículas reactivas de la
// herramienta y de los coleccionables (vórtice de succión, destello de salida,
// chispas de anclaje, columna de datos del checkpoint).
//
// Cada partícula se dibuja como un "píxel de datos":
//   - Modo cuadro (_GlyphMix = 0): cuadrado con núcleo brillante y halo
//     I(d) = saturate(1 − d / r)^k, con d la distancia de Chebyshev al centro
//     (max(|x|,|y|)), que produce un brillo cuadrado coherente con la estética
//     pixelada en lugar de un círculo suave.
//   - Modo bit (_GlyphMix = 1): dígito «0» o «1» de 3×5 píxeles codificado como
//     máscara de 15 bits; el dígito lo decide StableRandom.x de cada partícula,
//     transmitido por un custom vertex stream (TEXCOORD0.z), así que no cambia
//     durante la vida de la partícula.
//   - Parpadeo: la intensidad oscila con 0,75 + 0,25·sin(ωt + 2π·aleatorio).
//
// Mezcla aditiva (Blend One One) sin escritura de profundidad: el orden de dibujo
// no importa y no hay ordenamiento por partícula. Compatible con SRP Batcher.
// =============================================================================

Shader "NOS/DataParticle"
{
    Properties
    {
        [HDR] _TintColor ("Tinte", Color) = (1, 1, 1, 1)
        _Intensity ("Intensidad", Range(0, 8)) = 1.5
        _GlyphMix ("Cuadro (0) / Bit 0-1 (1)", Range(0, 1)) = 0
        _HaloSharpness ("Nitidez del halo", Range(0.5, 8)) = 2.5
        _FlickerSpeed ("Velocidad de parpadeo", Float) = 18
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One      // Aditivo: color final = destino + fuente.
            ZWrite Off         // Las partículas no ocluyen a otras ni a la geometría posterior.
            Cull Off           // Los billboards pueden verse por ambas caras.

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DataParticleVertex
            #pragma fragment DataParticleFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Include/NostalgIA_Common.hlsl"

            // Propiedades por material (SRP Batcher).
            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                half _Intensity;
                half _GlyphMix;
                half _HaloSharpness;
                float _FlickerSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;   // Esquina del billboard, ya orientada por Shuriken.
                half4 color       : COLOR;      // Color por partícula (Color over Lifetime).
                float3 texcoord   : TEXCOORD0;  // xy: UV del cuadro; z: StableRandom.x (custom vertex stream).
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color       : COLOR;
                float3 uvRandom   : TEXCOORD0;  // UV y número aleatorio estable.
                half fogFactor    : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DataParticleVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz); // Las partículas usan espacio de mundo: la matriz es la identidad.
                output.color = input.color;
                output.uvRandom = input.texcoord;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Devuelve 1 si el píxel (x, y) de la cuadrícula 3×5 pertenece al dígito.
            // Máscaras (fila superior primero, bit 14 = esquina superior izquierda):
            //   «0»: 111 101 101 101 111 → 0x7B6F      «1»: 010 110 010 010 111 → 0x2C97
            half DigitMask(float2 uv, bool one)
            {
                int2 cell = int2(floor(uv * float2(3.0, 5.0)));           // Columna 0–2 y fila 0–4 (fila 0 abajo).
                if (any(cell < 0) || cell.x > 2 || cell.y > 4) return 0.0h;
                uint mask = one ? 0x2C97u : 0x7B6Fu;
                uint bitIndex = (uint)(14 - ((4 - cell.y) * 3 + cell.x)); // Fila superior/columna izquierda = bit 14.
                return (half)((mask >> bitIndex) & 1u);
            }

            half4 DataParticleFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 centered = input.uvRandom.xy * 2.0 - 1.0;             // UV en [−1, 1].
                float chebyshev = max(abs(centered.x), abs(centered.y));     // Distancia "cuadrada" al centro.
                half halo = (half)pow(saturate(1.0 - chebyshev), _HaloSharpness); // Brillo que decae hacia el borde.

                // Dígito ocupando el 60 % central del cuadro.
                float2 glyphUV = (input.uvRandom.xy - 0.2) / 0.6;
                half glyph = DigitMask(glyphUV, input.uvRandom.z > 0.5);

                half shape = lerp(halo, glyph, _GlyphMix);                   // Mezcla entre cuadro y bit.
                float phase = NOS_TWO_PI * input.uvRandom.z;                 // Fase aleatoria por partícula.
                half flicker = 0.75h + 0.25h * (half)sin(_Time.y * _FlickerSpeed + phase);

                half3 color = _TintColor.rgb * input.color.rgb * (shape * flicker * _Intensity * input.color.a);
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor); // En aditivo, la niebla atenúa hacia negro.
                return half4(color, 0.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

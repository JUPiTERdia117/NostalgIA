// =============================================================================
// NostalgIA — Shader NOS/DataParticle
// Archivo: NOS_DataParticle.shader
//
// Es el material de las partículas "de datos": el vórtice de la aspiradora, el destello
// al disparar, las chispas del gancho al anclarse y la columna de datos del checkpoint.
// Quería que cada partícula se viera como un píxel o un bit de información, no como el
// círculo difuso típico.
//
// Cada partícula es un cuadro (billboard) y este shader decide qué se dibuja dentro:
//   - Modo cuadro (_GlyphMix = 0): un cuadrado con núcleo brillante y halo,
//     I(d) = saturate(1 − d)^k, donde d es la distancia de Chebyshev al centro,
//     max(|x|, |y|). Con la distancia normal (circular) saldría un círculo; con Chebyshev
//     las curvas de igual brillo son cuadrados, que va con la estética pixelada.
//   - Modo bit (_GlyphMix = 1): un «0» o un «1» de 3×5 píxeles guardado en 15 bits.
//     Qué dígito sale lo decide un número aleatorio fijo de cada partícula (StableRandom.x),
//     que el sistema de partículas manda en TEXCOORD0.z con un custom vertex stream. Como
//     es fijo, el dígito no cambia mientras la partícula vive.
//   - Parpadeo: la intensidad oscila con 0,75 + 0,25·sin(ω·t + 2π·aleatorio); el desfase
//     aleatorio evita que todas parpadeen al mismo tiempo.
//
// Mezcla aditiva (Blend One One) sin escribir profundidad: el color se suma a lo que ya
// hay detrás, así que el orden en que se dibujan las partículas no importa y no hay que
// ordenarlas (ese ordenamiento cuesta CPU). Compatible con SRP Batcher.
// =============================================================================

Shader "NOS/DataParticle"
{
    Properties
    {
        [HDR] _TintColor ("Tinte", Color) = (1, 1, 1, 1)                      // Color base; se multiplica por el color de cada partícula.
        _Intensity ("Intensidad", Range(0, 8)) = 1.5                         // Arriba de 1 la partícula alimenta el Bloom.
        _GlyphMix ("Cuadro (0) / Bit 0-1 (1)", Range(0, 1)) = 0              // Valores intermedios mezclan las dos formas.
        _HaloSharpness ("Nitidez del halo", Range(0.5, 8)) = 2.5              // Exponente k: más alto = núcleo más chico y halo más corto.
        _FlickerSpeed ("Velocidad de parpadeo", Float) = 18                    // En rad/s: 18 son unos 3 parpadeos por segundo.
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

            Blend One One      // Aditivo: color final = lo que ya había + esta partícula.
            ZWrite Off         // No escriben profundidad: una partícula nunca tapa a otra.
            Cull Off           // Dibujo las dos caras del cuadro por si el billboard queda volteado.

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DataParticleVertex
            #pragma fragment DataParticleFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Include/NostalgIA_Common.hlsl"

            // Propiedades por material, todas en UnityPerMaterial para el SRP Batcher.
            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                half _Intensity;
                half _GlyphMix;
                half _HaloSharpness;
                float _FlickerSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;   // Esquina del cuadro; el sistema de partículas ya la orienta hacia la cámara.
                half4 color       : COLOR;      // Color de cada partícula (por ejemplo, el de Color over Lifetime).
                float3 texcoord   : TEXCOORD0;  // xy: UV del cuadro; z: StableRandom.x (custom vertex stream).
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color       : COLOR;
                float3 uvRandom   : TEXCOORD0;  // xy: UV del cuadro; z: número aleatorio fijo de la partícula.
                half fogFactor    : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DataParticleVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // Las partículas se simulan en espacio de mundo y su matriz de objeto es la identidad,
                // así que esta transformación equivale a mundo → pantalla.
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uvRandom = input.texcoord;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Devuelve 1 si el píxel (x, y) de la cuadrícula de 3×5 forma parte del dígito.
            // Cada dígito son 15 píxeles encendidos/apagados guardados como bits de un entero
            // (fila de arriba primero; el bit 14 es la esquina superior izquierda):
            //   «0»: 111 101 101 101 111 → 0x7B6F      «1»: 010 110 010 010 111 → 0x2C97
            half DigitMask(float2 uv, bool one)
            {
                int2 cell = int2(floor(uv * float2(3.0, 5.0)));           // Columna 0–2 y fila 0–4 (la fila 0 es la de abajo).
                if (any(cell < 0) || cell.x > 2 || cell.y > 4) return 0.0h;
                uint mask = one ? 0x2C97u : 0x7B6Fu;
                uint bitIndex = (uint)(14 - ((4 - cell.y) * 3 + cell.x)); // Fila de arriba y columna izquierda → bit 14; fila de abajo y columna derecha → bit 0.
                return (half)((mask >> bitIndex) & 1u);                    // Muevo ese bit a la posición 0 y lo leo.
            }

            half4 DataParticleFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 centered = input.uvRandom.xy * 2.0 - 1.0;             // Paso las UV de [0, 1] a [−1, 1]: el centro queda en (0, 0).
                float chebyshev = max(abs(centered.x), abs(centered.y));     // Distancia "cuadrada": 0 en el centro, 1 en el borde del cuadro.
                half halo = (half)pow(saturate(1.0 - chebyshev), _HaloSharpness); // 1 en el centro, 0 en el borde; el exponente decide qué tan rápido cae.

                // El dígito ocupa el 60 % central del cuadro: (uv − 0,2)/0,6 lleva [0,2; 0,8] a [0, 1].
                float2 glyphUV = (input.uvRandom.xy - 0.2) / 0.6;
                half glyph = DigitMask(glyphUV, input.uvRandom.z > 0.5);

                half shape = lerp(halo, glyph, _GlyphMix);                   // _GlyphMix = 0 → cuadro; 1 → dígito.
                float phase = NOS_TWO_PI * input.uvRandom.z;                 // Desfase aleatorio de 0 a 2π por partícula.
                half flicker = 0.75h + 0.25h * (half)sin(_Time.y * _FlickerSpeed + phase); // Oscila entre 0,5 y 1,0.

                // Multiplico todo; el alfa de la partícula funciona como atenuador porque en aditivo no hay "transparencia".
                half3 color = _TintColor.rgb * input.color.rgb * (shape * flicker * _Intensity * input.color.a);
                // En aditivo, la niebla tiene que llevar el color hacia negro (sumar 0 no cambia nada);
                // si la llevara al color de la niebla, las partículas lejanas aclararían el horizonte.
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, 0.0h);                                   // El alfa no se usa con Blend One One.
            }
            ENDHLSL
        }
    }
    FallBack Off
}

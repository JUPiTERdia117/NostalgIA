// NostalgIA — Lava de código (Tutorial)
// Piso letal: una alberca azul con código de terminal que baja, una capa de código
// tenue detrás (más lenta, parece más lejana) y líneas de barrido tipo monitor CRT.
// Es Unlit: el brillo del código lo multiplica _GlowIntensity y el Bloom hace el resto.

Shader "Tutorial/BlueCodeLava"
{
    Properties
    {
        _BaseColor ("Pool Blue Color", Color) = (0.02, 0.25, 0.95, 1.0)
        _DeepColor ("Deep Blue Vignette", Color) = (0.01, 0.12, 0.65, 1.0)
        _CodeColor ("Code Text Color", Color) = (0.92, 0.97, 1.0, 1.0)
        _GlowIntensity ("Code Glow Intensity", Range(0.5, 5.0)) = 2.0
        _ScrollSpeed ("Scroll Speed (Down)", Float) = 1.6
        _WorldScale ("Code Scale (World Units)", Float) = 5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off   // Se ve por las dos caras.

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _DeepColor;
                float4 _CodeColor;
                float _GlowIntensity;
                float _ScrollSpeed;
                float _WorldScale;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            // 16 caracteres de 5×5 píxeles; cada uno guardado en los 25 bits de un entero.
            static const uint GLYPHS[16] =
            {
                0x0E8B52E, 0x04C421F, 0x1F09C2F, 0x1F0985F,  // 0 1 2 3
                0x1F87C1F, 0x0E8FE31, 0x0F8420F, 0x1E8FA5E,  // E A C B
                0x0E8422E, 0x1040100, 0x1151151, 0x1F87221,  // ( ; / P
                0x1F1111F, 0x0F8383E, 0x1F21084, 0x0000840   // D S T .
            };

            // Número "aleatorio" en [0, 1) que siempre es el mismo para la misma celda.
            float Hash(float2 p)
            {
                float3 p3 = frac(p.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // 1 si el punto `local` (0 a 1 dentro de la celda) cae en un píxel encendido del carácter.
            // La celda se parte en 7×7 y el carácter ocupa el 5×5 del centro (el borde separa letras).
            float Glyph(uint id, float2 local)
            {
                int2 g = int2(floor(local * 7.0)) - 1;
                if (any(g < 0) || any(g > 4)) return 0;
                return (GLYPHS[id & 15] >> ((4 - g.y) * 5 + (4 - g.x))) & 1;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float scroll = _Time.y * _ScrollSpeed;
                float2 grid = input.uv * _WorldScale * float2(28.0, 56.0);   // Rejilla de caracteres.

                // Capa de enfrente: se lee como bloques de código.
                float2 p = grid + float2(0.0, scroll * 8.0);
                float2 cell = floor(p);
                float row = Hash(float2(cell.y, 1.0));
                float col = fmod(abs(cell.x), 36.0);                                          // El diseño se repite cada 36 columnas.
                float indent = floor(Hash(float2(floor(cell.y * 0.2), 2.0)) * 3.0) * 4.0 + 9.0; // Sangría compartida por 5 filas.
                float lineEnd = indent + floor(Hash(float2(cell.y, 3.0)) * 18.0);
                bool lineNumber = col >= 1.0 && col <= 5.0 && row > 0.25;
                bool lineBody = col >= indent && col <= lineEnd && row > 0.12;
                float r = Hash(cell);
                float front = ((lineNumber || lineBody) && r > 0.16) ? Glyph(uint(r * 64.0), frac(p)) : 0.0;

                // Capa de atrás: dispersa, tenue y más lenta (paralaje).
                float2 q = grid + float2(3.5, scroll * 4.5);
                float rb = Hash(floor(q) + 19.3);
                float back = (rb > 0.72 && Hash(float2(floor(q.y), 4.0)) > 0.4) ? Glyph(uint(rb * 32.0), frac(q)) * 0.22 : 0.0;

                // Fondo azul con líneas de barrido y un pulso lento.
                float2 w = input.uv * _WorldScale * 10.0;
                float wave = (0.93 + 0.07 * sin((w.y * 80.0 + scroll * 20.0) * PI))
                           * (0.88 + 0.12 * sin(_Time.y * 2.5 + w.x * 0.8));

                float3 color = lerp(_DeepColor.rgb, _BaseColor.rgb, wave);
                color = lerp(color, _CodeColor.rgb * 0.65, back);
                color = lerp(color, _CodeColor.rgb * _GlowIntensity, front);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}

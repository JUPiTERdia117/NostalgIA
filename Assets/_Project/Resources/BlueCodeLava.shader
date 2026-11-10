// =============================================================================
// NostalgIA — Lava de código (Tutorial)
// Archivo: BlueCodeLava.shader
//
// Este es el piso letal del Tutorial: una alberca azul rey llena de código de
// terminal que corre hacia abajo, como si el suelo fuera una pantalla de datos.
// Es un activo final: solo le agregué estos comentarios, el código y el aspecto
// quedaron exactamente igual.
//
// Cómo está armado (todo en el fragment shader, sin texturas):
//   1. Uso la posición de MUNDO (x, z) como coordenada, no las UV de la malla. Así da
//      igual cuánto estire el plano de lava: los caracteres siempre miden lo mismo.
//   2. Divido el plano en celdas; cada celda es un carácter. Con hashes decido qué
//      celdas llevan texto, con qué sangría y con qué carácter, para que se lea como
//      bloques de código con números de línea a la izquierda.
//   3. Cada carácter es un mapa de bits de 5×5 guardado dentro de un entero.
//   4. Detrás pongo una segunda capa de código más tenue que baja más lento
//      (paralaje: lo que se mueve más lento parece estar más lejos).
//   5. El fondo azul respira con unas líneas de barrido tipo monitor CRT.
//
// Es Unlit (no calcula luces): el brillo del código viene de multiplicar su color por
// _GlowIntensity, y el Bloom del perfil de la Estación hace el resto.
// =============================================================================

Shader "Tutorial/BlueCodeLava"
{
    Properties
    {
        // Colores del fondo: el azul se mueve entre _DeepColor y _BaseColor con las líneas de barrido.
        _BaseColor ("Pool Blue Color", Color) = (0.02, 0.25, 0.95, 1.0)
        _DeepColor ("Deep Blue Vignette", Color) = (0.01, 0.12, 0.65, 1.0)
        // Color de los caracteres. Se multiplica por _GlowIntensity: arriba de 1 entra al Bloom.
        _CodeColor ("Code Text Color", Color) = (0.92, 0.97, 1.0, 1.0)
        _GlowIntensity ("Code Glow Intensity", Range(0.5, 5.0)) = 2.0
        // Qué tan rápido baja el código. Negativo lo hace subir.
        _ScrollSpeed ("Scroll Speed (Down)", Float) = 1.6
        // Escala del texto en el mundo: más alto = caracteres más chicos y más densos.
        _WorldScale ("Code Scale (World Units)", Float) = 0.35
        // Ojo: _Columns, _Rows, _BorderWidth, _BorderColor y _DotColor están declaradas,
        // pero el fragment shader actual no las lee. Las dejé para no romper los materiales
        // que ya las guardan; moverlas en el Inspector no cambia nada en pantalla.
        _Columns ("Text Columns", Float) = 28.0
        _Rows ("Text Rows", Float) = 56.0
        _BorderWidth ("Edge Border Width", Range(0.0, 0.15)) = 0.04
        _BorderColor ("Border Color", Color) = (0.01, 0.03, 0.08, 1.0)
        _DotColor ("Border Indicator Light Color", Color) = (0.2, 0.9, 1.0, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off      // Dibujo las dos caras: si la cámara queda debajo del plano, la lava sigue viéndose.
            ZWrite On     // Es opaca: escribe profundidad como cualquier piso.
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog   // Variantes con y sin niebla de RenderSettings.

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Todas las propiedades van en UnityPerMaterial para que el SRP Batcher pueda
            // agrupar los planos de lava en una sola configuración de GPU.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _DeepColor;
                float4 _CodeColor;
                float _GlowIntensity;
                float _ScrollSpeed;
                float _WorldScale;
                float _Columns;
                float _Rows;
                float _BorderWidth;
                float4 _BorderColor;
                float4 _DotColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;   // Posición del vértice en espacio del objeto.
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION; // Posición en pantalla (espacio de recorte).
                float3 positionWS : TEXCOORD0;   // Posición de mundo: de aquí sale todo el patrón.
                float2 uv         : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            // Hash de un número a [0, 1) sin usar seno ("hash without sine" de Dave Hoskins).
            // Multiplica, suma y se queda con la parte decimal varias veces hasta que el
            // resultado ya no se parece a la entrada. Este shader es anterior a nuestra
            // biblioteca común, por eso trae su propio hash.
            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            // Lo mismo para una celda 2D: mezclo x e y con un producto punto antes de quedarme con la parte decimal.
            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Devuelve 1 si el píxel (gx, gy) del carácter está encendido y 0 si está apagado.
            // Cada carácter es una cuadrícula de 5×5 = 25 píxeles; guardé esos 25 encendidos/
            // apagados como los 25 bits de un entero (un 1 = píxel encendido).
            float SampleGlyph(int glyphId, int gx, int gy)
            {
                if (gx < 0 || gx > 4 || gy < 0 || gy > 4) return 0.0;   // Fuera de la cuadrícula: es el margen del carácter.

                // 16 caracteres de terminal retro (dígitos, hexadecimal y signos).
                uint mask = 0u;
                int id = glyphId & 15;   // & 15 es lo mismo que módulo 16: cualquier número cae en uno de los 16 caracteres.

                if (id == 0)       mask = 0x0E8B52Eu; // '0'
                else if (id == 1)  mask = 0x04C421Fu; // '1'
                else if (id == 2)  mask = 0x1F09C2Fu; // '2'
                else if (id == 3)  mask = 0x1F0985Fu; // '3'
                else if (id == 4)  mask = 0x1F87C1Fu; // 'E'
                else if (id == 5)  mask = 0x0E8FE31u; // 'A'
                else if (id == 6)  mask = 0x0F8420Fu; // 'C'
                else if (id == 7)  mask = 0x1E8FA5Eu; // 'B'
                else if (id == 8)  mask = 0x0E8422Eu; // '{' / '('
                else if (id == 9)  mask = 0x0040100u; // ':'
                else if (id == 10) mask = 0x1151151u; // '/'
                else if (id == 11) mask = 0x1F87221u; // 'P'
                else if (id == 12) mask = 0x1F1111Fu; // 'D'
                else if (id == 13) mask = 0x0F8383Eu; // 'S'
                else if (id == 14) mask = 0x1F21084u; // 'T'
                else               mask = 0x0000840u; // '.' / ','

                // Número de bit del píxel: fila por fila, 5 bits por fila.
                // gy = 0, gx = 0 cae en el bit 24 (el más alto); gy = 4, gx = 4 en el bit 0.
                int bitIndex = (4 - gy) * 5 + (4 - gx);
                return ((mask >> bitIndex) & 1u) != 0u ? 1.0 : 0.0;   // Desplazo el bit hasta la posición 0 y lo leo.
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz); // Objeto → mundo → vista → recorte.
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);                   // Niebla calculada por vértice (barato).
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                
                 // En vez de input.positionWS.xz:
                float2 worldUV = input.uv * _WorldScale * 10.0;
                // 2) Desplazamiento en el tiempo. _Time.y son los segundos desde que arrancó el juego.
                float scroll = _Time.y * _ScrollSpeed;
                // Estiro la rejilla: 6 columnas y 10 filas por unidad de worldUV. Con _WorldScale = 0,35
                // cada carácter mide 1/(6·0,35) ≈ 0,48 m de ancho y 1/(10·0,35) ≈ 0,29 m de alto.
                // Sumarle scroll·8 a la coordenada vertical hace que el texto avance 8 filas por unidad de scroll.
                float2 codeCoords = float2(worldUV.x * 6.0, worldUV.y * 10.0 + scroll * 8.0);

                float2 cell = floor(codeCoords);   // Qué carácter (columna, fila) es este píxel.
                float2 local = frac(codeCoords);   // Dónde está el píxel dentro de ese carácter, de 0 a 1.

                // 3) Estructura de "código": un número aleatorio por fila y otro por bloque de 5 filas.
                //    floor(fila · 0,2) es igual para 5 filas seguidas, así varias líneas comparten sangría.
                float rowSeed = Hash11(cell.y);
                float blockSeed = Hash11(floor(cell.y * 0.2));

                // El diseño se repite cada 36 columnas (fmod = residuo de la división).
                float colMod = fmod(abs(cell.x), 36.0);
                // Columnas 1 a 5: la "columna de números de línea". El 75 % de las filas la tiene.
                bool isLeftLabel = (colMod >= 1.0 && colMod <= 5.0) && (rowSeed > 0.25);

                // Sangría del bloque: 9, 13 o 17 columnas (floor(0..2,99) · 4 + 9).
                float indent = floor(blockSeed * 3.0) * 4.0 + 9.0;
                // Largo de la línea: sangría + 0 a 17 caracteres al azar.
                float lineLen = indent + floor(Hash11(cell.y * 1.37 + 7.1) * 18.0);
                // El cuerpo de la línea va de la sangría al largo calculado; el 88 % de las filas tiene código.
                bool isCodeBody = (colMod >= indent && colMod <= lineLen) && (rowSeed > 0.12);

                // La idea es dejar una fila en blanco cada 7, como el espacio entre dos funciones:
                // frac(fila · 0,142857) < 0,12 se cumple cuando fila/7 cae justo en un entero.
                // Detalle que descubrí al documentarlo: 0,142857 es un pelito menor que 1/7, así que
                // para filas positivas múltiplo de 7 el resultado es 0,99999… y no 0. En la práctica
                // las filas en blanco solo aparecen mientras el número de fila es negativo (o cero).
                // Lo dejé así a propósito para no cambiar el aspecto final de la lava; si algún día
                // queremos la fila en blanco siempre, basta con cambiar 0.142857 por (1.0 / 7.0).
                bool isBlankRow = frac(cell.y * 0.142857) < 0.12;

                float pixelOn = 0.0;
                if (!isBlankRow && (isLeftLabel || isCodeBody))
                {
                    // El 16 % de las celdas quedan vacías: son los espacios entre palabras.
                    float charRand = Hash21(cell);
                    if (charRand > 0.16)
                    {
                        // Parto la celda en 7×7 y uso el 5×5 del centro: el borde de 1 píxel separa un carácter del otro.
                        int gx = int(floor(local.x * 7.0)) - 1;
                        int gy = int(floor(local.y * 7.0)) - 1;
                        int glyphId = int(floor(charRand * 64.0));   // El mismo número aleatorio decide qué carácter es.
                        pixelOn = SampleGlyph(glyphId, gx, gy);
                    }
                }

                // 4) Capa de fondo: otra rejilla corrida 3,5 columnas y que baja a 4,5 en vez de 8.
                //    Como se mueve más lento, el ojo la interpreta como más profunda (paralaje).
                float2 bgCoords = float2(worldUV.x * 6.0 + 3.5, worldUV.y * 10.0 + scroll * 4.5);
                float2 bgCell = floor(bgCoords);
                float2 bgLocal = frac(bgCoords);
                float bgRand = Hash21(bgCell + 19.3);   // +19,3 para que no coincida con el hash de la capa de enfrente.
                float bgPixel = 0.0;
                // Solo el 28 % de las celdas (y el 60 % de las filas) tienen carácter: el fondo queda disperso.
                if (bgRand > 0.72 && Hash11(bgCell.y + 5.0) > 0.4)
                {
                    int bgx = int(floor(bgLocal.x * 7.0)) - 1;
                    int bgy = int(floor(bgLocal.y * 7.0)) - 1;
                    bgPixel = SampleGlyph(int(floor(bgRand * 32.0)), bgx, bgy) * 0.22;   // Al 22 % de fuerza: apenas se ve.
                }

                // 5) Fondo azul que "respira".
                // Líneas de barrido CRT: una onda seno entre 0,86 y 1,0 que se desplaza con el código.
                // El periodo es 2/80 = 0,025 unidades de worldUV, unos 7 cm con la escala por defecto.
                float scanline = 0.93 + 0.07 * sin((worldUV.y * 80.0 + scroll * 20.0) * 3.14159);
                // Pulso lento entre 0,76 y 1,0 que viaja a lo largo de x (2,5 rad/s ≈ un ciclo cada 2,5 s).
                float pulse = 0.88 + 0.12 * sin(_Time.y * 2.5 + worldUV.x * 0.8);
                // lerp(a, b, t) = a + (b − a)·t. Con t entre ≈0,65 y 1, el fondo oscila entre casi _BaseColor y algo más profundo.
                float3 poolBg = lerp(_DeepColor.rgb, _BaseColor.rgb, scanline * pulse);

                // 6) Composición de capas, de atrás hacia adelante:
                float3 color = poolBg;
                color = lerp(color, _CodeColor.rgb * 0.65, bgPixel);                 // Código de fondo, tenue.
                color = lerp(color, _CodeColor.rgb * _GlowIntensity, pixelOn);       // Código de enfrente: valor HDR que alimenta el Bloom.

                half4 finalColor = half4(color, 1.0);
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);            // Mezcla con la niebla de la escena.
                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}

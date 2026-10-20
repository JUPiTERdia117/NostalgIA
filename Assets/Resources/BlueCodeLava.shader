Shader "Tutorial/BlueCodeLava"
{
    Properties
    {
        _BaseColor ("Pool Blue Color", Color) = (0.02, 0.25, 0.95, 1.0)
        _DeepColor ("Deep Blue Vignette", Color) = (0.01, 0.12, 0.65, 1.0)
        _CodeColor ("Code Text Color", Color) = (0.92, 0.97, 1.0, 1.0)
        _GlowIntensity ("Code Glow Intensity", Range(0.5, 5.0)) = 2.0
        _ScrollSpeed ("Scroll Speed (Down)", Float) = 1.6
        _WorldScale ("Code Scale (World Units)", Float) = 0.35
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

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Procedural 5x5 bitmap glyph generator resembling retro terminal code/hex/assembly
            float SampleGlyph(int glyphId, int gx, int gy)
            {
                if (gx < 0 || gx > 4 || gy < 0 || gy > 4) return 0.0;

                // 16 distinct retro code character bitmaps (25-bit patterns)
                uint mask = 0u;
                int id = glyphId & 15;

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

                int bitIndex = (4 - gy) * 5 + (4 - gx);
                return ((mask >> bitIndex) & 1u) != 0u ? 1.0 : 0.0;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Use world-space XZ so any scaled Lava plane has crisp, undistorted code lines scrolling downwards
                float2 worldUV = input.positionWS.xz * _WorldScale;

                // Scroll downwards along the hallway/canyon axis
                float scroll = _Time.y * _ScrollSpeed;
                float2 codeCoords = float2(worldUV.x * 6.0, worldUV.y * 10.0 + scroll * 8.0);

                float2 cell = floor(codeCoords);
                float2 local = frac(codeCoords);

                // Create structured indentation & code blocks like the reference image
                float rowSeed = Hash11(cell.y);
                float blockSeed = Hash11(floor(cell.y * 0.2));

                // Left column (line numbers / hex labels) + indented code blocks on right
                float colMod = fmod(abs(cell.x), 36.0);
                bool isLeftLabel = (colMod >= 1.0 && colMod <= 5.0) && (rowSeed > 0.25);

                float indent = floor(blockSeed * 3.0) * 4.0 + 9.0;
                float lineLen = indent + floor(Hash11(cell.y * 1.37 + 7.1) * 18.0);
                bool isCodeBody = (colMod >= indent && colMod <= lineLen) && (rowSeed > 0.12);

                // Occasional blank line between functions
                bool isBlankRow = frac(cell.y * 0.142857) < 0.12;

                float pixelOn = 0.0;
                if (!isBlankRow && (isLeftLabel || isCodeBody))
                {
                    // Word gaps (spaces)
                    float charRand = Hash21(cell);
                    if (charRand > 0.16)
                    {
                        // 5x5 glyph grid with 1px padding inside 7x7 cell
                        int gx = int(floor(local.x * 7.0)) - 1;
                        int gy = int(floor(local.y * 7.0)) - 1;
                        int glyphId = int(floor(charRand * 64.0));
                        pixelOn = SampleGlyph(glyphId, gx, gy);
                    }
                }

                // Subtle secondary background code layer (parallax depth)
                float2 bgCoords = float2(worldUV.x * 6.0 + 3.5, worldUV.y * 10.0 + scroll * 4.5);
                float2 bgCell = floor(bgCoords);
                float2 bgLocal = frac(bgCoords);
                float bgRand = Hash21(bgCell + 19.3);
                float bgPixel = 0.0;
                if (bgRand > 0.72 && Hash11(bgCell.y + 5.0) > 0.4)
                {
                    int bgx = int(floor(bgLocal.x * 7.0)) - 1;
                    int bgy = int(floor(bgLocal.y * 7.0)) - 1;
                    bgPixel = SampleGlyph(int(floor(bgRand * 32.0)), bgx, bgy) * 0.22;
                }

                // Subtle CRT scanline & royal blue pool gradient
                float scanline = 0.93 + 0.07 * sin((worldUV.y * 80.0 + scroll * 20.0) * 3.14159);
                float pulse = 0.88 + 0.12 * sin(_Time.y * 2.5 + worldUV.x * 0.8);
                float3 poolBg = lerp(_DeepColor.rgb, _BaseColor.rgb, scanline * pulse);

                // Combine background blue + dim depth code + bright white/cyan foreground code
                float3 color = poolBg;
                color = lerp(color, _CodeColor.rgb * 0.65, bgPixel);
                color = lerp(color, _CodeColor.rgb * _GlowIntensity, pixelOn);

                half4 finalColor = half4(color, 1.0);
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);
                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}

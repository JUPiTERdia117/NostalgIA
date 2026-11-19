// NostalgIA — NOS/StationKit
// Muros, pisos y techos de las Estaciones, sin texturas (todo sale de la posición en el mundo):
//   · Rejilla de neón de 1 m en todas las caras.
//   · Solo en muros: renglones de código y una banda de escaneo que sube.
//   · Luz, sombras, lightmaps, SSAO y reflejos de las Reflection Probes los pone el PBR de URP.

Shader "NOS/StationKit"
{
    Properties
    {
        _WallColorTop ("Color base", Color) = (0.039, 0.165, 0.62, 1)
        [HDR] _LineColor ("Color de rejilla y código", Color) = (0.227, 0.42, 1, 1)
        _LineWidth ("Ancho de línea (fracción de celda)", Range(0.005, 0.2)) = 0.03
        _LineEmission ("Brillo de la rejilla", Range(0, 4)) = 0.6
        _CodeDensity ("Densidad de código en muros", Range(0, 1)) = 0.35
        _CodeEmission ("Brillo del código", Range(0, 4)) = 0.5
        _ScanSpeed ("Velocidad del escaneo (m/s)", Float) = 1.5
        _ScanEmission ("Brillo del escaneo", Range(0, 2)) = 0.25
        _FloorSmoothness ("Pulido del piso (reflejo)", Range(0, 1)) = 0.85
        _WallSmoothness ("Pulido de muros y techo", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // Compartido por todos los pases (el SRP Batcher necesita el mismo bloque en todos).
        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _WallColorTop, _LineColor;
            float _LineWidth, _ScanSpeed;
            half _LineEmission, _CodeDensity, _CodeEmission, _ScanEmission, _FloorSmoothness, _WallSmoothness;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            // Luces y sombras en tiempo real, SSAO y Reflection Probes.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            // Luz horneada (las LavaGlow solo existen en los lightmaps).
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Líneas de una rejilla de periodo 1 con ancho `width` (fracción de celda), con antialiasing.
            // Se mide la distancia a la línea más cercana y se divide entre fwidth (cuánto cambia la
            // coordenada de un píxel al de al lado): el borde siempre mide ~1 píxel y no hay moiré a lo lejos.
            float NOS_GridLines(float2 coord, float width)
            {
                float2 derivative = max(fwidth(coord), 1e-5);              // Cuánto avanza la coordenada por píxel.
                float2 distanceToLine = abs(frac(coord - 0.5) - 0.5);      // Distancia a la línea entera más cercana (0 a 0,5).
                float2 lines = 1.0 - saturate((distanceToLine - width * 0.5) / derivative); // 1 sobre la línea, 0 fuera.
                // Si una celda ocupa menos de ~2 píxeles, las líneas solo hacen ruido: se apagan poco a poco.
                float fade = saturate(1.0 - max(derivative.x, derivative.y) * 2.0);
                return max(lines.x, lines.y) * fade;                       // Líneas verticales y horizontales.
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 2);
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            // Número de 0 a 1, siempre el mismo para la misma celda (Interleaved Gradient Noise).
            float CellValue(float2 cell) { return frac(52.9829189 * frac(dot(cell, float2(0.06711056, 0.00583715)))); }

            // Brillo del muro: renglones de código (tramos de 1/4 m cortados en caracteres de 1/24 m)
            // y una banda de escaneo que se repite cada 10 m y sube con el tiempo.
            half WallGlow(float2 uv, float height)
            {
                float row = frac(uv.y * 8.0);                                               // 8 renglones por metro.
                float code = step(1.0 - _CodeDensity, CellValue(floor(uv * float2(4, 8))))  // ¿Este tramo tiene texto?
                           * step(0.35, CellValue(floor(uv * float2(24, 8)) + 7.0))         // Huecos entre caracteres.
                           * step(0.25, row) * step(row, 0.75)                              // Interlineado.
                           * saturate(2.0 - 48.0 * max(fwidth(uv).x, fwidth(uv).y));        // Se apaga de lejos (sin parpadeo).
                float scan = exp(-8.0 * frac(0.1 * (height - _Time.y * _ScanSpeed)));
                return code * _CodeEmission * (0.35 + scan) + scan * _ScanEmission;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 pos = input.positionWS;
                half3 normalWS = normalize(input.normalWS);

                // Cada cara dibuja el patrón en el plano al que mira: piso y techo → XZ, muros → ZY o XY.
                half3 n = abs(normalWS);
                bool isWall = n.y < max(n.x, n.z);
                float2 uv = isWall ? (n.x > n.z ? pos.zy : pos.xy) : pos.xz;

                half3 emission = _LineColor.rgb * NOS_GridLines(uv, _LineWidth) * _LineEmission;
                if (isWall) emission += _LineColor.rgb * WallGlow(uv, pos.y);

                // Iluminación PBR de URP: luces, sombras, luz horneada, SSAO y reflejos (más fuertes al ver de lado).
                InputData light = (InputData)0;
                light.positionWS = pos;
                light.normalWS = normalWS;
                light.viewDirectionWS = GetWorldSpaceNormalizeViewDir(pos);
                light.shadowCoord = TransformWorldToShadowCoord(pos);
                light.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                light.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS);
                light.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _WallColorTop.rgb;
                surface.smoothness = !isWall && normalWS.y > 0 ? _FloorSmoothness : _WallSmoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.emission = emission;
                surface.occlusion = 1;
                surface.alpha = 1;

                return half4(UniversalFragmentPBR(light, surface).rgb, 1);
            }
            ENDHLSL
        }

        // Pases estándar de URP: sombras, profundidad y normales (para el SSAO).
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

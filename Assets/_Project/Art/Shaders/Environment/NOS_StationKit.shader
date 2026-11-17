// NostalgIA — NOS/StationKit
// Material de los muros, pisos y techos de las Estaciones: color base con luz,
// rejilla de neón de 1 m y, solo en muros, código y una banda de escaneo que sube.
// No usa texturas: el patrón sale de la posición en el mundo.
// Refleja el entorno con las Reflection Probes de la escena (pisos pulidos y muros metálicos).

Shader "NOS/StationKit"
{
    Properties
    {
        _WallColorTop ("Color base (muros, piso y techo)", Color) = (0.039, 0.165, 0.62, 1)
        [HDR] _LineColor ("Color emisivo de rejilla y código", Color) = (0.227, 0.42, 1, 1)
        _LineWidth ("Ancho de línea (fracción de celda)", Range(0.005, 0.2)) = 0.03
        _LineEmission ("Emisión de la rejilla", Range(0, 4)) = 0.6
        _CodeDensity ("Densidad de código en muros", Range(0, 1)) = 0.35
        _CodeEmission ("Emisión del código", Range(0, 4)) = 0.5
        _ScanSpeed ("Velocidad de escaneo (m/s)", Float) = 1.5
        _ScanEmission ("Emisión de la banda de escaneo", Range(0, 2)) = 0.25
        _FloorReflection ("Reflejo del piso (Reflection Probe)", Range(0, 1)) = 0.6
        _FloorSmoothness ("Suavidad del piso", Range(0, 1)) = 0.85
        _WallReflection ("Reflejo de muros y techo (metal)", Range(0, 1)) = 0.25
        _WallSmoothness ("Suavidad de muros y techo", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 200

        // Esto se comparte con todos los pases. El SRP Batcher necesita el mismo
        // bloque UnityPerMaterial en todos para agrupar las piezas.
        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _WallColorTop;
            half4 _LineColor;
            float _LineWidth;
            half _LineEmission;
            half _CodeDensity;
            half _CodeEmission;
            float _ScanSpeed;
            half _ScanEmission;
            half _FloorReflection;
            half _FloorSmoothness;
            half _WallReflection;
            half _WallSmoothness;
        CBUFFER_END
        ENDHLSL

        // Pase principal: patrón + luz Blinn-Phong de URP.
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Solo las variantes de luz que usa el juego: sombras de la luz principal,
            // luces adicionales (Forward y Forward+), sombras suaves y SSAO.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            // Reflection Probes: mezcla entre sondas, proyección de caja y atlas (Forward+).
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_instancing

            // Luz horneada: sin esto las luces Baked (como las LavaGlow) nunca se ven en
            // muros y pisos, porque su luz vive solo en los lightmaps.
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/Include/NostalgIA_Common.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS    : TEXCOORD1;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 2);
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
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normalWS = normalize(input.normalWS);
                float3 pos = input.positionWS;

                // Cada cara usa el plano hacia el que mira: piso/techo → XZ, muros → ZY o XY.
                half3 n = abs(normalWS);
                bool isWall = n.y < max(n.x, n.z);
                float2 planeUV = !isWall ? pos.xz : (n.x > n.z ? pos.zy : pos.xy);
                float pixelSize = max(fwidth(planeUV).x, fwidth(planeUV).y);   // Metros que cubre un píxel.

                // Rejilla de neón en todas las caras.
                half3 emission = _LineColor.rgb * NOS_GridLines(planeUV, _LineWidth) * _LineEmission;

                if (isWall)
                {
                    // Código: segmentos de 1/4 m en filas de 1/8 m, cortados en caracteres de 1/24 m.
                    float code = step(1.0 - _CodeDensity, NOS_Hash21(floor(planeUV * float2(4, 8)) + 41.0));
                    code *= step(0.35, NOS_Hash21(floor(planeUV * float2(24, 8)) + 7.0));
                    float row = frac(planeUV.y * 8.0);
                    code *= step(0.25, row) * step(row, 0.75);          // Interlineado.
                    code *= saturate(2.0 - 48.0 * pixelSize);           // Se apaga de lejos para que no parpadee.

                    // Banda de escaneo: se repite cada 10 m y sube con el tiempo.
                    float scan = exp(-8.0 * frac(0.1 * (pos.y - _Time.y * _ScanSpeed)));
                    emission += _LineColor.rgb * (code * _CodeEmission * (0.35 + scan) + scan * _ScanEmission);
                }

                // Datos para la iluminación de URP.
                InputData light = (InputData)0;
                light.positionWS = pos;
                light.normalWS = normalWS;
                light.viewDirectionWS = GetWorldSpaceNormalizeViewDir(pos);
                light.shadowCoord = TransformWorldToShadowCoord(pos);
                light.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                // Luz horneada: lightmap en objetos estáticos, armónicos esféricos en dinámicos.
                light.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);
                light.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _WallColorTop.rgb;
                surface.specular = 0.06;
                surface.smoothness = 0.3;
                surface.normalTS = half3(0, 0, 1);
                surface.emission = emission;
                surface.occlusion = 1;
                surface.alpha = 1;

                half3 color = UniversalFragmentBlinnPhong(light, surface).rgb;

                // Reflejo de la Reflection Probe más cercana (o del cielo si no hay ninguna).
                // El piso pulido refleja más y más nítido; muros y techo, un brillo metálico suave.
                bool isFloor = !isWall && normalWS.y > 0.0;
                half reflection = isFloor ? _FloorReflection : _WallReflection;
                half smoothness = isFloor ? _FloorSmoothness : _WallSmoothness;
                half3 reflectVector = reflect(-light.viewDirectionWS, normalWS);
                half3 environment = GlossyEnvironmentReflection(reflectVector, pos, 1.0h - smoothness, 1.0h, light.normalizedScreenSpaceUV);

                // Fresnel: se refleja más cuando se mira la superficie de lado, como en un piso real.
                half fresnel = pow(1.0h - saturate(dot(normalWS, light.viewDirectionWS)), 4.0h);
                color += environment * reflection * (0.4h + 0.6h * fresnel);

                return half4(color, 1);
            }
            ENDHLSL
        }

        // Pases auxiliares estándar de URP: sombras, profundidad y normales (para el SSAO).
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

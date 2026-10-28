// =============================================================================
// NostalgIA — Shader NOS/AeroFish
// Peces voladores brillantes (Guía de estilo §5.2). Nado procedural en el vértice:
//   peso de cola w = smoothstep(0,2; 1; u)   (u = UV.x: 0 cabeza, 1 cola)
//   desplazamiento lateral x += sin(z·frecuencia − t·velocidad + 2π·s)·amplitud·w
// donde s = hash(posición del pez) desfasa cada pez del cardumen. El cuerpo se tiñe con un
// degradado lomo → vientre, franjas suaves y un tono por pez (rotación de matiz), con
// especular nítido y borde del cielo. Sin sombras: son pequeños y se mueven rápido.
//
// Conexión con FishSchool.cs:
//   - FishSchool solo gira y sube/baja el Transform raíz del cardumen una vez por frame;
//     los peces hijos comparten material con GPU Instancing activo y su aleteo lateral más su
//     variación de color (HueShift) ocurren 100 % en la GPU usando la posición de mundo de
//     cada instancia como semilla.
// =============================================================================

Shader "NOS/AeroFish"
{
    Properties
    {
        _BackColor ("Lomo", Color) = (0.05, 0.47, 0.85, 1)
        _BellyColor ("Vientre", Color) = (0.95, 1, 0.98, 1)
        _StripeColor ("Franjas", Color) = (0.49, 0.85, 0.34, 1)
        _HueVariation ("Variación de tono", Range(0, 1)) = 0.35
        _SwimAmplitude ("Amplitud del nado (m)", Float) = 0.12
        _SwimFrequency ("Frecuencia espacial", Float) = 6
        _SwimSpeed ("Velocidad del nado", Float) = 9
        _Gloss ("Brillo", Range(0, 2)) = 1.2
        _RimColor ("Borde", Color) = (0.8, 0.95, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardFish"
            Tags { "LightMode" = "UniversalForward" }
            // Cull Off para que las aletas planas se vean por ambos lados.
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FishVertex
            #pragma fragment FishFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/Include/NostalgIA_Common.hlsl"

            // Bloque UnityPerMaterial para compatibilidad con SRP Batcher e Instancing.
            CBUFFER_START(UnityPerMaterial)
                half4 _BackColor;
                half4 _BellyColor;
                half4 _StripeColor;
                half4 _RimColor;
                half _HueVariation;
                float _SwimAmplitude;
                float _SwimFrequency;
                float _SwimSpeed;
                half _Gloss;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 localPos : TEXCOORD3;
                float seed : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings FishVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Semilla por instancia a partir de la columna de traslación de su matriz M.
                float seed = NOS_Hash31(floor(UNITY_MATRIX_M._m03_m13_m23 * 3.17));
                // La cabeza (uv.x < 0,2) queda rígida (w = 0) y la ondulación crece suavemente hacia la cola (uv.x = 1).
                float w = smoothstep(0.2, 1.0, input.uv.x);
                float3 p = input.positionOS.xyz;
                float phase = p.z * _SwimFrequency - _Time.y * _SwimSpeed + seed * NOS_TWO_PI;
                p.x += sin(phase) * _SwimAmplitude * w;
                // La normal gira con la pendiente de la ondulación: ∂x/∂z = A·k·cos(fase)·w.
                float3 n = input.normalOS;
                n.x -= cos(phase) * _SwimAmplitude * _SwimFrequency * w * n.z;

                output.positionWS = TransformObjectToWorld(p);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = (half3)TransformObjectToWorldNormal(normalize(n));
                output.uv = input.uv;
                output.localPos = input.positionOS.xyz;
                output.seed = seed;
                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Rotación de matiz alrededor del eje gris (fórmula de Rodrigues con k = (1,1,1)/√3).
            // Como el eje (1,1,1)/√3 tiene R = G = B, girar un vector RGB alrededor de él cambia el
            // matiz conservando su luminancia percibida.
            half3 HueShift(half3 color, half angle)
            {
                const half3 k = half3(0.57735h, 0.57735h, 0.57735h);
                half c = cos(angle);
                half s = sin(angle);
                return color * c + cross(k, color) * s + k * dot(k, color) * (1.0h - c);
            }

            half4 FishFragment(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Invierto la normal en las caras traseras de las aletas para que reciban luz correctamente.
                half3 N = normalize(input.normalWS) * (isFrontFace ? 1.0h : -1.0h);
                half3 V = (half3)GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight();

                // Lomo arriba, vientre abajo (posición local y), franjas a lo largo del cuerpo.
                half belly = smoothstep(0.05h, -0.08h, (half)input.localPos.y);
                half3 albedo = lerp(_BackColor.rgb, _BellyColor.rgb, belly);
                half stripe = (half)step(0.7, sin(input.uv.x * 22.0) * 0.5 + 0.5) * (1.0h - belly);
                albedo = lerp(albedo, _StripeColor.rgb, stripe * 0.6h);
                albedo = HueShift(albedo, (half)(input.seed - 0.5) * _HueVariation * 6.2831h);

                half NdotL = saturate(dot(N, sun.direction) * 0.6h + 0.4h);
                half3 H = normalize(sun.direction + V);
                half spec = pow(saturate(dot(N, H)), 256.0h) * _Gloss;
                half rim = pow(1.0h - saturate(dot(N, V)), 3.0h) * 0.6h;
                half3 color = albedo * (sun.color * NdotL + SampleSH(N)) + sun.color * spec + _RimColor.rgb * rim;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

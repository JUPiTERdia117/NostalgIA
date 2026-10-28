// =============================================================================
// NostalgIA — Shader NOS/AeroWater
// Laguna turquesa de la Estación Frutiger Aero.
//
// Vértice: suma de tres ondas de Gerstner (crestas más agudas que una senoide):
//   para cada onda con dirección d, inclinación s y longitud λ:
//     k = 2π/λ,  c = √(g/k) (velocidad de fase en aguas profundas),  f = k·(d·p − c·t)
//     desplazamiento = (d.x·a·cos f, a·sin f, d.y·a·cos f), con a = s/k
//   las tangentes analíticas de cada onda se suman y la normal es cross(binormal, tangente).
// Fragmento:
//   - normal de detalle: gradiente por diferencias finitas de dos octavas de ruido que se
//     desplazan en sentidos distintos (ondulación fina sin texturas de normales)
//   - Fresnel de Schlick con F₀ = 0,02 (agua): F = F₀ + (1 − F₀)·(1 − N·V)⁵
//   - reflejo: el mismo gradiente de cielo que el skybox (NOS_SkyGradient)
//   - sol: destello Blinn-Phong muy concentrado + chispas (celdas de ruido que superan un
//     umbral y titilan)
//   - _NOS_REFRACTION (PC, con texturas de opacos y profundidad): se refracta la imagen de
//     la escena con un desplazamiento proporcional a la normal; el color se absorbe con la
//     profundidad del agua (Beer-Lambert: 1 − e^(−k·d)) y aparece espuma en la orilla
//     donde el fondo está a menos de _FoamDepth metros
//   - sin refracción (móvil): mezcla alfa con color por ángulo de vista
// Cola Transparent-50 y escritura de profundidad: se dibuja antes que las burbujas y las
// oculta correctamente cuando quedan bajo la superficie.
// =============================================================================

Shader "NOS/AeroWater"
{
    Properties
    {
        _ShallowColor ("Agua somera", Color) = (0.24, 0.9, 0.82, 1)
        _DeepColor ("Agua profunda", Color) = (0, 0.42, 0.78, 1)
        _FoamColor ("Espuma", Color) = (1, 1, 1, 1)
        _WaveA ("Onda A (dir xy, inclinación, longitud)", Vector) = (1, 0.3, 0.12, 14)
        _WaveB ("Onda B", Vector) = (-0.4, 1, 0.1, 9)
        _WaveC ("Onda C", Vector) = (0.7, -0.6, 0.08, 5.5)
        _WaveSpeed ("Velocidad de las ondas", Float) = 0.6
        _DetailScale ("Escala de la ondulación fina", Float) = 0.35
        _DetailStrength ("Intensidad de la ondulación fina", Range(0, 1)) = 0.35
        _Absorption ("Absorción por metro", Range(0.01, 2)) = 0.35
        _RefractionStrength ("Refracción", Range(0, 0.1)) = 0.03
        _ReflectionStrength ("Reflejo", Range(0, 1.5)) = 1
        _SunSpecular ("Destello del sol", Range(0, 8)) = 3
        _Sparkle ("Chispas", Range(0, 4)) = 1.5
        _FoamDepth ("Profundidad de espuma (m)", Range(0.05, 3)) = 0.8
        _MobileAlpha ("Opacidad sin refracción", Range(0.3, 1)) = 0.78
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _NOS_REFRACTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Assets/_Project/Art/Shaders/Frutiger/FrutigerCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float4 _WaveA;
                float4 _WaveB;
                float4 _WaveC;
                float _WaveSpeed;
                float _DetailScale;
                half _DetailStrength;
                half _Absorption;
                half _RefractionStrength;
                half _ReflectionStrength;
                half _SunSpecular;
                half _Sparkle;
                half _FoamDepth;
                half _MobileAlpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            // Una onda de Gerstner: devuelve el desplazamiento y acumula las tangentes.
            float3 GerstnerWave(float4 wave, float3 p, inout float3 tangent, inout float3 binormal)
            {
                float steepness = wave.z;
                float k = NOS_TWO_PI / wave.w;
                float c = sqrt(9.8 / k);
                float2 d = normalize(wave.xy);
                float f = k * (dot(d, p.xz) - c * _Time.y * _WaveSpeed);
                float a = steepness / k;
                float sinF = sin(f);
                float cosF = cos(f);
                tangent += float3(-d.x * d.x * steepness * sinF, d.x * steepness * cosF, -d.x * d.y * steepness * sinF);
                binormal += float3(-d.x * d.y * steepness * sinF, d.y * steepness * cosF, -d.y * d.y * steepness * sinF);
                return float3(d.x * a * cosF, a * sinF, d.y * a * cosF);
            }

            Varyings WaterVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                float3 tangent = float3(1, 0, 0);
                float3 binormal = float3(0, 0, 1);
                float3 displaced = p;
                displaced += GerstnerWave(_WaveA, p, tangent, binormal);
                displaced += GerstnerWave(_WaveB, p, tangent, binormal);
                displaced += GerstnerWave(_WaveC, p, tangent, binormal);
                output.positionWS = displaced;
                output.positionCS = TransformWorldToHClip(displaced);
                output.normalWS = (half3)normalize(cross(binormal, tangent));
                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Altura de la ondulación fina: dos octavas de ruido que viajan en direcciones opuestas.
            float DetailHeight(float2 p)
            {
                float t = _Time.y;
                return NOS_ValueNoise2D(p + float2(t * 0.21, t * 0.13)) +
                       0.5 * NOS_ValueNoise2D(p * 2.3 + float2(-t * 0.27, t * 0.19) + 11.0);
            }

            half4 WaterFragment(Varyings input) : SV_Target
            {
                float3 positionWS = input.positionWS;

                // Normal de detalle: gradiente (∂h/∂x, ∂h/∂z) por diferencias finitas, ε = 0,05.
                float2 dp = positionWS.xz * _DetailScale;
                const float eps = 0.05;
                float h0 = DetailHeight(dp);
                float hx = DetailHeight(dp + float2(eps, 0));
                float hz = DetailHeight(dp + float2(0, eps));
                float3 detail = float3(-(hx - h0) / eps, 1.0, -(hz - h0) / eps);
                float3 N = normalize((float3)input.normalWS + float3(detail.x, 0, detail.z) * _DetailStrength);

                float3 V = GetWorldSpaceNormalizeViewDir(positionWS);
                float3 L = _MainLightPosition.xyz;
                half NdotV = (half)saturate(dot(N, V));
                half fresnel = 0.02h + 0.98h * pow(1.0h - NdotV, 5.0h);

                half3 reflection = NOS_SkyGradient((half3)reflect(-V, N), (half3)L) * _ReflectionStrength;
                float3 H = normalize(L + V);
                half sun = (half)pow(saturate(dot(N, H)), 900.0) * _SunSpecular;
                // Chispas: celdas de 0,4 m cuyo hash supera 0,985 y titilan con fase propia.
                float2 cell = floor(positionWS.xz * 2.5);
                float glitter = step(0.985, NOS_Hash21(cell + floor(_Time.y * 3.0)));
                half sparkle = (half)(glitter * pow(saturate(dot(N, H)), 40.0)) * _Sparkle;

                half3 color;
                half alpha;
                half foam = 0.0h;
#if defined(_NOS_REFRACTION)
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float surfaceEye = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depthBelow = max(0.0, sceneEye - surfaceEye);

                // Refracción: desplazamiento según la normal, limitado en la orilla (saturate(d)).
                float2 refractUV = screenUV + N.xz * _RefractionStrength * saturate(depthBelow);
                float refractEye = LinearEyeDepth(SampleSceneDepth(refractUV), _ZBufferParams);
                // Si el punto desplazado está delante del agua, no se refracta (evita "fantasmas").
                if (refractEye < surfaceEye) { refractUV = screenUV; refractEye = sceneEye; }
                float waterDepth = max(0.0, refractEye - surfaceEye);

                half3 scene = SampleSceneColor(refractUV);
                half absorb = 1.0h - exp(-(half)waterDepth * _Absorption);
                half3 underwater = lerp(scene * _ShallowColor.rgb * 1.15h, _DeepColor.rgb, absorb);

                // Espuma: banda junto a la orilla, recortada por ruido animado.
                half shore = saturate(1.0h - (half)depthBelow / _FoamDepth);
                float foamNoise = NOS_ValueNoise2D(positionWS.xz * 1.7 + _Time.y * float2(0.3, -0.2));
                foam = shore * step(0.45, foamNoise + shore * 0.5);

                color = lerp(underwater, reflection, fresnel);
                alpha = 1.0h;
#else
                half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, 1.0h - NdotV);
                color = lerp(body, reflection, fresnel);
                alpha = lerp(_MobileAlpha, 1.0h, fresnel);
#endif
                color += (sun + sparkle) * _NOS_SunTint.rgb;
                color = lerp(color, _FoamColor.rgb, foam);
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

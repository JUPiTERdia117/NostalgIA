// =============================================================================
// NostalgIA — Shader NOS/AeroSky (skybox de la Estación Frutiger Aero)
//
// Cielo procedural sin texturas:
//   - Gradiente cénit / horizonte / suelo (misma función que usan agua, burbujas y vidrio).
//   - Sol: disco nítido + halo, en la dirección de la luz principal (_MainLightPosition).
//   - Nubes cúmulo: fBm de 4 octavas proyectado sobre un plano de altura constante
//     (uv = dir.xz / (dir.y + 0,2)), con iluminación falsa por diferencia de densidad hacia
//     el sol y borde plateado a contraluz.
//   - Banda de bruma clara sobre el horizonte, típica de los fondos Frutiger Aero.
// Costo: 8 evaluaciones de ruido por píxel solo en la mitad superior (las nubes se
// omiten con una rama por debajo del horizonte).
// =============================================================================

Shader "NOS/AeroSky"
{
    Properties
    {
        _ZenithColor ("Cénit", Color) = (0.11, 0.56, 0.95, 1)
        _HorizonColor ("Horizonte", Color) = (0.72, 0.93, 1, 1)
        _GroundColor ("Bajo el horizonte", Color) = (0.24, 0.64, 0.82, 1)
        [HDR] _SunColor ("Sol", Color) = (1.6, 1.5, 1.25, 1)
        _SunSize ("Tamaño del sol", Range(0.0005, 0.02)) = 0.004
        _CloudColor ("Nubes (cara iluminada)", Color) = (1, 1, 1, 1)
        _CloudShadow ("Nubes (sombra)", Color) = (0.62, 0.78, 0.93, 1)
        _CloudCoverage ("Cobertura de nubes", Range(0, 1)) = 0.52
        _CloudScale ("Escala de nubes", Float) = 1.6
        _CloudSpeed ("Velocidad del viento", Float) = 0.012
        _HazeStrength ("Bruma del horizonte", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SkyVertex
            #pragma fragment SkyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Include/NostalgIA_Common.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half4 _SunColor;
                half4 _CloudColor;
                half4 _CloudShadow;
                half _SunSize;
                half _CloudCoverage;
                float _CloudScale;
                float _CloudSpeed;
                half _HazeStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;   // En el skybox, la posición de objeto ES la dirección de mundo.
            };

            Varyings SkyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            // Densidad de nubes en un punto del plano de nubes.
            float CloudDensity(float2 uv)
            {
                return NOS_Fbm2D(uv, 4);
            }

            half4 SkyFragment(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.direction);
                half3 sunDir = (half3)_MainLightPosition.xyz;

                // Gradiente base (igual que NOS_SkyGradient, pero con los colores del material).
                half up = saturate((half)dir.y);
                half3 color = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, 0.55h));
                color = lerp(color, _GroundColor.rgb, saturate(-(half)dir.y * 3.0h));

                // Bruma: banda clara gaussiana centrada en el horizonte, exp(−(y/0,08)²).
                half haze = exp(-pow((half)dir.y / 0.08h, 2.0h)) * _HazeStrength;
                color = lerp(color, half3(1.0h, 1.0h, 1.0h), haze);

                // Sol: disco con borde suave y halo de dos lóbulos.
                half sunDot = saturate(dot((half3)dir, sunDir));
                half disk = smoothstep(1.0h - _SunSize, 1.0h - _SunSize * 0.5h, sunDot);
                half glow = pow(sunDot, 12.0h) * 0.35h + pow(sunDot, 180.0h) * 0.8h;
                color += _SunColor.rgb * (disk * 4.0h + glow);

                // Nubes solo por encima del horizonte (rama: ahorra el fBm en la mitad inferior).
                if (dir.y > 0.0)
                {
                    float2 uv = dir.xz / (dir.y + 0.2) * _CloudScale + _Time.y * _CloudSpeed * float2(1.0, 0.35);
                    float density = CloudDensity(uv);
                    half cover = smoothstep(_CloudCoverage, _CloudCoverage + 0.22h, (half)density);

                    // Luz falsa: si la densidad baja al avanzar hacia el sol, ese lado está iluminado.
                    float towardSun = CloudDensity(uv + sunDir.xz * 0.06);
                    half lit = saturate(0.5h + (half)(density - towardSun) * 4.0h);
                    half3 cloud = lerp(_CloudShadow.rgb, _CloudColor.rgb, lit);
                    // Borde plateado: más brillo cerca del sol donde la nube es delgada.
                    cloud += _SunColor.rgb * pow(sunDot, 6.0h) * (1.0h - cover) * 0.8h;

                    cover *= smoothstep(0.0h, 0.18h, (half)dir.y);   // Se disuelven hacia el horizonte.
                    color = lerp(color, cloud, cover);
                }

                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

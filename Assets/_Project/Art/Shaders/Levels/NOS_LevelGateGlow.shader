// =============================================================================
// NostalgIA — Shader NOS/LevelGateGlow
// Archivo: NOS_LevelGateGlow.shader
// Brillo de la puerta entre niveles (todas las Estaciones): bordes luminosos del
// marco y haz de luz volumétrico que sale de la abertura hacia el nivel actual.
//
// Técnica:
//   - Color: degradado vertical en espacio del mundo, t = (y − y_inf) / (y_sup − y_inf),
//     c = lerp(_ColorB, _ColorA, t) · _Intensity. _GradientY = (y_inf, y_sup) lo
//     escribe LevelGateVisuals para que marco, haz y luz compartan el mismo degradado.
//   - Modo haz (_SHAFT): mezcla aditiva (Blend One One) sin escritura de profundidad.
//     La opacidad aparente se construye con tres factores:
//       · transversal: a_u = sin(π·u)^k, que apaga los costados del haz;
//       · longitudinal: a_v = (1 − v)^p · smoothstep(0; 0,06; v), que lo desvanece
//         desde la puerta hasta el suelo;
//       · de vista: a_n = smoothstep(0,05; 0,55; |N·V|), que oculta las caras vistas de
//         canto (evita líneas brillantes en los planos del haz).
//     Unas estrías lentas, 0,8 + 0,2·sin(23u + ωt), sugieren polvo en suspensión.
//   - Modo borde: opaco, solo el degradado HDR (lo recoge el Bloom).
//
// Propiedades: _ColorA/_ColorB (HDR), _Intensity (0–10), _GradientY, _EdgePower (k),
// _LengthPower (p), _StreakSpeed (ω), _SrcBlend/_DstBlend/_ZWrite/_Cull (estado de
// mezcla elegido por material).
//
// Pases: UniversalForward (y DepthOnly para el modo opaco).
// Costo: aritmética simple sin texturas; muy bajo en PC y móvil. Compatible con SRP
// Batcher; la niebla se aplica hacia negro en el modo aditivo para no aclarar el
// horizonte.
// =============================================================================

Shader "NOS/LevelGateGlow"
{
    Properties
    {
        [HDR] _ColorA ("Color superior", Color) = (1, 0.86, 0.55, 1)
        [HDR] _ColorB ("Color inferior", Color) = (1, 0.6, 0.78, 1)
        _Intensity ("Intensidad", Range(0, 10)) = 2
        _GradientY ("Altura del degradado (inferior, superior)", Vector) = (0, 4.4, 0, 0)
        _EdgePower ("Suavidad transversal del haz (k)", Range(0.2, 4)) = 1.2
        _LengthPower ("Caída longitudinal del haz (p)", Range(0.2, 4)) = 1.4
        _StreakSpeed ("Velocidad de las estrías (rad/s)", Float) = 0.6
        [Toggle(_SHAFT)] _Shaft ("Modo haz", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Mezcla origen", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Mezcla destino", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Escritura de profundidad", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Caras", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _ColorA;
            half4 _ColorB;
            half _Intensity;
            float4 _GradientY;
            half _EdgePower;
            half _LengthPower;
            float _StreakSpeed;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma shader_feature_local _SHAFT

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
                half3 normalWS    : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                half fogFactor    : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = (half)ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Degradado vertical compartido con la abertura y el foco.
                float range = max(_GradientY.y - _GradientY.x, 0.001);
                half t = (half)saturate((input.positionWS.y - _GradientY.x) / range);
                half3 color = lerp(_ColorB.rgb, _ColorA.rgb, t) * _Intensity;

            #if defined(_SHAFT)
                half3 viewDir = (half3)GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facing = smoothstep(0.05h, 0.55h, abs(dot(normalize(input.normalWS), viewDir)));
                half across = pow(saturate(sin((half)input.uv.x * PI)), _EdgePower);
                half v = (half)saturate(input.uv.y);
                half along = pow(1.0h - v, _LengthPower) * smoothstep(0.0h, 0.06h, v);
                half streaks = 0.8h + 0.2h * (half)sin(input.uv.x * 23.0 + _Time.y * _StreakSpeed);
                color *= facing * across * along * streaks;
                // Mezcla aditiva: la niebla lleva el aporte hacia negro, no hacia el color de niebla.
                color = MixFogColor(color, half3(0.0h, 0.0h, 0.0h), input.fogFactor);
            #else
                color = MixFog(color, input.fogFactor);
            #endif
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite [_ZWrite]
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
    }
}

// =============================================================================
// NostalgIA — Shader NOS/LevelGateGlow
// Archivo: NOS_LevelGateGlow.shader
//
// Es el brillo que sale de la puerta entre niveles. Lo uso en dos tipos de pieza:
//   - Bordes luminosos del marco (modo opaco): solo el degradado de color en HDR, que el
//     Bloom convierte en resplandor.
//   - Haz de luz (modo haz, palabra clave _SHAFT): las caras de una malla en forma de
//     cuña que sale de la abertura hacia el suelo, sumando luz como si hubiera polvo en
//     el aire iluminado por el otro mundo.
//
// Color: degradado vertical en espacio de MUNDO,
//     t = (y − y_inf) / (y_sup − y_inf),   c = lerp(_ColorB, _ColorA, t) · _Intensity.
// LevelGateVisuals escribe _GradientY = (y_inf, y_sup) con la altura real de la puerta, así
// el marco, el haz y el foco de luz comparten exactamente el mismo degradado.
//
// En modo haz la "opacidad" (que en aditivo es cuánto se suma) es el producto de tres factores:
//   - a_u = sin(π·u)^k (a lo ancho): 0 en los costados y 1 al centro, para que el haz no tenga bordes duros.
//   - a_v = (1 − v)^p · smoothstep(0; 0,06; v) (a lo largo): fuerte en la puerta y se apaga hacia el suelo;
//     el smoothstep evita una línea dura pegada a la puerta.
//   - a_n = smoothstep(0,05; 0,55; |N·V|) (según la vista): cuando una cara del haz se ve de canto, su
//     brillo se acumula en una línea muy delgada y muy brillante; con este factor la apago.
// Encima, unas estrías que se mueven despacio, 0,8 + 0,2·sin(23u + ω·t), sugieren polvo flotando.
//
// El estado de mezcla (_SrcBlend, _DstBlend, _ZWrite, _Cull) lo elige cada material: el de los
// bordes es opaco y el del haz es aditivo sin escribir profundidad. Así un solo shader cubre las
// dos piezas.
//
// Costo: pura aritmética, sin texturas; muy barato en PC y en móvil. Compatible con SRP Batcher.
// =============================================================================

Shader "NOS/LevelGateGlow"
{
    Properties
    {
        // Colores e intensidad: LevelGateVisuals los copia desde su configuración (sólido o degradado).
        [HDR] _ColorA ("Color superior", Color) = (1, 0.86, 0.55, 1)
        [HDR] _ColorB ("Color inferior", Color) = (1, 0.6, 0.78, 1)
        _Intensity ("Intensidad", Range(0, 10)) = 2
        _GradientY ("Altura del degradado (inferior, superior)", Vector) = (0, 4.4, 0, 0)   // x = y del piso de la puerta, y = y del dintel, en metros de mundo.
        _EdgePower ("Suavidad transversal del haz (k)", Range(0.2, 4)) = 1.2
        _LengthPower ("Caída longitudinal del haz (p)", Range(0.2, 4)) = 1.4
        _StreakSpeed ("Velocidad de las estrías (rad/s)", Float) = 0.6
        [Toggle(_SHAFT)] _Shaft ("Modo haz", Float) = 0      // Activa la palabra clave _SHAFT (otra variante del shader).
        // Estado de render elegido por material: opaco (One/Zero, ZWrite On) o aditivo (One/One, ZWrite Off).
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
            #pragma shader_feature_local _SHAFT   // shader_feature: solo se compila la variante si algún material la usa.

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
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);   // La necesito para el factor de vista del haz.
                output.uv = input.uv;   // En la malla del haz: u va de un costado al otro y v de la puerta (0) al suelo (1).
                output.fogFactor = (half)ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Degradado vertical en metros de mundo, compartido con la abertura y el foco de luz.
                float range = max(_GradientY.y - _GradientY.x, 0.001);
                half t = (half)saturate((input.positionWS.y - _GradientY.x) / range);
                half3 color = lerp(_ColorB.rgb, _ColorA.rgb, t) * _Intensity;

            #if defined(_SHAFT)
                half3 viewDir = (half3)GetWorldSpaceNormalizeViewDir(input.positionWS);   // Dirección del píxel hacia la cámara.
                // |N·V| es 1 si veo la cara de frente y 0 si la veo de canto. abs() porque el haz se ve por ambos lados.
                half facing = smoothstep(0.05h, 0.55h, abs(dot(normalize(input.normalWS), viewDir)));
                half across = pow(saturate(sin((half)input.uv.x * PI)), _EdgePower);       // sin(π·u): 0 en u = 0 y u = 1, 1 en el centro.
                half v = (half)saturate(input.uv.y);
                half along = pow(1.0h - v, _LengthPower) * smoothstep(0.0h, 0.06h, v);     // Se apaga hacia el suelo; nace suave en la puerta.
                half streaks = 0.8h + 0.2h * (half)sin(input.uv.x * 23.0 + _Time.y * _StreakSpeed); // Unas 3,7 estrías a lo ancho (23/2π) que se deslizan.
                color *= facing * across * along * streaks;                                // Producto de los cuatro factores.
                // En aditivo la niebla tiene que llevar el aporte hacia negro: si lo llevara al color de la
                // niebla, el haz aclararía el horizonte en lugar de desvanecerse.
                color = MixFogColor(color, half3(0.0h, 0.0h, 0.0h), input.fogFactor);
            #else
                color = MixFog(color, input.fogFactor);   // Bordes opacos: niebla normal.
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

// =============================================================================
// NostalgIA — Shader NOS/AeroBubbleSprite
// Burbuja para partículas (billboard): reconstruye una esfera a partir del UV del cuadro
// (impostor), así cada partícula de 2 triángulos se sombrea como una burbuja completa:
//   p = uv·2 − 1,  r² = p·p;  fuera del círculo (r² > 1) α = 0
//   normal de vista n = (p.x, p.y, √(1 − r²))  → normal de mundo con la inversa de la vista
// Luego aplica el mismo NOS_BubbleShade que las burbujas de malla. El tamaño, el color y
// el desvanecimiento vienen del sistema de partículas (color por vértice).
// StableRandom.x llega en TEXCOORD0.z (custom vertex stream) para desfasar la película.
//
// Diferencia frente a NOS/AeroBubble en modo _IMPOSTOR:
//   - Aquí el ParticleSystem ya orienta los cuadros hacia la cámara en la CPU/GPU y anima
//     su posición, tamaño y alfa de vida en el color de vértice (input.color).
//   - Por eso el vertex shader solo transforma la posición y pasa TEXCOORD0.z como semilla.
// =============================================================================

Shader "NOS/AeroBubbleSprite"
{
    Properties
    {
        _FilmThickness ("Grosor de la película (µm)", Range(0.1, 1.5)) = 0.6
        _Iridescence ("Iridiscencia", Range(0, 2)) = 1.2
        _Reflection ("Reflejo del cielo", Range(0, 2)) = 0.8
        _BaseAlpha ("Opacidad del centro", Range(0, 0.3)) = 0.03
        _EdgeAlpha ("Opacidad del borde", Range(0, 1)) = 0.8
        _Specular ("Destello del sol", Range(0, 4)) = 1.5
        _WindowHighlight ("Reflejo de ventana", Range(0, 1.5)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SpriteVertex
            #pragma fragment SpriteFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Frutiger/FrutigerCommon.hlsl"

            // Bloque UnityPerMaterial para compatibilidad con SRP Batcher.
            CBUFFER_START(UnityPerMaterial)
                half _FilmThickness;
                half _Iridescence;
                half _Reflection;
                half _BaseAlpha;
                half _EdgeAlpha;
                half _Specular;
                half _WindowHighlight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float3 texcoord : TEXCOORD0;   // xy: UV del cuadro; z: StableRandom.x.
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 uvSeed : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings SpriteVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uvSeed = input.texcoord;
                output.color = input.color;
                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 SpriteFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Mapeo UV [0, 1] al cuadrado [−1, 1] para evaluar la ecuación del disco r² = x² + y².
                float2 p = input.uvSeed.xy * 2.0 - 1.0;
                float r2 = dot(p, p);
                if (r2 > 1.0) return half4(0, 0, 0, 0);   // Fuera del disco: transparente.

                // Normal de la esfera en espacio de vista y su paso a mundo (inversa de la vista).
                float3 viewN = float3(p.x, p.y, sqrt(1.0 - r2));
                float3 N = normalize(mul((float3x3)UNITY_MATRIX_I_V, viewN));
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 L = _MainLightPosition.xyz;

                // Reutilizo exactamente el mismo sombreado físico-estilizado de NOS_BubbleShade.
                half4 color = NOS_BubbleShade(N, V, L, (half3)viewN, input.uvSeed.z,
                                              _FilmThickness, _Iridescence, _Reflection,
                                              _BaseAlpha, _EdgeAlpha, _Specular, _WindowHighlight, input.color.rgb);
                // Borde antialiasado del disco: atenuación en el último 4 % del radio.
                half edge = saturate((1.0h - (half)r2) * 25.0h);
                color *= edge * input.color.a;
                color.rgb = MixFogColor(color.rgb, unity_FogColor.rgb * color.a, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

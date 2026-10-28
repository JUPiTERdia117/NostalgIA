// =============================================================================
// NostalgIA — Shader NOS/AeroBubble
// Burbuja de jabón iridiscente para mallas (esferas). Pensado para dibujarse con GPU
// Instancing (BubbleFieldRenderer usa Graphics.RenderMeshInstanced: cientos de burbujas
// en una sola llamada) y también para burbujas sueltas con MeshRenderer.
//
// Movimiento 100 % en el vértice (sin costo de CPU por burbuja):
//   semilla s = hash(centro de la instancia)  → cada burbuja se mueve distinto
//   deriva   = (sin(0,7t + 2πs), 1,4·sin(t + 12s), cos(0,6t + 9s)) · A · (0,5 + s)
//   ondulación de la superficie: desplazamiento sobre la normal
//            w = sin(p·(3,1; 2,3; 4,7) + ω·t + 20s) · amplitud
// Sombreado: NOS_BubbleShade (Fresnel, película delgada, reflejo del cielo, destellos).
// Transparente premultiplicado, sin escritura de profundidad.
//
// Modo impostor (_IMPOSTOR, con la malla Quad): el cuadro se orienta hacia la cámara con los
// ejes derecha/arriba de la matriz de vista y la esfera se reconstruye por píxel
// (p = 2·uv − 1, n_vista = (p.x, p.y, √(1 − |p|²))). Cada burbuja cuesta 2 triángulos; la
// ondulación de la superficie se sustituye por una "respiración" del radio
// r·(1 + 0,04·sin(ω·t + 20s)) y una leve elipse que gira.
// =============================================================================

Shader "NOS/AeroBubble"
{
    Properties
    {
        _Tint ("Tinte del cuerpo", Color) = (0.85, 0.97, 1, 1)
        _FilmThickness ("Grosor de la película (µm)", Range(0.1, 1.5)) = 0.55
        _Iridescence ("Iridiscencia", Range(0, 2)) = 1.1
        _Reflection ("Reflejo del cielo", Range(0, 2)) = 0.9
        _BaseAlpha ("Opacidad del centro", Range(0, 0.3)) = 0.04
        _EdgeAlpha ("Opacidad del borde", Range(0, 1)) = 0.75
        _Specular ("Destello del sol", Range(0, 4)) = 2
        _WindowHighlight ("Reflejo de ventana", Range(0, 1.5)) = 0.9
        _DriftAmplitude ("Amplitud de la deriva (m)", Float) = 0.6
        _DriftSpeed ("Velocidad de la deriva", Float) = 0.5
        _WobbleAmount ("Ondulación de la superficie", Range(0, 0.1)) = 0.025
        _WobbleSpeed ("Velocidad de ondulación", Float) = 3
        [Toggle(_IMPOSTOR)] _Impostor ("Impostor (malla Quad)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BubbleVertex
            #pragma fragment BubbleFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _IMPOSTOR

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Frutiger/FrutigerCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _FilmThickness;
                half _Iridescence;
                half _Reflection;
                half _BaseAlpha;
                half _EdgeAlpha;
                half _Specular;
                half _WindowHighlight;
                float _DriftAmplitude;
                float _DriftSpeed;
                half _WobbleAmount;
                float _WobbleSpeed;
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
                float seed : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float2 corner : TEXCOORD4;   // Solo impostor: coordenada del cuadro en [−1, 1].
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings BubbleVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Centro de la instancia: traslación de su matriz de objeto a mundo.
                float3 center = UNITY_MATRIX_M._m03_m13_m23;
                float seed = NOS_Hash31(floor(center * 7.31));

                float t = _Time.y * _DriftSpeed;
                float3 drift = float3(sin(t * 0.7 + seed * NOS_TWO_PI), 1.4 * sin(t + seed * 12.1), cos(t * 0.6 + seed * 9.3))
                             * _DriftAmplitude * (0.5 + seed);

#if defined(_IMPOSTOR)
                // Radio = escala de la instancia (la matriz escala el cuadro de 1 m al diámetro).
                float radius = length(UNITY_MATRIX_M._m00_m10_m20) * 0.5;
                radius *= 1.0 + 0.04 * sin(_Time.y * _WobbleSpeed + seed * 20.0);
                float2 corner = input.uv * 2.0 - 1.0;
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;   // Filas de la vista = ejes de la cámara en mundo.
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                float3 sphereCenter = center + drift;
                float3 positionWS = sphereCenter + (cameraRight * corner.x + cameraUp * corner.y) * radius;
                output.corner = corner;
                // La normal de la cara apunta a la cámara; la de la esfera se reconstruye en el fragmento.
                output.normalWS = (half3)normalize(GetCameraPositionWS() - sphereCenter);
#else
                float wobble = sin(dot(input.positionOS.xyz, float3(3.1, 2.3, 4.7)) * 4.0 + _Time.y * _WobbleSpeed + seed * 20.0) * _WobbleAmount;
                float3 positionOS = input.positionOS.xyz + input.normalOS * wobble;
                float3 positionWS = TransformObjectToWorld(positionOS) + drift;
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
#endif
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.seed = seed;
                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 BubbleFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 L = _MainLightPosition.xyz;
#if defined(_IMPOSTOR)
                // Leve elipse que gira con el tiempo: la burbuja "tiembla" sin geometría extra.
                float2 p = input.corner;
                float angle = _Time.y * 0.7 + input.seed * 6.0;
                float2 axis = float2(cos(angle), sin(angle));
                p += axis * dot(p, axis) * 0.05 * sin(_Time.y * _WobbleSpeed * 0.8 + input.seed * 9.0);
                float r2 = dot(p, p);
                if (r2 > 1.0) return half4(0, 0, 0, 0);
                float3 viewNormal = float3(p, sqrt(1.0 - r2));
                float3 N = normalize(mul((float3x3)UNITY_MATRIX_I_V, viewNormal));
                float3 V = (float3)input.normalWS;   // Dirección hacia la cámara desde el centro.
                half3 viewN = (half3)viewNormal;
                half edge = (half)saturate((1.0 - r2) * 25.0);   // Borde del disco antialiasado.
#else
                float3 N = normalize((float3)input.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 viewN = (half3)mul((float3x3)UNITY_MATRIX_V, N);
                half edge = 1.0h;
#endif

                half4 color = NOS_BubbleShade(N, V, L, viewN, input.seed,
                                              _FilmThickness, _Iridescence, _Reflection,
                                              _BaseAlpha, _EdgeAlpha, _Specular, _WindowHighlight, _Tint.rgb);
                color *= edge;
                // Niebla en premultiplicado: se atenúa hacia el color de niebla proporcionalmente a α.
                color.rgb = MixFogColor(color.rgb, unity_FogColor.rgb * color.a, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

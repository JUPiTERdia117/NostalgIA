// =============================================================================
// NostalgIA — Shader NOS/LevelGatePortal
// Archivo: NOS_LevelGatePortal.shader
// Superficie de la puerta entre niveles (todas las Estaciones).
//
// Técnica:
//   - Muestra la vista del nivel siguiente, dibujada por una cámara dedicada en una
//     textura (_PortalTex), con coordenadas de PANTALLA: uv = posición_clip.xy / w.
//     Así la imagen encaja exactamente con la perspectiva del jugador y cualquier cara
//     de la caja de la superficie muestra lo mismo (permite engrosarla al cruzar).
//   - Antes de abrirse (_Open = 0) la abertura es un relleno de luz en degradado,
//     como una puerta abierta hacia un exterior sobreexpuesto; al abrirse se mezcla
//     con la vista: c = lerp(relleno, vista_con_bruma, _Open).
//   - Bruma de borde: cerca del marco la luz "come" la imagen, e(d) = exp(−d / w_b),
//     con d la distancia al borde en metros (se obtiene de la escala del objeto).
//   - Disolución (_Dissolve): ruido de valor 2D; se descarta el fragmento si
//     ruido < _Dissolve y una franja de 0,08 sobre el umbral brilla con el color de la
//     puerta, para que el nivel anterior "se deshaga en luz".
//
// Propiedades:
//   _PortalTex      textura de la vista (la asigna LevelGateVisuals con un MPB)
//   _ColorA/_ColorB colores HDR superior e inferior del degradado
//   _GlowIntensity  multiplicador HDR del brillo (0–8)
//   _EdgeWidth      anchura de la bruma de borde, en m (0,01–0,6)
//   _Open           0 = relleno de luz, 1 = vista del otro nivel
//   _Dissolve       0 = entera, 1 = disuelta por completo
//   _DissolveScale  frecuencia del ruido de disolución
//
// Pases: UniversalForward y DepthOnly. Cull Off (la caja puede verse por dentro).
// Costo: una lectura de textura y un ruido de valor por fragmento; bajo en PC y
// móvil. Compatible con SRP Batcher (las propiedades por objeto llegan por MPB, que
// se reserva a estas pocas superficies).
// =============================================================================

Shader "NOS/LevelGatePortal"
{
    Properties
    {
        _PortalTex ("Vista del otro nivel", 2D) = "black" {}
        [HDR] _ColorA ("Color superior", Color) = (1, 0.86, 0.55, 1)
        [HDR] _ColorB ("Color inferior", Color) = (1, 0.6, 0.78, 1)
        _GlowIntensity ("Intensidad del brillo", Range(0, 8)) = 2.4
        _EdgeWidth ("Anchura de la bruma de borde (m)", Range(0.01, 0.6)) = 0.18
        _Open ("Apertura", Range(0, 1)) = 0
        _Dissolve ("Disolución", Range(0, 1)) = 0
        _DissolveScale ("Escala del ruido de disolución", Float) = 3.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Assets/_Project/Art/Shaders/Include/NostalgIA_Common.hlsl"

        TEXTURE2D(_PortalTex);
        SAMPLER(sampler_PortalTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _ColorA;
            half4 _ColorB;
            half _GlowIntensity;
            half _EdgeWidth;
            half _Open;
            half _Dissolve;
            float _DissolveScale;
        CBUFFER_END

        // Tamaño de la abertura en metros: longitud de los ejes X e Y de la matriz objeto→mundo.
        float2 ApertureSize()
        {
            return float2(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21));
        }

        // Ruido de disolución en [0, 1]. Se corrige la relación de aspecto para que las manchas
        // sean redondas aunque la abertura sea más alta que ancha, y se desplaza muy despacio.
        float DissolveNoise(float2 uv)
        {
            float2 size = ApertureSize();
            float2 p = uv * float2(size.x / max(size.y, 0.001), 1.0) * _DissolveScale;
            return NOS_ValueNoise2D(p + _Time.y * 0.12) * 0.7 + NOS_ValueNoise2D(p * 2.7 - _Time.y * 0.2) * 0.3;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float2 uv         : TEXCOORD1;   // Posición en la abertura: (0,0) abajo a la izquierda, (1,1) arriba a la derecha.
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                // La malla es un cubo unitario centrado: x, y ∈ [−0,5; 0,5] → uv ∈ [0, 1].
                output.uv = input.positionOS.xy + 0.5;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Disolución: se evalúa primero para descartar cuanto antes los fragmentos.
                float noise = DissolveNoise(input.uv);
                clip(noise - _Dissolve * 1.08 + (_Dissolve <= 0.0 ? 1.0 : 0.0));

                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                half3 view = SAMPLE_TEXTURE2D(_PortalTex, sampler_PortalTex, screenUV).rgb;

                // Degradado vertical de la luz de la puerta: c(y) = lerp(B, A, y).
                half3 tint = lerp(_ColorB.rgb, _ColorA.rgb, saturate((half)input.uv.y));
                half3 light = tint * _GlowIntensity;

                // Distancia al borde en metros: d = min((0,5 − |x|)·ancho, (0,5 − |y|)·alto).
                float2 size = ApertureSize();
                float2 centered = abs(input.uv - 0.5);
                float edgeDistance = min((0.5 - centered.x) * size.x, (0.5 - centered.y) * size.y);
                half haze = (half)exp(-max(edgeDistance, 0.0) / max(_EdgeWidth, 0.001));

                // Relleno cerrado: luz con un núcleo algo más intenso en el centro (sobreexposición).
                half core = (half)(1.0 - saturate(length((input.uv - float2(0.5, 0.45)) * float2(1.6, 1.1))));
                half3 closedFill = light * (0.75h + 0.6h * core);

                // Abierta: la vista con bruma luminosa en los bordes.
                half3 openView = lerp(view, light * 1.2h, haze * 0.85h);
                half3 color = lerp(closedFill, openView, _Open);

                // Franja brillante en el frente de la disolución.
                half band = (_Dissolve > 0.0) ? (half)(1.0 - smoothstep(0.0, 0.08, noise - _Dissolve * 1.08)) : 0.0h;
                color += light * band * 2.5h;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.positionOS.xy + 0.5;
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                clip(DissolveNoise(input.uv) - _Dissolve * 1.08 + (_Dissolve <= 0.0 ? 1.0 : 0.0));
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}

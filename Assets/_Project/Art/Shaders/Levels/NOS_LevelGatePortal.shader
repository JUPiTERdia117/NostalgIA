// =============================================================================
// NostalgIA — Shader NOS/LevelGatePortal
// Archivo: NOS_LevelGatePortal.shader
//
// Es la "superficie" de la puerta entre niveles (LevelGate): lo que ves dentro del marco.
// Hace tres cosas según el momento:
//   1. Puerta cerrada (_Open = 0): un relleno de luz en degradado, como cuando abres una
//      puerta hacia un exterior muy iluminado y solo ves blanco cálido.
//   2. Puerta abierta (_Open = 1): la vista en vivo del siguiente nivel. Una cámara aparte
//      la dibuja en una textura (_PortalTex) y yo la leo con coordenadas de PANTALLA,
//      uv = posición_clip.xy / w. Como esa cámara mira exactamente desde donde está el
//      jugador (trasladado al otro nivel), la imagen encaja con la perspectiva y parece un
//      hueco real. Leer en pantalla también hace que cualquier cara de la caja muestre lo
//      mismo, y por eso puedo engrosar la caja al cruzar sin que se note.
//   3. Disolución (_Dissolve de 0 a 1): la puerta de regreso se deshace en manchas de luz
//      cuando dejamos atrás el nivel anterior.
//
// Fórmulas:
//   - Mezcla cerrada/abierta:  c = lerp(relleno, vista_con_bruma, _Open).
//   - Bruma de borde:  e(d) = exp(−d / w), con d la distancia al marco en metros. A una
//     distancia w la bruma ya bajó a 0,37; a 3w, a 0,05.
//   - Disolución: descarto el píxel si ruido < _Dissolve·1,08. La franja de 0,08 por
//     encima del umbral brilla con el color de la puerta, como un borde que se quema.
//
// Propiedades:
//   _PortalTex       vista del otro nivel (la asigna LevelGateVisuals con un MaterialPropertyBlock)
//   _ColorA/_ColorB  colores HDR de arriba y de abajo del degradado
//   _GlowIntensity   multiplicador de brillo (0 a 8)
//   _EdgeWidth       ancho de la bruma del borde, en metros (0,01 a 0,6)
//   _Open            0 = relleno de luz, 1 = vista del otro nivel
//   _Dissolve        0 = entera, 1 = desaparecida
//   _DissolveScale   tamaño de las manchas de la disolución (más alto = manchas más chicas)
//
// Pases: UniversalForward y DepthOnly. Cull Off porque la caja se puede ver por dentro.
// Costo por píxel: una lectura de textura y dos ruidos de valor; barato en PC y en móvil.
// Las propiedades están en UnityPerMaterial, pero como cada puerta recibe su textura y su
// apertura con un MaterialPropertyBlock, estas pocas superficies se dibujan fuera del SRP
// Batcher. Son dos o tres objetos en pantalla, así que no pesa.
// =============================================================================

Shader "NOS/LevelGatePortal"
{
    Properties
    {
        // Todos estos valores los anima LevelGateVisuals en juego; en el Inspector sirven para previsualizar.
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
            "RenderType" = "TransparentCutout"   // Opaco con recortes (clip), no transparencia con mezcla.
            "Queue" = "AlphaTest+10"             // Después de lo opaco, para que el marco ya esté en el búfer de profundidad.
            "RenderPipeline" = "UniversalPipeline"
        }

        // HLSLINCLUDE se copia en los dos pases: así el pase de profundidad recorta exactamente
        // igual que el de color y la disolución no deja "huecos fantasma" en la profundidad.
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

        // Tamaño de la abertura en metros. La malla es un cubo de 1 m escalado al tamaño de la puerta,
        // así que la escala está guardada en la matriz del objeto: la longitud de su primera columna
        // es la escala en X (ancho) y la de la segunda, la escala en Y (alto).
        float2 ApertureSize()
        {
            return float2(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21));
        }

        // Ruido de disolución en [0, 1]. Multiplico x por ancho/alto para que las manchas salgan
        // redondas aunque la puerta sea más alta que ancha. Sumo dos capas de ruido de valor
        // (70 % grande y 30 % fina, 2,7 veces más chica) que se mueven despacio en sentidos opuestos.
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
                float2 uv         : TEXCOORD1;   // Posición dentro de la abertura: (0,0) abajo a la izquierda, (1,1) arriba a la derecha.
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);   // Posición de pantalla sin dividir entre w (la divido por píxel).
                // La malla es un cubo de 1 m centrado: x, y van de −0,5 a 0,5; sumando 0,5 quedan de 0 a 1.
                output.uv = input.positionOS.xy + 0.5;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Disolución primero: si el píxel se va a descartar, no gasto en leer la textura.
                // clip(x) descarta el píxel si x < 0. Cuando _Dissolve es 0 sumo 1 para que nunca se descarte.
                // Multiplico por 1,08 para que con _Dissolve = 1 el umbral (1,08) supere al ruido máximo (1)
                // y la puerta desaparezca por completo.
                float noise = DissolveNoise(input.uv);
                clip(noise - _Dissolve * 1.08 + (_Dissolve <= 0.0 ? 1.0 : 0.0));

                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-5);   // División de perspectiva: posición en pantalla de 0 a 1.
                half3 view = SAMPLE_TEXTURE2D(_PortalTex, sampler_PortalTex, screenUV).rgb;

                // Degradado vertical de la luz de la puerta: abajo _ColorB, arriba _ColorA.
                half3 tint = lerp(_ColorB.rgb, _ColorA.rgb, saturate((half)input.uv.y));
                half3 light = tint * _GlowIntensity;                     // Valor HDR: arriba de 1 lo recoge el Bloom.

                // Distancia al marco en metros: d = min((0,5 − |x|)·ancho, (0,5 − |y|)·alto).
                // Uso metros y no UV para que la bruma mida lo mismo en los lados y arriba aunque la puerta no sea cuadrada.
                float2 size = ApertureSize();
                float2 centered = abs(input.uv - 0.5);
                float edgeDistance = min((0.5 - centered.x) * size.x, (0.5 - centered.y) * size.y);
                half haze = (half)exp(-max(edgeDistance, 0.0) / max(_EdgeWidth, 0.001));   // 1 pegado al marco, cae exponencialmente hacia el centro.

                // Puerta cerrada: luz con un núcleo más intenso un poco abajo del centro (0,5; 0,45), en forma de
                // elipse (escalo x por 1,6 e y por 1,1). El relleno va de 0,75 en las orillas a 1,35 en el núcleo.
                half core = (half)(1.0 - saturate(length((input.uv - float2(0.5, 0.45)) * float2(1.6, 1.1))));
                half3 closedFill = light * (0.75h + 0.6h * core);

                // Puerta abierta: la vista del otro nivel, con la luz "comiéndose" la imagen cerca del marco (hasta 85 %).
                half3 openView = lerp(view, light * 1.2h, haze * 0.85h);
                half3 color = lerp(closedFill, openView, _Open);         // LevelGateVisuals anima _Open de 0 a 1 al despertar el nivel siguiente.

                // Borde que se "quema": 1 justo en el umbral de la disolución y 0 a partir de 0,08 por encima.
                half band = (_Dissolve > 0.0) ? (half)(1.0 - smoothstep(0.0, 0.08, noise - _Dissolve * 1.08)) : 0.0h;
                color += light * band * 2.5h;                            // Lo sumo muy intenso para que el Bloom lo haga brillar.
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
                return input.positionCS.z;   // En el pase de profundidad solo importa escribir z (y respetar el mismo recorte).
            }
            ENDHLSL
        }
    }
}

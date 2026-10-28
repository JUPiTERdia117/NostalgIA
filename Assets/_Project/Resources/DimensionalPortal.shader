// =============================================================================
// NostalgIA — Portal dimensional (puerta del Tutorial hacia OtherDimension)
// Archivo: DimensionalPortal.shader
//
// Es la "ventana" del portal antiguo (NonEuclideanPortal / TutorialDoor). Una cámara
// aparte dibuja el otro mundo en una textura (_PortalTex) y este shader la pega sobre
// la superficie de la puerta.
//
// El truco central: leo la textura con coordenadas de PANTALLA, no con las UV de la
// malla. La cámara del portal ve exactamente desde donde está el jugador (trasladada
// al otro mundo), así que si tomo el píxel (x, y) de su imagen y lo pongo en el píxel
// (x, y) de la pantalla, la perspectiva encaja perfecto y parece un hueco real.
//
// Encima le agregué:
//   - Un borde de energía brillante pegado al marco (smoothstep sobre la distancia al borde).
//   - Una ondulación muy sutil, solo cerca de los bordes, que mueve un poco la lectura de
//     la textura para que el otro mundo se vea "inestable".
//
// La puerta nueva entre niveles usa NOS/LevelGatePortal, que sigue la misma idea pero con
// bruma de luz, degradado de dos colores y disolución.
// =============================================================================

Shader "Tutorial/DimensionalPortal"
{
    Properties
    {
        _PortalTex ("Portal Render Texture", 2D) = "black" {}                       // La asigna NonEuclideanPortal en tiempo de ejecución.
        _FrameColor ("Portal Edge Glow Color", Color) = (0.1, 0.75, 1.0, 1.0)      // Color del borde de energía.
        _BorderWidth ("Edge Glow Width", Range(0.0, 0.15)) = 0.035                  // Ancho del borde, en fracción de la puerta (UV).
        _RippleStrength ("Dimensional Ripple", Range(0.0, 0.03)) = 0.004            // Cuánto se deforma la imagen cerca del borde.
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+10"    // Un poco después de la geometría normal, para que el marco ya esté dibujado.
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PortalScreenSpace"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off      // Se ve por los dos lados de la puerta.
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Las texturas no van dentro del CBUFFER; solo los números.
            TEXTURE2D(_PortalTex);
            SAMPLER(sampler_PortalTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _FrameColor;
                float _BorderWidth;
                float _RippleStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;   // UV de la malla: (0,0) a (1,1) de esquina a esquina de la puerta.
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;   // Posición en pantalla todavía sin dividir entre w.
                float2 uv         : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // ComputeScreenPos pasa de espacio de recorte a un rango de 0 a w. No divido aquí
                // entre w porque la división no se interpola bien entre vértices; la hago por píxel.
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // División de perspectiva: (x/w, y/w) da la posición del píxel en pantalla de 0 a 1.
                // El max evita dividir entre cero en un caso degenerado.
                float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);

                // Distancia al borde más cercano de la puerta, en UV:
                // min(uv, 1 − uv) da la distancia a izquierda/derecha y abajo/arriba; me quedo con la menor.
                float2 dEdge = min(input.uv, 1.0 - input.uv);
                float edgeDist = min(dEdge.x, dEdge.y);

                // Ondulación solo cerca del marco: edgeFactor vale 1 en el borde y baja a 0 a una
                // distancia de 4 anchos de borde. Así el centro de la imagen queda quieto.
                float edgeFactor = saturate(1.0 - edgeDist / max(0.001, _BorderWidth * 4.0));
                // Dos ondas seno/coseno que avanzan con el tiempo (4 y 3,5 rad/s) y varían a lo largo
                // de la puerta (18 ondas por unidad de UV). Las uso como desplazamiento de la lectura.
                float2 ripple = float2(
                    sin(_Time.y * 4.0 + input.uv.y * 18.0),
                    cos(_Time.y * 3.5 + input.uv.x * 18.0)
                ) * (_RippleStrength * edgeFactor);

                // Leo la imagen del otro mundo en la posición de pantalla, ligeramente desplazada.
                // saturate evita leer fuera de la textura cuando la ondulación empuja hacia el borde.
                half4 portalCol = SAMPLE_TEXTURE2D(_PortalTex, sampler_PortalTex, saturate(screenUV + ripple));

                // Borde de energía: smoothstep(ancho, 0, d) vale 1 pegado al marco y 0 a partir de "ancho".
                // (Con los límites invertidos, smoothstep sube al acercarse a 0.)
                float rim = smoothstep(_BorderWidth, 0.0, edgeDist);
                // Mezclo hacia el color del marco multiplicado por 2,2 (valor HDR que enciende el Bloom).
                // Uso rim · 0,85 para que incluso en el borde se alcance a ver un poco del otro lado.
                float3 finalRgb = lerp(portalCol.rgb, _FrameColor.rgb * 2.2, rim * 0.85);

                return half4(finalRgb, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}

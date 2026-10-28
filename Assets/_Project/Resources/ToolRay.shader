// =============================================================================
// NostalgIA — Rayo visible de la herramienta
// Archivo: ToolRay.shader
//
// El shader más simple del proyecto: pinta cada vértice con el color que trae la
// malla (el LineRenderer del rayo de la herramienta) y lo mezcla con transparencia.
// No tiene propiedades; el color y el alfa los decide el componente que dibuja la línea
// (por ejemplo, el degradado del LineRenderer).
// =============================================================================

Shader "Tools/VisibleRay"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }   // Pase sin iluminación de URP.
            Blend SrcAlpha OneMinusSrcAlpha          // Mezcla alfa clásica: final = fuente·α + destino·(1 − α).
            ZWrite Off                               // Lo transparente no escribe profundidad para no tapar lo que está detrás.
            ZTest LEqual
            Cull Off                                 // La línea es una tira plana: la dibujo por las dos caras.
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };   // Posición y color por vértice.
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;   // La GPU interpola el color entre vértices: así sale el degradado.
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return input.color; }   // Devuelvo el color tal cual.
            ENDHLSL
        }
    }
}

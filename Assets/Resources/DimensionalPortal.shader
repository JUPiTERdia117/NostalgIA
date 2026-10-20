Shader "Tutorial/DimensionalPortal"
{
    Properties
    {
        _PortalTex ("Portal Render Texture", 2D) = "black" {}
        _FrameColor ("Portal Edge Glow Color", Color) = (0.1, 0.75, 1.0, 1.0)
        _BorderWidth ("Edge Glow Width", Range(0.0, 0.15)) = 0.035
        _RippleStrength ("Dimensional Ripple", Range(0.0, 0.03)) = 0.004
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PortalScreenSpace"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);

                // Subtle edge distance for energy border
                float2 dEdge = min(input.uv, 1.0 - input.uv);
                float edgeDist = min(dEdge.x, dEdge.y);

                // Very subtle dimensional ripple near the frame edges only
                float edgeFactor = saturate(1.0 - edgeDist / max(0.001, _BorderWidth * 4.0));
                float2 ripple = float2(
                    sin(_Time.y * 4.0 + input.uv.y * 18.0),
                    cos(_Time.y * 3.5 + input.uv.x * 18.0)
                ) * (_RippleStrength * edgeFactor);

                half4 portalCol = SAMPLE_TEXTURE2D(_PortalTex, sampler_PortalTex, saturate(screenUV + ripple));

                // Glowing dimensional rim at the very edge of the doorway
                float rim = smoothstep(_BorderWidth, 0.0, edgeDist);
                float3 finalRgb = lerp(portalCol.rgb, _FrameColor.rgb * 2.2, rim * 0.85);

                return half4(finalRgb, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}

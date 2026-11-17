Shader "BubbleR/StandardBubble-Textured"
{
    // Versión URP del shader de BubbleR (el original era un surface shader del pipeline Built-in
    // y en URP salía rosa). Mismas propiedades, así que los materiales y el editor siguen igual.
    Properties
    {
        [Header(Base)]
        _MainColor ("Color", Color) = (1,1,1,0.2)
        _MainTexture("Main Texture", 2D) = "white" {}
        _Glossiness("Smoothness", Range(0,1)) = 1.0
        _Metal("Metallic", Range(0,1)) = 1.0

        [Header(Bouncing)]
        _BounceAmplitude("Bounce Amplitude", Range(0,1)) = 0.2
        _BounceFrequency("Bounce Frequency Multiplier", Range(0, 5)) = 1

        [Header(Color Shifting)]
        _ColorShiftPeak("Color Shifting Offset (RGB)", Vector) = (0.3, 0.5, 0.8)
        _ColorShiftBand("Color Shifting Bands (RGB)", Vector) = (0.3, 0.2, 0.3)
        _ColorShifting("Color Shift Multiplier", Range(0,1)) = 1.0
        _ColorShiftNoise("Color Shift Noise", Vector) = (0.1, 1, 1, 0) // (multiplier, scale, speed, unused)

        [Enum(BubbleR.BlendMode)]
        _ColorShiftMode("Shift mode", Float) = 0

        [Toggle(SPATIAL_NOISE)] _SpatialNoise("Enable Spatial noise", Float) = 0
    }

    CustomEditor "BubbleShaderTexturedEditor"

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        LOD 200

        HLSLINCLUDE
        #pragma target 3.5
        #pragma multi_compile_instancing
        #pragma shader_feature_local _ SPATIAL_NOISE
        #define BUBBLER_TEXTURED
        #include "BubbleR_URP.hlsl"
        ENDHLSL

        // Pase principal: PBR de URP con transparencia "fade" (como alpha:fade del original).
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Luces adicionales (Forward y Forward+). Igual que el original ("noshadow"),
            // la burbuja no recibe sombras.
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                half4  color      : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(BubbleAttributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = BubbleDeform(input);
                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.texcoord.xy * _MainTexture_ST.xy + _MainTexture_ST.zw;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                half texAlpha;
                half4 baseColor = BubbleBaseColor(input.uv, input.color, texAlpha);

                // En la versión con textura, el alfa de la textura también atenúa el ruido.
                float3 shift = BubbleShiftColor(input.positionWS, normalWS, viewDirWS, _ColorShiftNoise.x * texAlpha);
                float3 albedo = baseColor.rgb;
                if (_ColorShiftMode < 0.5)       albedo += lerp(0.0, shift, _ColorShifting);   // ADD
                else if (_ColorShiftMode < 1.5)  albedo *= lerp(1.0, shift, _ColorShifting);   // MUL
                else                             albedo -= lerp(0.0, shift, _ColorShifting);   // SUB

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.shadowCoord = float4(0, 0, 0, 0);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = max(albedo, 0.0);
                surfaceData.metallic = _Metal;
                surfaceData.smoothness = _Glossiness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = baseColor.a;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = baseColor.a;
                return color;
            }
            ENDHLSL
        }

        // Sombras semitransparentes con trama (como el pase ShadowCaster del original).
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   alpha      : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings ShadowVert(BubbleAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(BubbleDeform(input));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                output.positionCS = positionCS;
                output.uv = input.texcoord.xy * _MainTexture_ST.xy + _MainTexture_ST.zw;
                output.alpha = _MainColor.a * input.color.a;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half texAlpha;
                half alpha = BubbleBaseColor(input.uv, half4(1, 1, 1, input.alpha), texAlpha).a;
                clip(saturate(alpha * 15.0 / 16.0) - BubbleDither(input.positionCS.xy));
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

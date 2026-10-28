// =============================================================================
// NostalgIA — Shader NOS/AeroGlass
// Vidrio "Aero" de los sistemas operativos de 2007: paneles translúcidos con tinte,
// borde brillante, reflejo del cielo y la franja de brillo que barre el cristal.
//   - Fresnel F = (1 − N·V)³ → borde más opaco y más reflejante
//   - brillo superior: la mitad alta del objeto (UV.y > 0,5) se aclara con un degradado,
//     el recurso clásico de los botones y ventanas Aero
//   - barrido: banda diagonal s = frac(p·d·escala − t·velocidad); intensidad
//     smoothstep(0, w, s)·smoothstep(2w, w, s) (rampa de subida y de bajada)
//   - _NOS_REFRACTION (PC): se ve la escena opaca detrás, desplazada por la normal, y
//     ligeramente esmerilada (cuatro muestras en cruz)
// Transparente premultiplicado.
// =============================================================================

Shader "NOS/AeroGlass"
{
    Properties
    {
        _Tint ("Tinte", Color) = (0.55, 0.85, 1, 1)
        _Opacity ("Opacidad base", Range(0, 1)) = 0.22
        _EdgeColor ("Color del borde", Color) = (0.85, 0.97, 1, 1)
        _EdgeStrength ("Intensidad del borde", Range(0, 2)) = 0.9
        _Reflection ("Reflejo del cielo", Range(0, 1.5)) = 0.6
        _TopGloss ("Brillo superior", Range(0, 1)) = 0.35
        _ShineScale ("Escala del barrido", Float) = 0.15
        _ShineSpeed ("Velocidad del barrido", Float) = 0.12
        _ShineWidth ("Ancho del barrido", Range(0.01, 0.3)) = 0.06
        _ShineStrength ("Intensidad del barrido", Range(0, 2)) = 0.8
        _RefractionStrength ("Refracción", Range(0, 0.1)) = 0.02
        _Frost ("Esmerilado", Range(0, 0.02)) = 0.004
        _Specular ("Destello del sol", Range(0, 4)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardGlass"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GlassVertex
            #pragma fragment GlassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _NOS_REFRACTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Assets/_Project/Art/Shaders/Frutiger/FrutigerCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _EdgeColor;
                half _Opacity;
                half _EdgeStrength;
                half _Reflection;
                half _TopGloss;
                float _ShineScale;
                float _ShineSpeed;
                half _ShineWidth;
                half _ShineStrength;
                half _RefractionStrength;
                half _Frost;
                half _Specular;
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
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings GlassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = (half)ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 GlassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 N = normalize((float3)input.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 L = _MainLightPosition.xyz;
                half NdotV = (half)saturate(dot(N, V));
                half rim = 1.0h - NdotV;
                half fresnel = rim * rim * rim;

                half3 sky = NOS_SkyGradient((half3)reflect(-V, N), (half3)L);
                half sun = (half)pow(saturate(dot(reflect(-V, N), L)), 250.0) * _Specular;

                // Brillo superior Aero: degradado de 0,35 a 0,08 en la mitad alta del UV.
                half top = step(0.5h, (half)input.uv.y) * lerp(1.0h, 0.25h, saturate(((half)input.uv.y - 0.5h) * 2.0h)) * _TopGloss;

                // Barrido diagonal en espacio de mundo.
                float s = frac(dot(input.positionWS, float3(0.35, 0.6, 0.2)) * _ShineScale - _Time.y * _ShineSpeed);
                half shine = smoothstep(0.0h, _ShineWidth, (half)s) * smoothstep(_ShineWidth * 2.0h, _ShineWidth, (half)s) * _ShineStrength;

                half3 highlights = _EdgeColor.rgb * fresnel * _EdgeStrength + sky * fresnel * _Reflection + (top + shine + sun);

#if defined(_NOS_REFRACTION)
                // Fondo refractado y esmerilado; el vidrio queda opaco (α = 1) y compone él mismo.
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS) + N.xy * _RefractionStrength;
                half3 behind = (SampleSceneColor(uv + float2(_Frost, 0)) + SampleSceneColor(uv - float2(_Frost, 0)) +
                                SampleSceneColor(uv + float2(0, _Frost)) + SampleSceneColor(uv - float2(0, _Frost))) * 0.25h;
                half3 color = lerp(behind, behind * _Tint.rgb, 0.6h) + _Tint.rgb * _Opacity * 0.35h + highlights;
                half alpha = 1.0h;
#else
                half alpha = saturate(_Opacity + fresnel * 0.6h + top * 0.5h + shine * 0.5h);
                half3 color = _Tint.rgb * _Opacity + highlights;
#endif
                color = MixFogColor(color, unity_FogColor.rgb * alpha, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

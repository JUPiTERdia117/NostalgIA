// #NVJOB Water Shader SAF. MIT license - license_nvjob.txt
// #NVJOB Nicholas Veselov - https://nvjob.github.io
// Versión URP para NostalgIA: misma lógica que el surface shader original,
// reescrita en HLSL para URP con la iluminación PBR de URP.

// Agua semitransparente: las texturas fluyen con valores que manda el script de agua
// (_WaterLocalUv*), el relieve se simula con paralaje y la orilla se desvanece
// comparando la profundidad del agua con la del fondo.
Shader "#NVJOB/Nature/Water Shader Type 2"
{
    Properties
    {
        _Color("Main Color", Color) = (0.15,0.161,0.16,1)
        _MainTex("Main Texture", 2D) = "white" {}
        _Detail("Main Detail", 2D) = "gray" {}
        _BumpMap("Normalmap", 2D) = "bump" {}
        _BumpMapDetail("Normalmap Detail", 2D) = "bump" {}
        _ParallaxMap("Heightmap", 2D) = "black" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
        _IntensityMt("Intensity Main Texture", Range(0.1, 5)) = 1
        _ContrastMt("Contrast Main Texture", Range(-0.5, 3)) = 1
        _MainDetailScale("Main Detail Uv", Range(0.001, 10)) = 1
        _MainDetailFlow("Main Detail Flow", Range(0, 2)) = 1
        _IntensityNm("Intensity Normalmap", Range(0.001, 10)) = 1
        _NormalmapDetailScale("Normalmap Detail Uv", Range(0.001, 10)) = 0.5
        _NormalmapDetailSpeed("Normalmap Detail Speed", Range(-1, 1)) = 0.5
        _IntensityNmDetail("Intensity Normalmap Detail", Range(0.001, 10)) = 1
        _MicrowaveScale("Microwave Uv", Range(0.5, 10)) = 1
        _IntensityMicrowave("Intensity Microwave", Range(0.001, 1.5)) = 0.5
        _ParallaxScale("Heightmap Uv", Range(0.001, 20)) = 1
        _Parallax("Heightmap", Range(-0.2, 0.2)) = 0.1
        _ReverseFlow("Reverse Flow", Range(-5, 5)) = 1
        _SoftFactor("Soft Factor", Range(0.0001, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+800" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True" }
        Cull Off
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST, _BumpMap_ST;
                half4 _Color;
                half _Glossiness, _Metallic, _IntensityMt, _ContrastMt, _MainDetailScale, _MainDetailFlow;
                half _IntensityNm, _NormalmapDetailScale, _NormalmapDetailSpeed, _IntensityNmDetail;
                half _MicrowaveScale, _IntensityMicrowave, _ParallaxScale, _Parallax, _ReverseFlow, _SoftFactor;
            CBUFFER_END

            // Desplazamiento del flujo: lo escribe cada frame el script del agua (variables globales).
            half _WaterLocalUvX, _WaterLocalUvZ, _WaterLocalUvNX, _WaterLocalUvNZ;

            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
            TEXTURE2D(_Detail);         SAMPLER(sampler_Detail);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_BumpMapDetail);  SAMPLER(sampler_BumpMapDetail);
            TEXTURE2D(_ParallaxMap);    SAMPLER(sampler_ParallaxMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float2 uvMain = TRANSFORM_TEX(input.uv, _MainTex);
                float2 uvBump = TRANSFORM_TEX(input.uv, _BumpMap);
                float2 flow = float2(_WaterLocalUvX, _WaterLocalUvZ);
                float2 flowNormal = float2(_WaterLocalUvNX, _WaterLocalUvNZ);

                // 1. Paralaje: el mapa de alturas mueve las UV según el ángulo de vista (falso relieve).
                half height = SAMPLE_TEXTURE2D(_ParallaxMap, sampler_ParallaxMap, (uvBump - flowNormal * _ReverseFlow) * _ParallaxScale).r;
                half2 offset = ParallaxOffset1Step(height, _Parallax, GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewWS));
                uvMain -= offset;
                uvBump += offset;

                // 2. Color: textura principal × detalle, cada una con su propio flujo.
                float2 uv = uvMain + flow;
                float2 uvDetail = uvMain + flow * _MainDetailFlow;
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;
                tex *= SAMPLE_TEXTURE2D(_Detail, sampler_Detail, uvDetail * _MainDetailScale) * _IntensityMt;
                half3 albedo = (tex.rgb - 0.5) * _ContrastMt + 0.5;

                // 3. Olas: normal principal + detalle − "microolas" (detalle a otra escala que lo rompe).
                float2 uvNormal = uvBump + flowNormal;
                float2 uvNormalDetail = uvBump + flowNormal * _NormalmapDetailSpeed;
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvNormal)) * _IntensityNm;
                normalTS += UnpackNormal(SAMPLE_TEXTURE2D(_BumpMapDetail, sampler_BumpMapDetail, uvNormalDetail * _NormalmapDetailScale)) * _IntensityNmDetail;
                normalTS -= UnpackNormal(SAMPLE_TEXTURE2D(_BumpMapDetail, sampler_BumpMapDetail, (uv + uvNormalDetail) * 2.0 * _MicrowaveScale)) * _IntensityMicrowave;
                normalTS = normalize(normalTS / 3.0);
                half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 normalWS = normalize(normalTS.x * input.tangentWS.xyz + normalTS.y * bitangent + normalTS.z * input.normalWS);

                // 4. Orilla suave: entre menos agua haya entre la superficie y el fondo, más transparente.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float waterDepth = -TransformWorldToView(input.positionWS).z;
                half fade = saturate(_SoftFactor * (sceneDepth - waterDepth));

                // 5. Iluminación PBR de URP (lo que el original llamaba "Standard").
                InputData light = (InputData)0;
                light.positionWS = input.positionWS;
                light.normalWS = normalWS;
                light.viewDirectionWS = viewWS;
                light.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                light.normalizedScreenSpaceUV = screenUV;
                light.fogCoord = input.fogFactor;
                light.bakedGI = SampleSH(normalWS);
                light.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = _Metallic;
                surface.smoothness = _Glossiness;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.alpha = _Color.a * fade;

                half4 color = UniversalFragmentPBR(light, surface);
                return half4(MixFog(color.rgb, input.fogFactor), surface.alpha);
            }
            ENDHLSL
        }
    }
}

// NostalgIA — NOS/ScreenSpaceReflection
// Reflejos en espacio de pantalla (SSR) para los pisos: lo más parecido a ray tracing que
// corre bien en una GPU integrada. URP 17.3 no trae SSR, por eso existe este shader.
// Se usa con el Renderer Feature «Full Screen Pass» de URP, antes de los transparentes.
//
// Idea: por cada píxel de piso se lanza un rayo en la dirección del reflejo y se avanza
// por pasos comparando contra la textura de profundidad. Si el rayo queda detrás de algo
// que ya está dibujado en pantalla, ese algo es lo que se refleja y se copia su color.
// Lo que no está en pantalla no se puede reflejar; ahí siguen mandando las Reflection Probes.

Shader "NOS/ScreenSpaceReflection"
{
    Properties
    {
        _Strength ("Fuerza del reflejo", Range(0, 2)) = 1
        _MinReflect ("Reflejo mirando de frente", Range(0, 1)) = 0.08
        _FresnelPower ("Potencia del Fresnel", Range(1, 8)) = 3
        _MaxDistance ("Distancia máxima del rayo (m)", Range(1, 80)) = 30
        _Thickness ("Grosor asumido de los objetos (m)", Range(0.05, 3)) = 0.6
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "SSR"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            #define STEPS 40          // Pasos del rayo.
            #define REFINE_STEPS 5    // Pasos de búsqueda binaria para afinar el punto de choque.

            CBUFFER_START(UnityPerMaterial)
                half _Strength, _MinReflect, _FresnelPower;
                float _MaxDistance, _Thickness;
            CBUFFER_END

            // Lo pone el componente ScreenReflections de la escena (0 = apagado en los demás niveles).
            float _NOS_ScreenReflections;

            // Profundidad de la escena en metros desde la cámara, sin derivadas (se usa dentro de un bucle).
            float SceneEyeDepth(float2 uv)
            {
                return LinearEyeDepth(SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, uv, 0).r, _ZBufferParams);
            }

            // Proyecta un punto del mundo: xy = UV de pantalla, z = distancia a la cámara en metros.
            float3 Project(float3 positionWS)
            {
                float3 ndc = ComputeNormalizedDeviceCoordinatesWithZ(positionWS, UNITY_MATRIX_VP);
                return float3(ndc.xy, -mul(UNITY_MATRIX_V, float4(positionWS, 1.0)).z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
                if (_NOS_ScreenReflections <= 0.0) return color;

                float rawDepth = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, uv, 0).r;
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.0) return color;                      // Cielo.
                #else
                    if (rawDepth >= 1.0) return color;
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                // Solo reflejan las superficies que miran hacia arriba (pisos y tapas).
                half3 normalWS = SampleSceneNormals(uv);
                half floorMask = smoothstep(0.85, 0.97, normalWS.y);
                if (floorMask <= 0.0) return color;

                float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                half3 viewDir = normalize(GetCameraPositionWS() - positionWS);
                half3 rayDir = reflect(-viewDir, normalWS);

                // El primer paso se desplaza un poco distinto en cada píxel para disimular los escalones.
                float jitter = InterleavedGradientNoise(input.positionCS.xy, 0);
                float stepLength = _MaxDistance / STEPS * 0.35;
                float3 origin = positionWS + normalWS * 0.03;
                float t = stepLength * (0.4 + jitter), previousT = 0.0;
                float2 hitUV = 0; bool hit = false;

                for (int i = 0; i < STEPS; i++)
                {
                    float3 p = Project(origin + rayDir * t);
                    if (p.x < 0.0 || p.x > 1.0 || p.y < 0.0 || p.y > 1.0 || p.z <= 0.0) break;   // Salió de la pantalla.

                    float behind = p.z - SceneEyeDepth(p.xy);        // > 0: el rayo ya está detrás de algo dibujado.
                    if (behind > 0.0 && behind < _Thickness + t * 0.08)
                    {
                        // Búsqueda binaria entre el paso anterior y este para caer justo en la superficie.
                        float a = previousT, b = t;
                        for (int j = 0; j < REFINE_STEPS; j++)
                        {
                            float mid = 0.5 * (a + b);
                            float3 m = Project(origin + rayDir * mid);
                            if (m.z - SceneEyeDepth(m.xy) > 0.0) b = mid; else a = mid;
                        }
                        hitUV = Project(origin + rayDir * b).xy;
                        t = b; hit = true;
                        break;
                    }
                    previousT = t;
                    stepLength *= 1.09;      // Pasos cada vez más largos: detalle cerca, alcance lejos.
                    t += stepLength;
                }
                if (!hit) return color;

                half3 reflection = min(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, hitUV, 0).rgb, 12.0);

                // Se desvanece cerca del borde de la pantalla y al final del alcance del rayo.
                float2 edge = saturate(min(hitUV, 1.0 - hitUV) * 9.0);
                half fade = edge.x * edge.y * saturate(1.0 - t / _MaxDistance);

                // Fresnel: casi nada mirando de frente, mucho mirando el piso de lado.
                half fresnel = lerp(_MinReflect, 1.0, pow(1.0 - saturate(dot(normalWS, viewDir)), _FresnelPower));

                color.rgb += reflection * (fresnel * fade * floorMask * _Strength * _NOS_ScreenReflections);
                return color;
            }
            ENDHLSL
        }
    }
}

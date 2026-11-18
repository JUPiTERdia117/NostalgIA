// -----------------------------------------------------------------------------
// NostalgIA — compatibilidad de los shaders de Polytope Studio con URP.
// Los shaders originales son Surface Shaders del pipeline Built-in (Amplify) y se
// veían rosas en URP. Este archivo define lo que su código daba por hecho
// (appdata_full, SurfaceOutputStandard, WorldNormalVector...) para poder reutilizar
// sus funciones vertexDataFunc y surf tal cual dentro de pases de URP.
// -----------------------------------------------------------------------------
#ifndef PT_URP_COMPAT_INCLUDED
#define PT_URP_COMPAT_INCLUDED

#define UNITY_INITIALIZE_OUTPUT(type, name) name = (type)0;

// Base tangente → mundo por píxel, igual que INTERNAL_DATA en los Surface Shaders.
#define INTERNAL_DATA float3 internalSurfaceTtoW0; float3 internalSurfaceTtoW1; float3 internalSurfaceTtoW2;
#define WorldNormalVector(data, normal) normalize(float3(dot((data).internalSurfaceTtoW0, (normal)), dot((data).internalSurfaceTtoW1, (normal)), dot((data).internalSurfaceTtoW2, (normal))))

#define UnpackScaleNormal(packed, scale) UnpackNormalScale(packed, scale)

struct appdata_full
{
    float4 vertex : POSITION;
    float4 tangent : TANGENT;
    float3 normal : NORMAL;
    float4 texcoord : TEXCOORD0;
    float4 texcoord1 : TEXCOORD1;
    float4 texcoord2 : TEXCOORD2;
    float4 texcoord3 : TEXCOORD3;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SurfaceOutputStandard
{
    half3 Albedo;
    half3 Normal;
    half3 Emission;
    half Metallic;
    half Smoothness;
    half Occlusion;
    half Alpha;
};

inline float3 UnityWorldSpaceViewDir(float3 worldPos) { return _WorldSpaceCameraPos.xyz - worldPos; }
inline float3 UnityObjectToWorldNormal(float3 normal) { return TransformObjectToWorldNormal(normal); }

inline bool IsGammaSpace()
{
#ifdef UNITY_COLORSPACE_GAMMA
    return true;
#else
    return false;
#endif
}

// Built-in tenía LinearEyeDepth de un solo argumento.
inline float LinearEyeDepth(float rawDepth) { return LinearEyeDepth(rawDepth, _ZBufferParams); }

#endif

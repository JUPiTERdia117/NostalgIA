// #NVJOB NX Shaders. MIT license - license_nvjob.txt
// Versión URP para NostalgIA.
// -----------------------------------------------------------------------------
// Piezas compartidas por los shaders NX portados a URP (peces, aves y SC Legacy).
// Los originales eran "surface shaders" del pipeline Built-in, que URP no dibuja
// (por eso salían morados). Aquí está la misma lógica escrita para URP:
//   - Vértice con un gancho NX_DEFORM para que cada shader mueva sus vértices (aleteo).
//   - Iluminación Blinn-Phong de URP, igual que el "BlinnPhong" del original.
//   - Pases de profundidad y normales para que SSAO y los reflejos de pantalla vean al objeto.
//
// Antes de incluir este archivo, cada shader declara su CBUFFER y elige su familia:
//   #define NX_BOIDS            → peces y aves (mapa especular, oclusión, normal de detalle).
//   #define NX_SC               → SC Legacy (más simple), con variantes:
//       NX_SC_OCCLUSION, NX_SC_WATER, NX_SC_CUTOUT, NX_SC_TRANSPARENT.
//   #define NX_DEFORM(p) p = MiFuncion(p)   → mueve el vértice en espacio de objeto.
// -----------------------------------------------------------------------------

#ifndef NX_URP_INCLUDED
#define NX_URP_INCLUDED

#ifndef NX_DEFORM
    #define NX_DEFORM(p)
#endif

// ---------------- Texturas ----------------

TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
TEXTURECUBE(_Cube);         SAMPLER(sampler_Cube);
#if defined(NX_BOIDS) || defined(NX_SC_OCCLUSION) || defined(NX_SC_WATER)
TEXTURE2D(_OcclusionMap);   SAMPLER(sampler_OcclusionMap);
#endif
#if defined(NX_BOIDS)
TEXTURE2D(_BumpMapD);       SAMPLER(sampler_BumpMapD);
TEXTURE2D(_SpecMap);        SAMPLER(sampler_SpecMap);
TEXTURE2D(_EmissionTex);    SAMPLER(sampler_EmissionTex);
#endif

// Los Cutout descartan los píxeles transparentes en todos los pases.
// El original multiplicaba dos veces por el alfa del tinte; se conserva igual.
#if defined(NX_SC_CUTOUT)
    #define NX_CLIP(uv) clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(uv, _MainTex)).a * _Color.a * _Color.a - _Cutoff)
#else
    #define NX_CLIP(uv)
#endif

// ---------------- Vértice ----------------

struct NXAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct NXVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;   // w = signo de la bitangente.
    half fogFactor : TEXCOORD4;
    half3 vertexSH : TEXCOORD5;    // Luz ambiente de las sondas de luz.
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Aplica la deformación del shader y pasa todo a espacio de mundo.
NXVaryings NXVert(NXAttributes input)
{
    NXVaryings output = (NXVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 positionOS = input.positionOS.xyz;
    NX_DEFORM(positionOS);

    VertexPositionInputs position = GetVertexPositionInputs(positionOS);
    VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = normal.normalWS;
    output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.uv = input.uv;
    output.fogFactor = ComputeFogFactor(position.positionCS.z);
    output.vertexSH = SampleSH(output.normalWS);
    return output;
}

// ---------------- Utilidades ----------------

// Pasa una dirección de la textura de normales (tangente) al mundo, sin normalizar.
half3 NXToWorld(NXVaryings input, half3 directionTS)
{
    half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
    return directionTS.x * input.tangentWS.xyz + directionTS.y * bitangent + directionTS.z * input.normalWS;
}

// Saturación, brillo y contraste, como en los originales.
half3 NXAdjust(half3 color, half saturation, half brightness, half contrast)
{
    half luminance = dot(color, half3(0.2126, 0.7152, 0.0722));
    color = lerp(luminance.xxx, color, saturation) * brightness;
    return (color - 0.5) * contrast + 0.5;
}

// Reflejo del cubemap. "Bias Normal" escala la normal antes de reflejar, igual que en el original.
half3 NXReflection(NXVaryings input, half3 normalTS)
{
    half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 direction = reflect(-viewWS, NXToWorld(input, normalTS * _BiasNormal));
    return SAMPLE_TEXTURECUBE(_Cube, sampler_Cube, direction).rgb * _IntensityRef;
}

// Lo que el original llamaba BlinnPhong:
//   brillo especular = pow(N·H, specular * 128) * gloss * _SpecColor.
// URP pide una "suavidad" s cuyo exponente es 2^(10 s + 1); aquí se convierte.
half3 NXShade(NXVaryings input, half3 normalWS, half3 albedo, half alpha,
              half specular, half gloss, half3 emission)
{
    InputData light = (InputData)0;
    light.positionWS = input.positionWS;
    light.normalWS = normalWS;
    light.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    light.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    light.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    light.fogCoord = input.fogFactor;
    light.bakedGI = input.vertexSH;
    light.shadowMask = half4(1, 1, 1, 1);

    SurfaceData surface = (SurfaceData)0;
    surface.albedo = albedo;
    surface.alpha = alpha;
    surface.specular = _SpecColor.rgb * gloss;
    surface.smoothness = saturate((log2(max(specular * 128.0, 2.0)) - 1.0) / 10.0);
    surface.normalTS = half3(0, 0, 1);
    surface.emission = emission;
    surface.occlusion = 1;

    half4 color = UniversalFragmentBlinnPhong(light, surface);
    return MixFog(color.rgb, input.fogFactor);
}

// ---------------- Fragmento de peces y aves ----------------

#if defined(NX_BOIDS)
half4 NXFrag(NXVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    float2 uv = TRANSFORM_TEX(input.uv, _MainTex);
    float2 uvBump = TRANSFORM_TEX(input.uv, _BumpMap);

    // Color: textura × tinte, ajustada y oscurecida por la oclusión.
    half occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv * _OcclusionMapUv).r;
    occlusion = (occlusion - 0.5) * _IntensityOc + 0.5;
    half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;
    half3 albedo = NXAdjust(tex.rgb, _Saturation, _Brightness, _Contrast) * occlusion;

    // Brillo: el canal verde del mapa especular dice qué tan pulida es cada zona.
    half specMap = (SAMPLE_TEXTURE2D(_SpecMap, sampler_SpecMap, uv * _SpecMapUV).g - 0.5) * _SpecMapInts + 0.5;

    // Normal: la principal más la de detalle (escamas o plumas).
    half3 normal = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvBump));
    half3 detail = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMapD, sampler_BumpMapD, uvBump * _BumpMapDUV));
    normal.xy *= _IntensityNm;
    detail.xy *= _IntensityNmD;
    half3 normalTS = normalize(normal + detail);

    // Reflejo del cubemap, con su propia saturación y contraste, enmascarado por la textura de emisión.
    half3 reflection = NXReflection(input, normalTS);
    half luminance = dot(reflection, half3(0.2126, 0.7152, 0.0722));
    reflection = (lerp(luminance.xxx, reflection, _SaturationRef) - 0.5) * _ContrastRef + 0.5;
    half3 emission = SAMPLE_TEXTURE2D(_EmissionTex, sampler_EmissionTex, uv).rgb * reflection * _ReflectColor.rgb;

    half3 normalWS = normalize(NXToWorld(input, normalTS));
    return half4(NXShade(input, normalWS, albedo, 1, _Shininess * specMap, _SpecColor.a, emission), 1);
}
#endif

// ---------------- Fragmento de SC Legacy ----------------

#if defined(NX_SC)
half4 NXFrag(NXVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    float2 uv = TRANSFORM_TEX(input.uv, _MainTex);
    float2 uvBump = TRANSFORM_TEX(input.uv, _BumpMap);

    half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;
    half3 albedo = NXAdjust(tex.rgb, _Saturation, _Brightness, _Contrast);

    #if defined(NX_SC_OCCLUSION)
        albedo *= SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv * _OcclusionMapUv).r * _IntensityOc;
    #elif defined(NX_SC_WATER)
        // Agua: la oclusión se mueve en dos direcciones y se promedia (manchas de luz que fluyen).
        float2 uvA = uv * _OcclusionMapUv + float2(_Time.y * 0.10 * _SpeedOcclusion, 0);
        float2 uvB = uv * _OcclusionMapUv + float2(0, _Time.y * 0.13 * _SpeedOcclusion);
        half flow = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uvA).r
                  + SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uvB).r;
        albedo *= flow * 0.5 * _IntensityOc;
    #endif

    NX_CLIP(input.uv);

    // Opacos: el alfa de la textura es el brillo. Cutout y Transparent usan el alfa de _SpecColor.
    #if defined(NX_SC_CUTOUT) || defined(NX_SC_TRANSPARENT)
        half gloss = _SpecColor.a;
    #else
        half gloss = tex.a;
    #endif

    half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvBump));
    normalTS.xy *= _IntensityNm;
    normalTS = normalize(normalTS);

    half3 emission = NXReflection(input, normalTS) * tex.a * _ReflectColor.rgb;
    half3 normalWS = normalize(NXToWorld(input, normalTS));
    half3 color = NXShade(input, normalWS, albedo, tex.a, _Shininess, gloss, emission);

    #if defined(NX_SC_TRANSPARENT)
        return half4(color, tex.a);
    #else
        return half4(color, 1);
    #endif
}
#endif

// ---------------- Pases de profundidad (SSAO, reflejos de pantalla) ----------------

struct NXDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

NXDepthVaryings NXDepthVert(NXAttributes input)
{
    NXDepthVaryings output = (NXDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 positionOS = input.positionOS.xyz;
    NX_DEFORM(positionOS);                       // El mismo aleteo que en el pase de color.
    output.positionCS = TransformObjectToHClip(positionOS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = input.uv;
    return output;
}

// Sombras: igual que la profundidad, pero vista desde la luz y con un pequeño
// desplazamiento (bias) para que la superficie no se sombree a sí misma.
float3 _LightDirection;
float3 _LightPosition;

NXDepthVaryings NXShadowVert(NXAttributes input)
{
    NXDepthVaryings output = (NXDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 positionOS = input.positionOS.xyz;
    NX_DEFORM(positionOS);
    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 lightDirection = normalize(_LightPosition - positionWS);
    #else
        float3 lightDirection = _LightDirection;
    #endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    output.positionCS = positionCS;
    output.normalWS = normalWS;
    output.uv = input.uv;
    return output;
}

half NXDepthFrag(NXDepthVaryings input) : SV_Target
{
    NX_CLIP(input.uv);
    return input.positionCS.z;
}

half4 NXDepthNormalsFrag(NXDepthVaryings input) : SV_Target
{
    NX_CLIP(input.uv);
    return half4(normalize(input.normalWS), 0);
}

#endif

// =============================================================================
// NostalgIA — Pase Forward del shader NOS/StationKit
// Archivo: StationKitForwardPass.hlsl
// Tarea: F1-B02
//
// Superficie procedural para el kit modular de las Estaciones (estética Webcore):
//   - Proyección triplanar "dura" en espacio de mundo: cada cara elige el plano de su
//     eje dominante (piso/techo → XZ, muro → ZY o XY). Las piezas del kit son cajas
//     alineadas a los ejes, así que no hace falta mezclar tres muestras.
//   - Pisos: baldosas de 1 m con variación de tono por celda (hash) y grano pixelado.
//   - Muros: gradiente vertical, segmentos de "código" emisivos por fila y una banda
//     de escaneo que recorre la pared hacia arriba.
//   - Rejilla emisiva antialiasada analíticamente en todas las caras (fwidth).
//   - Iluminación Blinn-Phong de URP (luz principal con sombras, luces adicionales con
//     el bucle por clústeres de Forward+, SH/lightmaps y niebla).
//
// Costo estimado (fragmento): ~4 hashes PCG + 1 rejilla + iluminación Blinn-Phong.
// Sin texturas: cero ancho de banda de muestreo, ideal para GPUs móviles TBDR.
// =============================================================================

#ifndef NOS_STATION_KIT_FORWARD_INCLUDED
#define NOS_STATION_KIT_FORWARD_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Assets/_Project/Art/Shaders/Include/NostalgIA_Common.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

// Datos de vértice que entrega la malla.
struct Attributes
{
    float4 positionOS        : POSITION;   // Posición en espacio de objeto.
    float3 normalOS          : NORMAL;     // Normal en espacio de objeto.
    float2 staticLightmapUV  : TEXCOORD1;  // UV del lightmap horneado (si existe).
    float2 dynamicLightmapUV : TEXCOORD2;  // UV del lightmap dinámico (GI en tiempo real).
    UNITY_VERTEX_INPUT_INSTANCE_ID         // Índice de instancia para GPU Instancing.
};

// Datos interpolados del vértice al fragmento.
struct Varyings
{
    float3 positionWS : TEXCOORD0;         // Posición de mundo: base de la proyección procedural.
    half3 normalWS    : TEXCOORD1;         // Normal de mundo (precisión half, suficiente para direcciones).
    half fogFactor    : TEXCOORD2;         // Factor de niebla calculado por vértice.
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD3;        // Coordenada de sombra cuando URP la calcula por vértice.
#endif
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4); // UV de lightmap o armónicos esféricos por vértice.
#ifdef DYNAMICLIGHTMAP_ON
    float2 dynamicLightmapUV : TEXCOORD5;  // UV del lightmap dinámico.
#endif
#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD6;     // Oclusión de sondas APV (si el proyecto las usa).
#endif
    float4 positionCS : SV_POSITION;       // Posición en espacio de recorte.
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// -----------------------------------------------------------------------------
// Vértice
// -----------------------------------------------------------------------------
Varyings StationKitVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);                    // Selecciona la matriz de la instancia.
    UNITY_TRANSFER_INSTANCE_ID(input, output);         // Propaga el índice al fragmento.
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);     // Soporte de renderizado estéreo (sin costo en mono).

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz); // Objeto → mundo → vista → recorte.
    output.positionWS = positionInputs.positionWS;
    output.positionCS = positionInputs.positionCS;
    output.normalWS = NormalizeNormalPerVertex(TransformObjectToWorldNormal(input.normalOS)); // Normal con la inversa transpuesta.
    output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z); // Niebla lineal/exponencial por vértice.

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV); // Escala y desplazamiento del atlas.
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    // Armónicos esféricos de la luz ambiental (o muestreo diferido a fragmento según la calidad).
    OUTPUT_SH4(positionInputs.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(positionInputs.positionWS), output.vertexSH, output.probeOcclusion);

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(positionInputs); // Coordenada en el mapa de sombras en cascada.
#endif
    return output;
}

// -----------------------------------------------------------------------------
// Patrón procedural
// -----------------------------------------------------------------------------

// Resultado del patrón: albedo y emisión de la superficie.
struct KitPattern
{
    half3 albedo;
    half3 emission;
};

// Calcula el patrón según la orientación de la cara.
KitPattern EvaluateKitPattern(float3 positionWS, half3 normalWS)
{
    KitPattern pattern;
    half3 absNormal = abs(normalWS);

    // Eje dominante: piso o techo si la normal apunta mayormente en Y.
    bool horizontal = absNormal.y >= max(absNormal.x, absNormal.z);
    // Coordenada 2D del plano proyectado (metros).
    float2 planeUV = horizontal ? positionWS.xz : (absNormal.x > absNormal.z ? positionWS.zy : positionWS.xy);

    // Huella del píxel de pantalla en metros del plano: fwidth = |∂p/∂x| + |∂p/∂y|. Sirve para
    // apagar los patrones cuya frecuencia supera la de muestreo (si una celda mide menos de dos
    // píxeles en pantalla, su hash cambia de un frame a otro y se ve como "nieve" que parpadea).
    float2 footprint = fwidth(planeUV);
    float pixelMeters = max(footprint.x, footprint.y);

    // Grano pixelado: un valor aleatorio por "texel" de 1/_PixelsPerMeter m, centrado en 0.
    // Atenuación: 1 mientras el texel ocupa ≥ 2 píxeles, 0 cuando ocupa ≤ 1 píxel.
    half grainFade = (half)saturate(2.0 - 2.0 * pixelMeters * _PixelsPerMeter);
    half grain = (half)(NOS_Hash21(floor(planeUV * _PixelsPerMeter)) - 0.5) * _GrainStrength * grainFade;

    // Rejilla emisiva de periodo 1/_GridScale m con antialiasing analítico.
    half grid = (half)NOS_GridLines(planeUV * _GridScale, _LineWidth);

    if (horizontal)
    {
        // Baldosas: tono por celda, cuantizado a 3 niveles para un aspecto de paleta limitada.
        float cellValue = NOS_Hash21(floor(planeUV * _GridScale) + 17.0);  // +17 desacopla este hash del grano.
        half tone = (half)(floor(cellValue * 3.0) / 2.0);                    // Niveles 0, 0,5 y 1.
        half3 floorColor = lerp(_FloorColorA.rgb, _FloorColorB.rgb, tone);
        // Las caras hacia abajo usan el color del techo.
        half3 baseColor = normalWS.y >= 0.0h ? floorColor : _CeilingColor.rgb;
        pattern.albedo = saturate(baseColor + grain);
        pattern.emission = _LineColor.rgb * (grid * _LineEmission);
    }
    else
    {
        // Gradiente vertical del muro según la altura de mundo.
        half heightT = (half)NOS_InverseLerp01(0.0, max(_WallHeight, 0.01), positionWS.y);
        half3 wallColor = lerp(_WallColorBottom.rgb, _WallColorTop.rgb, heightT);
        pattern.albedo = saturate(wallColor + grain);

        // Segmentos de "código": filas de 1/8 m divididas en segmentos de 1/4 m; cada
        // segmento se enciende si su hash supera (1 − densidad). Una segunda escala de
        // pixelado recorta el segmento en "caracteres" para leerlo como texto.
        float2 codeCell = floor(planeUV * float2(4.0, 8.0));
        half segmentOn = (half)step(1.0 - _CodeDensity, NOS_Hash21(codeCell + 41.0));
        float2 inCell = frac(planeUV * float2(4.0, 8.0));
        half character = (half)step(0.35, NOS_Hash21(floor(planeUV * float2(24.0, 8.0)) + 7.0)); // Huecos entre "caracteres".
        half rowGap = (half)step(0.25, inCell.y) * (half)step(inCell.y, 0.75);                   // Deja espacio entre filas.
        // Los "caracteres" miden 1/24 m de ancho: con el mismo criterio que el grano, el código se
        // desvanece cuando un carácter ocupa menos de dos píxeles (muros lejanos o rasantes) y
        // queda solo la rejilla, que sí tiene antialiasing analítico.
        half codeFade = (half)saturate(2.0 - 2.0 * pixelMeters * 24.0);
        half code = segmentOn * character * rowGap * codeFade;

        // Banda de escaneo: fase periódica que sube con el tiempo; la intensidad decae
        // exponencialmente detrás del frente: exp(−8·fase).
        float scanPhase = frac(positionWS.y * 0.1 - _Time.y * _ScanSpeed * 0.1);
        half scan = (half)exp(-8.0 * scanPhase);

        pattern.emission = _LineColor.rgb * (grid * _LineEmission + code * _CodeEmission * (0.35h + scan) + scan * _ScanEmission);
    }

    // Pulso global de daño (PostFXController): tiñe la emisión de la rejilla hacia rojo.
    half pulse = (half)saturate(_NOS_DamagePulse) * _DamagePulseTint;
    pattern.emission = lerp(pattern.emission, half3(1.0h, 0.15h, 0.2h) * (grid + 0.15h), pulse);
    return pattern;
}

// -----------------------------------------------------------------------------
// Fragmento
// -----------------------------------------------------------------------------
void StationKitFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);           // Transición de LOD por dithering.
#endif

    half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    KitPattern pattern = EvaluateKitPattern(input.positionWS, normalWS);

    // Datos de superficie para el modelo Blinn-Phong de URP.
    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = pattern.albedo;
    surfaceData.specular = half3(0.2h, 0.2h, 0.2h) * _Smoothness;  // Especular gris proporcional al brillo.
    surfaceData.smoothness = _Smoothness;
    surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);                // Sin mapa de normales.
    surfaceData.emission = pattern.emission;
    surfaceData.occlusion = 1.0h;
    surfaceData.alpha = 1.0h;

    // Datos de entrada de iluminación (equivalente a InitializeInputData de SimpleLit).
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
    inputData.vertexLighting = half3(0, 0, 0);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    // Iluminación global horneada o armónicos esféricos.
#if defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH, GetAbsolutePositionWS(inputData.positionWS), inputData.normalWS,
                                  inputData.viewDirectionWS, input.positionCS.xy, input.probeOcclusion, inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif

    half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData); // Luz principal + adicionales + ambiente + emisión.
    color.rgb = MixFog(color.rgb, inputData.fogCoord);                 // Niebla de RenderSettings.
    color.a = 1.0h;                                                     // Superficie opaca.
    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif // NOS_STATION_KIT_FORWARD_INCLUDED

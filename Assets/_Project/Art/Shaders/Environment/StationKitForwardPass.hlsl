// =============================================================================
// NostalgIA — Pase Forward del shader NOS/StationKit
// Archivo: StationKitForwardPass.hlsl
//
// Aquí está todo lo que se ve del kit modular de las Estaciones:
//   - Proyección por eje dominante en espacio de mundo: cada cara usa el plano hacia el
//     que mira (piso/techo → XZ, muro → ZY o XY). Las piezas del kit son cajas alineadas
//     a los ejes, así que no necesito mezclar tres proyecciones como en un triplanar
//     completo; con elegir una me ahorro dos tercios del trabajo.
//   - Todas las caras: un solo color base y una rejilla de neón de 1 m con antialiasing
//     calculado (fwidth).
//   - Muros: además, segmentos de "código" que brillan y una banda de escaneo que sube.
//   - Luz Blinn-Phong de URP: luz principal con sombras, luces adicionales (Forward+),
//     luz ambiental (armónicos esféricos o lightmaps horneados) y niebla.
//
// Costo aproximado por píxel: dos hashes PCG (solo en muros), una rejilla y la iluminación Blinn-Phong.
// No lee ninguna textura, así que no gasta ancho de banda de memoria (lo más caro en un
// teléfono).
// =============================================================================

#ifndef NOS_STATION_KIT_FORWARD_INCLUDED
#define NOS_STATION_KIT_FORWARD_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Assets/_Project/Art/Shaders/Include/NostalgIA_Common.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

// Lo que trae cada vértice de la malla.
struct Attributes
{
    float4 positionOS        : POSITION;   // Posición en espacio de objeto.
    float3 normalOS          : NORMAL;     // Normal en espacio de objeto.
    float2 staticLightmapUV  : TEXCOORD1;  // UV del lightmap horneado (cuando la escena tiene luz cocinada).
    float2 dynamicLightmapUV : TEXCOORD2;  // UV del lightmap dinámico (no lo usamos: toda nuestra luz es horneada).
    UNITY_VERTEX_INPUT_INSTANCE_ID         // Índice de instancia para GPU Instancing.
};

// Lo que paso del vértice al píxel; la GPU interpola estos valores entre los tres vértices del triángulo.
struct Varyings
{
    float3 positionWS : TEXCOORD0;         // Posición de mundo: de aquí sale todo el patrón.
    half3 normalWS    : TEXCOORD1;         // Normal de mundo. Uso half porque una dirección de longitud 1 no necesita más precisión.
    half fogFactor    : TEXCOORD2;         // Niebla calculada por vértice (más barato que por píxel).
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD3;        // Coordenada de sombra cuando URP la calcula por vértice.
#endif
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4); // Macro de URP: UV de lightmap si hay luz horneada, o luz ambiental por vértice si no.
#ifdef DYNAMICLIGHTMAP_ON
    float2 dynamicLightmapUV : TEXCOORD5;  // UV del lightmap dinámico.
#endif
#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD6;     // Oclusión de Adaptive Probe Volumes; solo existe si se activan.
#endif
    float4 positionCS : SV_POSITION;       // Posición en espacio de recorte (la que usa la GPU para rasterizar).
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// -----------------------------------------------------------------------------
// Vértice
// -----------------------------------------------------------------------------
Varyings StationKitVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);                    // Con GPU Instancing, elijo la matriz de esta instancia.
    UNITY_TRANSFER_INSTANCE_ID(input, output);         // Y paso su índice al píxel.
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);     // Soporte de realidad virtual; en pantalla normal no cuesta nada.

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz); // Cadena de espacios: objeto → mundo → vista → recorte.
    output.positionWS = positionInputs.positionWS;
    output.positionCS = positionInputs.positionCS;
    // Las normales se transforman con la inversa transpuesta de la matriz del objeto: si la pieza
    // está escalada de forma no uniforme, así siguen siendo perpendiculares a la superficie.
    output.normalWS = NormalizeNormalPerVertex(TransformObjectToWorldNormal(input.normalOS));
    output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z); // Niebla según la profundidad (lineal o exponencial, la que tenga la escena).

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV); // Ubica este objeto dentro del atlas de lightmaps (escala + desplazamiento).
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    // Luz ambiental en armónicos esféricos (una forma compacta de guardar "de qué color llega la
    // luz desde cada dirección"). Según la calidad, URP la evalúa aquí o la deja para el píxel.
    OUTPUT_SH4(positionInputs.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(positionInputs.positionWS), output.vertexSH, output.probeOcclusion);

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(positionInputs); // Dónde cae este vértice en el mapa de sombras.
#endif
    return output;
}

// -----------------------------------------------------------------------------
// Patrón procedural
// -----------------------------------------------------------------------------

// Lo que devuelve el patrón: el color base (albedo, recibe luz) y la emisión (brilla sola).
struct KitPattern
{
    half3 albedo;
    half3 emission;
};

// Calculo el patrón según hacia dónde mira la cara.
KitPattern EvaluateKitPattern(float3 positionWS, half3 normalWS)
{
    KitPattern pattern;
    half3 absNormal = abs(normalWS);

    // Eje dominante: si la componente Y de la normal es la mayor, la cara es piso o techo.
    bool horizontal = absNormal.y >= max(absNormal.x, absNormal.z);
    // Coordenada 2D en metros sobre el plano elegido: XZ para pisos, ZY para muros que miran a X, XY para los que miran a Z.
    float2 planeUV = horizontal ? positionWS.xz : (absNormal.x > absNormal.z ? positionWS.zy : positionWS.xy);

    // Todas las caras usan el mismo color base.
    pattern.albedo = _WallColorTop.rgb;

    // Rejilla de neón: una línea cada metro, con antialiasing (ver NOS_GridLines).
    half grid = (half)NOS_GridLines(planeUV, _LineWidth);
    pattern.emission = _LineColor.rgb * (grid * _LineEmission);
    if (horizontal) return pattern;

    // ---- Solo muros: código y banda de escaneo ----

    // Cuántos metros de superficie cubre un píxel de pantalla: fwidth = |∂p/∂x| + |∂p/∂y|.
    // Lo uso para apagar el código cuando sus caracteres ya son más chicos que dos píxeles;
    // si no, su patrón aleatorio parpadea como "nieve" al mover la cámara.
    float2 footprint = fwidth(planeUV);
    float pixelMeters = max(footprint.x, footprint.y);

    // Segmentos de "código": divido el muro en filas de 1/8 m y segmentos de 1/4 m. Cada segmento
    // se enciende si su hash supera (1 − densidad): con densidad 0,35 se enciende el 35 %.
    // step(a, b) devuelve 1 si b ≥ a y 0 si no; es un "if" sin ramificación.
    float2 codeCell = floor(planeUV * float2(4.0, 8.0));
    half segmentOn = (half)step(1.0 - _CodeDensity, NOS_Hash21(codeCell + 41.0));
    float2 inCell = frac(planeUV * float2(4.0, 8.0));
    // Corto cada segmento en "caracteres" de 1/24 m; el 35 % quedan apagados y parecen espacios entre letras.
    half character = (half)step(0.35, NOS_Hash21(floor(planeUV * float2(24.0, 8.0)) + 7.0));
    // Solo dibujo la mitad central de la fila (de 0,25 a 0,75) para dejar interlineado.
    half rowGap = (half)step(0.25, inCell.y) * (half)step(inCell.y, 0.75);
    half codeFade = (half)saturate(2.0 - 2.0 * pixelMeters * 24.0);
    half code = segmentOn * character * rowGap * codeFade;

    // Banda de escaneo que sube por la pared:
    //   fase = frac(0,1·y − 0,1·velocidad·t) se repite cada 10 m de altura y baja con el tiempo,
    //   así el punto donde fase = 0 (el frente de la banda) va subiendo.
    //   exp(−8·fase) vale 1 justo en el frente y se apaga rápido hacia arriba (1 m arriba ya vale 0,45).
    float scanPhase = frac(positionWS.y * 0.1 - _Time.y * _ScanSpeed * 0.1);
    half scan = (half)exp(-8.0 * scanPhase);

    // Emisión del muro = rejilla + código (que se aviva cuando pasa la banda) + la banda misma.
    pattern.emission += _LineColor.rgb * (code * _CodeEmission * (0.35h + scan) + scan * _ScanEmission);
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
    LODFadeCrossFade(input.positionCS);           // Transición suave entre LODs con dithering (descarta píxeles en patrón).
#endif

    half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    KitPattern pattern = EvaluateKitPattern(input.positionWS, normalWS);

    // Lleno los datos de superficie que espera la iluminación Blinn-Phong de URP.
    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = pattern.albedo;
    surfaceData.specular = half3(0.06h, 0.06h, 0.06h);              // Reflejo gris neutro y suave (antes lo daba el "Brillo" en 0,3).
    surfaceData.smoothness = 0.3h;
    surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);                // Sin mapa de normales: la normal "plana" (0, 0, 1).
    surfaceData.emission = pattern.emission;
    surfaceData.occlusion = 1.0h;
    surfaceData.alpha = 1.0h;

    // Datos que necesita la iluminación (es lo mismo que hace InitializeInputData en SimpleLit).
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

    // Luz ambiental: lightmap horneado si existe; si no, armónicos esféricos (o Probe Volumes si estuvieran activos).
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

    // Blinn-Phong: difusa = albedo · luz · max(0, N·L) y especular = (N·H)^brillo, con H a medio camino
    // entre la luz y la cámara. URP suma luz principal + adicionales + ambiente + emisión.
    half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);                 // Mezclo con la niebla de la escena.
    color.a = 1.0h;                                                     // Opaco.
    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif // NOS_STATION_KIT_FORWARD_INCLUDED

// =============================================================================
// NostalgIA — Biblioteca común de shaders
// Archivo: NostalgIA_Common.hlsl
// Tarea: F1-B05 (TDD §8.1.1, convención 4)
//
// Funciones puras (sin estado ni texturas) que comparten todos los shaders NOS/*:
//   1. Hashes enteros y flotantes para pseudoaleatoriedad determinista.
//   2. Ruido de valor 2D/3D con interpolación quíntica.
//   3. Paleta coseno de Íñigo Quílez:  color(t) = a + b·cos(2π(c·t + d)).
//   4. Término de Fresnel de Schlick simplificado: F = (1 − saturate(N·V))^p.
//   5. Matriz de dithering ordenado Bayer 4×4 para transparencias sin mezcla.
//   6. Utilidades de cuadrícula y pixelado usadas por la estética Webcore/Y2K.
//
// Decisiones de diseño:
//   - En este módulo se decidió no incluir ningún archivo de URP: la biblioteca
//     solo usa tipos y funciones intrínsecas de HLSL, por lo que puede incluirse
//     en cualquier pase (Forward, ShadowCaster, partículas) sin dependencias.
//   - Se usa `float` en coordenadas y `half` solo en resultados de color, según
//     la convención 5 del TDD: las coordenadas de mundo grandes pierden precisión
//     en `half` (10 bits de mantisa ≈ 3 cifras decimales).
//   - Todas las funciones van precedidas de `NOS_` para no chocar con nombres
//     de la biblioteca de URP ni de shaders de terceros.
// =============================================================================

#ifndef NOSTALGIA_COMMON_INCLUDED
#define NOSTALGIA_COMMON_INCLUDED

// Constante 2π utilizada por la paleta coseno y las ondas.
#define NOS_TWO_PI 6.28318530718

// -----------------------------------------------------------------------------
// 1. HASHES
// -----------------------------------------------------------------------------

// Hash entero de 32 bits ("PCG hash" de Jarzynski y Olano, 2020).
// Se calcula un paso del generador congruencial lineal  s = s·747796405 + 2891336453
// seguido de una permutación por desplazamientos y multiplicación que dispersa los
// bits. Ofrece mejor distribución que los hashes basados en `frac(sin(x))`, que
// en GPUs móviles con `sin` de baja precisión producen patrones visibles.
uint NOS_PcgHash(uint input)
{
    uint state = input * 747796405u + 2891336453u;                       // Paso LCG.
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u; // Permutación RXS-M.
    return (word >> 22u) ^ word;                                         // Mezcla final de bits altos y bajos.
}

// Hash de un escalar a [0, 1). Se convierte el patrón de bits del flotante a entero
// (asuint) para que valores cercanos produzcan resultados no correlacionados.
float NOS_Hash11(float p)
{
    return NOS_PcgHash(asuint(p)) * (1.0 / 4294967296.0); // Divide entre 2^32 para normalizar.
}

// Hash de una celda 2D a [0, 1). Las coordenadas se combinan antes del hash con un
// primo grande en la componente y, para que (x, y) y (y, x) den valores distintos.
float NOS_Hash21(float2 p)
{
    uint2 q = asuint(floor(p));                       // Celda entera; floor evita depender de la parte fraccionaria.
    uint h = NOS_PcgHash(q.x + NOS_PcgHash(q.y));     // Hash anidado: h(x + h(y)).
    return h * (1.0 / 4294967296.0);
}

// Hash de una celda 3D a [0, 1), con el mismo anidamiento: h(x + h(y + h(z))).
float NOS_Hash31(float3 p)
{
    uint3 q = asuint(floor(p));
    uint h = NOS_PcgHash(q.x + NOS_PcgHash(q.y + NOS_PcgHash(q.z)));
    return h * (1.0 / 4294967296.0);
}

// -----------------------------------------------------------------------------
// 2. RUIDO DE VALOR
// -----------------------------------------------------------------------------

// Curva de interpolación quíntica de Perlin: s(t) = 6t⁵ − 15t⁴ + 10t³.
// A diferencia del smoothstep cúbico (3t² − 2t³), anula también la segunda derivada
// en los bordes de cada celda, lo que elimina las discontinuidades visibles en la
// iluminación cuando el ruido se usa para desplazar normales o vértices.
float2 NOS_Quintic(float2 t) { return t * t * t * (t * (t * 6.0 - 15.0) + 10.0); }
float3 NOS_Quintic(float3 t) { return t * t * t * (t * (t * 6.0 - 15.0) + 10.0); }

// Ruido de valor 2D en [0, 1]: se asigna un valor aleatorio a cada esquina de la celda
// entera y se interpola bilinealmente con los pesos quínticos.
float NOS_ValueNoise2D(float2 p)
{
    float2 i = floor(p);                         // Esquina inferior izquierda de la celda.
    float2 f = frac(p);                          // Posición dentro de la celda, en [0, 1).
    float a = NOS_Hash21(i);                     // Valor en (0, 0).
    float b = NOS_Hash21(i + float2(1.0, 0.0));  // Valor en (1, 0).
    float c = NOS_Hash21(i + float2(0.0, 1.0));  // Valor en (0, 1).
    float d = NOS_Hash21(i + float2(1.0, 1.0));  // Valor en (1, 1).
    float2 u = NOS_Quintic(f);                   // Pesos suavizados.
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y); // Interpolación bilineal.
}

// Ruido de valor 3D en [0, 1]: interpolación trilineal de las 8 esquinas de la celda.
float NOS_ValueNoise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = NOS_Quintic(f);
    // Cara z = 0.
    float n000 = NOS_Hash31(i);
    float n100 = NOS_Hash31(i + float3(1, 0, 0));
    float n010 = NOS_Hash31(i + float3(0, 1, 0));
    float n110 = NOS_Hash31(i + float3(1, 1, 0));
    // Cara z = 1.
    float n001 = NOS_Hash31(i + float3(0, 0, 1));
    float n101 = NOS_Hash31(i + float3(1, 0, 1));
    float n011 = NOS_Hash31(i + float3(0, 1, 1));
    float n111 = NOS_Hash31(i + float3(1, 1, 1));
    float z0 = lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y);
    float z1 = lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y);
    return lerp(z0, z1, u.z);
}

// Ruido fractal (fBm) de 2 a 4 octavas: Σ ruido(p·2ⁱ)·0,5ⁱ, normalizado a [0, 1].
// El número de octavas es un parámetro constante para que el compilador desenrolle
// el bucle; los shaders móviles usan 2 octavas.
float NOS_Fbm2D(float2 p, int octaves)
{
    float sum = 0.0;
    float amplitude = 0.5;
    float norm = 0.0;
    [unroll(4)]
    for (int i = 0; i < octaves; i++)
    {
        sum += NOS_ValueNoise2D(p) * amplitude; // Suma de la octava ponderada.
        norm += amplitude;                      // Acumula el peso total para normalizar.
        p *= 2.0;                               // Duplica la frecuencia (lacunaridad 2).
        amplitude *= 0.5;                       // Reduce la amplitud a la mitad (ganancia 0,5).
    }
    return sum / max(norm, 1e-4);
}

// -----------------------------------------------------------------------------
// 3. PALETA COSENO
// -----------------------------------------------------------------------------

// color(t) = a + b·cos(2π(c·t + d)).
//   a: brillo medio, b: contraste, c: frecuencia por canal, d: desfase por canal.
// Con cuatro vectores se obtiene un gradiente continuo y periódico; lo usamos para la
// iridiscencia de Frutiger Aero y para variar tonos de celdas sin texturas.
half3 NOS_CosinePalette(float t, half3 a, half3 b, half3 c, half3 d)
{
    return a + b * cos(NOS_TWO_PI * (c * t + d));
}

// -----------------------------------------------------------------------------
// 4. FRESNEL
// -----------------------------------------------------------------------------

// Aproximación del efecto Fresnel para bordes brillantes: F = (1 − saturate(N·V))^p.
// N y V deben estar normalizados. p alto (4–6) concentra el brillo en la silueta.
half NOS_Fresnel(half3 normalWS, half3 viewDirWS, half power)
{
    half nDotV = saturate(dot(normalWS, viewDirWS)); // Coseno entre normal y vista.
    return pow(1.0h - nDotV, power);                 // Crece hacia los bordes (N ⟂ V).
}

// -----------------------------------------------------------------------------
// 5. DITHERING BAYER 4×4
// -----------------------------------------------------------------------------

// Umbral de la matriz de Bayer 4×4 normalizada a (0, 1):
//      0  8  2 10
//     12  4 14  6      umbral = (valor + 0,5) / 16
//      3 11  1  9
//     15  7 13  5
// Comparando un alfa con este umbral por píxel (`clip(alpha − umbral)`) se simulan
// 16 niveles de transparencia escribiendo profundidad: evita el costo de mezcla y de
// ordenamiento de los objetos transparentes, especialmente caro en GPUs TBDR móviles.
float NOS_Bayer4x4(float2 pixelCoord)
{
    uint2 p = uint2(pixelCoord) & 3u;   // Posición del píxel dentro del patrón 4×4 (módulo 4 con máscara de bits).
    // Matriz codificada en cuatro filas de 4 valores de 4 bits (0–15) para evitar arreglos locales.
    uint row = p.y == 0u ? 0xA280u : (p.y == 1u ? 0x6E4Cu : (p.y == 2u ? 0x91B3u : 0x5D7Fu));
    uint value = (row >> (p.x * 4u)) & 0xFu; // Extrae la columna x de la fila.
    return (value + 0.5) / 16.0;
}

// -----------------------------------------------------------------------------
// 6. CUADRÍCULA Y PIXELADO
// -----------------------------------------------------------------------------

// Cuantiza una coordenada a una rejilla de `pixelsPerUnit` celdas por unidad: es el
// pixelado de textura de la estética retro (32 px por metro en Primavera, guía §5.1).
float2 NOS_Pixelate(float2 uv, float pixelsPerUnit)
{
    return (floor(uv * pixelsPerUnit) + 0.5) / pixelsPerUnit; // Centro de la celda para evitar parpadeo en bordes.
}

// Líneas de una rejilla de periodo 1 con ancho `width` (en fracción de celda) y
// antialiasing analítico: se divide la distancia a la línea entre la derivada en
// pantalla (fwidth), de modo que el borde mide siempre ≈ 1 píxel y la rejilla no
// produce moiré en la distancia.
float NOS_GridLines(float2 coord, float width)
{
    float2 derivative = max(fwidth(coord), 1e-5);              // Cambio de la coordenada por píxel.
    float2 distanceToLine = abs(frac(coord - 0.5) - 0.5);      // Distancia a la línea entera más cercana, en [0, 0,5].
    float2 lines = 1.0 - saturate((distanceToLine - width * 0.5) / derivative); // 1 sobre la línea, 0 fuera, borde de 1 px.
    // Se atenúa la rejilla cuando una celda ocupa menos de ~2 píxeles: a esa distancia
    // las líneas solo generarían ruido.
    float fade = saturate(1.0 - max(derivative.x, derivative.y) * 2.0);
    return max(lines.x, lines.y) * fade;
}

// Interpolación lineal inversa con saturación: devuelve en qué fracción de [a, b] cae x.
float NOS_InverseLerp01(float a, float b, float x)
{
    return saturate((x - a) / (b - a));
}

#endif // NOSTALGIA_COMMON_INCLUDED

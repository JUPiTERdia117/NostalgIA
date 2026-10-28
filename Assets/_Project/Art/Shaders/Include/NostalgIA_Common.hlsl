// =============================================================================
// NostalgIA — Biblioteca común de shaders
// Archivo: NostalgIA_Common.hlsl
//
// Aquí junté las funciones matemáticas que comparten todos nuestros shaders NOS/*.
// Ninguna lee texturas ni guarda estado: reciben números y devuelven números, así
// que se pueden llamar desde cualquier pase sin efectos secundarios.
//
// Lo que hay en este archivo:
//   1. Hashes: convierten una coordenada en un número "aleatorio" que siempre es el
//      mismo para la misma entrada (sirve para variar tonos por celda sin texturas).
//   2. Ruido de valor 2D/3D y fBm: manchas suaves y orgánicas a partir de los hashes.
//   3. Paleta coseno:  color(t) = a + b·cos(2π(c·t + d)).
//   4. Fresnel aproximado:  F = (1 − saturate(N·V))^p, para bordes que brillan.
//   5. Dithering Bayer 4×4: transparencia "falsa" sin mezcla de colores.
//   6. Utilidades de rejilla y pixelado para la estética Webcore/Y2K.
//
// Decisiones que tomé:
//   - No incluyo nada de URP aquí. Solo uso tipos y funciones nativas de HLSL, así
//     puedo meter esta biblioteca en el pase Forward, en el de sombras o en un shader
//     de partículas sin arrastrar dependencias.
//   - Uso `float` para coordenadas y `half` solo para colores. Un `half` tiene 10 bits
//     de mantisa (unas 3 cifras decimales): en coordenadas de mundo como 245,37 m ya
//     pierde los centímetros y los patrones empiezan a temblar.
//   - Todas las funciones empiezan con `NOS_` para que no choquen con nombres de URP
//     ni de shaders de terceros.
// =============================================================================

#ifndef NOSTALGIA_COMMON_INCLUDED
#define NOSTALGIA_COMMON_INCLUDED

// 2π, la uso en la paleta coseno y en las ondas (una vuelta completa en radianes).
#define NOS_TWO_PI 6.28318530718

// -----------------------------------------------------------------------------
// 1. HASHES
// -----------------------------------------------------------------------------

// Hash entero de 32 bits ("PCG hash", Jarzynski y Olano, 2020).
// Primero doy un paso de generador congruencial lineal: s = s·747796405 + 2891336453.
// Luego revuelvo los bits con desplazamientos y una multiplicación para que un cambio
// pequeño en la entrada cambie muchos bits de la salida.
// Lo preferí sobre el truco clásico `frac(sin(x)·43758)` porque en GPUs móviles el
// `sin` tiene poca precisión y ese truco deja patrones repetidos que se notan.
uint NOS_PcgHash(uint input)
{
    uint state = input * 747796405u + 2891336453u;                       // Paso del generador lineal.
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u; // Desplazamiento variable + multiplicación (permutación RXS-M).
    return (word >> 22u) ^ word;                                         // Mezclo los bits altos con los bajos.
}

// Hash de un número a [0, 1).
// `asuint` reinterpreta los bits del flotante como entero (no lo redondea), así 1,0 y
// 1,0001 dan resultados que no se parecen en nada.
float NOS_Hash11(float p)
{
    return NOS_PcgHash(asuint(p)) * (1.0 / 4294967296.0); // Divido entre 2^32 para llevarlo a [0, 1).
}

// Hash de una celda 2D a [0, 1).
// Anido los hashes, h(x + h(y)), para que (3, 5) y (5, 3) no den el mismo valor.
float NOS_Hash21(float2 p)
{
    uint2 q = asuint(floor(p));                       // Me quedo con la celda entera; la parte decimal no importa.
    uint h = NOS_PcgHash(q.x + NOS_PcgHash(q.y));     // h(x + h(y)).
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

// Curva quíntica de Perlin: s(t) = 6t⁵ − 15t⁴ + 10t³.
// Convierte la posición dentro de la celda (0 a 1) en un peso de mezcla que arranca
// y termina "plano". Probé el smoothstep cúbico (3t² − 2t³), pero ese deja quiebres
// en la segunda derivada y, si el ruido mueve normales o vértices, se ven las costuras
// de la cuadrícula en la luz. La quíntica las borra.
float2 NOS_Quintic(float2 t) { return t * t * t * (t * (t * 6.0 - 15.0) + 10.0); }
float3 NOS_Quintic(float3 t) { return t * t * t * (t * (t * 6.0 - 15.0) + 10.0); }

// Ruido de valor 2D en [0, 1].
// Le doy un valor aleatorio a cada esquina de la celda y mezclo las cuatro esquinas
// según dónde cae el punto, usando los pesos quínticos.
float NOS_ValueNoise2D(float2 p)
{
    float2 i = floor(p);                         // Esquina inferior izquierda de la celda.
    float2 f = frac(p);                          // Dónde estoy dentro de la celda, de 0 a 1.
    float a = NOS_Hash21(i);                     // Valor de la esquina (0, 0).
    float b = NOS_Hash21(i + float2(1.0, 0.0));  // Esquina (1, 0).
    float c = NOS_Hash21(i + float2(0.0, 1.0));  // Esquina (0, 1).
    float d = NOS_Hash21(i + float2(1.0, 1.0));  // Esquina (1, 1).
    float2 u = NOS_Quintic(f);                   // Pesos suavizados.
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y); // Mezcla en x abajo y arriba, luego mezcla esos dos en y.
}

// Ruido de valor 3D en [0, 1]: lo mismo, pero con las 8 esquinas de un cubo.
float NOS_ValueNoise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = NOS_Quintic(f);
    // Las cuatro esquinas de la cara de abajo (z = 0).
    float n000 = NOS_Hash31(i);
    float n100 = NOS_Hash31(i + float3(1, 0, 0));
    float n010 = NOS_Hash31(i + float3(0, 1, 0));
    float n110 = NOS_Hash31(i + float3(1, 1, 0));
    // Las cuatro de la cara de arriba (z = 1).
    float n001 = NOS_Hash31(i + float3(0, 0, 1));
    float n101 = NOS_Hash31(i + float3(1, 0, 1));
    float n011 = NOS_Hash31(i + float3(0, 1, 1));
    float n111 = NOS_Hash31(i + float3(1, 1, 1));
    float z0 = lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y); // Mezcla bilineal de la cara z = 0.
    float z1 = lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y); // Mezcla bilineal de la cara z = 1.
    return lerp(z0, z1, u.z);                                           // Y al final mezclo las dos caras en z.
}

// Ruido fractal (fBm) de 2 a 4 octavas: Σ ruido(p·2ⁱ)·0,5ⁱ, normalizado a [0, 1].
// Cada octava duplica la frecuencia (manchas más chicas) y reduce a la mitad su peso,
// así sumo detalle fino encima de las formas grandes.
// Paso el número de octavas como constante para que el compilador desenrolle el bucle.
// En móvil me quedo con 2 octavas porque cada una cuesta 4 hashes más.
float NOS_Fbm2D(float2 p, int octaves)
{
    float sum = 0.0;
    float amplitude = 0.5;
    float norm = 0.0;
    [unroll(4)]
    for (int i = 0; i < octaves; i++)
    {
        sum += NOS_ValueNoise2D(p) * amplitude; // Sumo esta octava con su peso.
        norm += amplitude;                      // Llevo la cuenta del peso total para normalizar al final.
        p *= 2.0;                               // Duplico la frecuencia (lacunaridad 2).
        amplitude *= 0.5;                       // Bajo el peso a la mitad (ganancia 0,5).
    }
    return sum / max(norm, 1e-4);               // Divido entre la suma de pesos para que el resultado quede en [0, 1].
}

// -----------------------------------------------------------------------------
// 3. PALETA COSENO
// -----------------------------------------------------------------------------

// color(t) = a + b·cos(2π(c·t + d))   (técnica de Íñigo Quílez).
//   a: brillo medio de cada canal.
//   b: cuánto se aleja del medio (contraste).
//   c: cuántas veces se repite el ciclo por cada unidad de t.
//   d: desfase de cada canal; si R, G y B tienen fases distintas, aparecen arcoíris.
// Con solo cuatro vectores obtengo un degradado continuo y periódico. La uso para la
// iridiscencia de Frutiger Aero y para variar tonos de celdas sin texturas.
half3 NOS_CosinePalette(float t, half3 a, half3 b, half3 c, half3 d)
{
    return a + b * cos(NOS_TWO_PI * (c * t + d));
}

// -----------------------------------------------------------------------------
// 4. FRESNEL
// -----------------------------------------------------------------------------

// Brillo de borde: F = (1 − saturate(N·V))^p.
// N·V es el coseno del ángulo entre la normal y la dirección hacia la cámara: vale 1
// cuando la superficie me mira de frente y 0 cuando la veo de canto. Al restarlo de 1
// el valor crece hacia la silueta. Con p alto (4 a 6) el brillo queda solo en el borde.
// N y V tienen que llegar normalizados (longitud 1), si no el coseno sale mal.
half NOS_Fresnel(half3 normalWS, half3 viewDirWS, half power)
{
    half nDotV = saturate(dot(normalWS, viewDirWS)); // Coseno entre normal y vista, recortado a [0, 1].
    return pow(1.0h - nDotV, power);                 // Crece hacia los bordes (cuando N ⟂ V).
}

// -----------------------------------------------------------------------------
// 5. DITHERING BAYER 4×4
// -----------------------------------------------------------------------------

// Umbral de la matriz de Bayer 4×4, normalizado a (0, 1):
//      0  8  2 10
//     12  4 14  6      umbral = (valor + 0,5) / 16
//      3 11  1  9
//     15  7 13  5
// Si comparo un alfa contra este umbral en cada píxel (`clip(alpha − umbral)`), obtengo
// 16 niveles de transparencia sin mezclar colores: el objeto se dibuja como opaco y
// escribe profundidad. Así me ahorro ordenar transparencias y el costo de la mezcla,
// que en GPUs móviles de tipo TBDR es caro.
float NOS_Bayer4x4(float2 pixelCoord)
{
    uint2 p = uint2(pixelCoord) & 3u;   // Posición del píxel dentro del patrón 4×4 (& 3 es lo mismo que módulo 4).
    // Guardé la matriz como cuatro números de 16 bits (cuatro valores de 4 bits por fila)
    // para no declarar un arreglo local, que en algunas GPUs se va a memoria lenta.
    uint row = p.y == 0u ? 0xA280u : (p.y == 1u ? 0x6E4Cu : (p.y == 2u ? 0x91B3u : 0x5D7Fu));
    uint value = (row >> (p.x * 4u)) & 0xFu; // Saco la columna x de esa fila.
    return (value + 0.5) / 16.0;
}

// -----------------------------------------------------------------------------
// 6. CUADRÍCULA Y PIXELADO
// -----------------------------------------------------------------------------

// Pixelado: redondeo la coordenada a una rejilla de `pixelsPerUnit` celdas por unidad.
// Es lo que da el aspecto de textura retro (en Primavera uso 32 px por metro).
// Devuelvo el centro de la celda (+0,5) y no la esquina para que no parpadee en los bordes.
float2 NOS_Pixelate(float2 uv, float pixelsPerUnit)
{
    return (floor(uv * pixelsPerUnit) + 0.5) / pixelsPerUnit;
}

// Líneas de una rejilla de periodo 1 con ancho `width` (fracción de celda), con antialiasing.
// La idea: mido qué tan lejos estoy de la línea más cercana y lo divido entre fwidth,
// que es cuánto cambia la coordenada de un píxel al de al lado. Así el borde de la línea
// siempre mide más o menos 1 píxel, esté cerca o lejos, y no aparece moiré a lo lejos.
float NOS_GridLines(float2 coord, float width)
{
    float2 derivative = max(fwidth(coord), 1e-5);              // Cuánto avanza la coordenada por píxel (evito dividir entre 0).
    float2 distanceToLine = abs(frac(coord - 0.5) - 0.5);      // Distancia a la línea entera más cercana, de 0 a 0,5.
    float2 lines = 1.0 - saturate((distanceToLine - width * 0.5) / derivative); // 1 sobre la línea, 0 fuera, con un borde de 1 px.
    // Cuando una celda ocupa menos de unos 2 píxeles en pantalla, las líneas ya no se
    // distinguen y solo producen ruido; ahí las apago poco a poco.
    float fade = saturate(1.0 - max(derivative.x, derivative.y) * 2.0);
    return max(lines.x, lines.y) * fade;                       // Uno líneas verticales y horizontales.
}

// Interpolación lineal inversa con saturación: me dice en qué fracción del tramo [a, b] cae x.
// Ejemplo: a = 0, b = 6, x = 3 → 0,5.
float NOS_InverseLerp01(float a, float b, float x)
{
    return saturate((x - a) / (b - a));
}

#endif // NOSTALGIA_COMMON_INCLUDED

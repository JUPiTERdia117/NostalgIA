// =============================================================================
// NostalgIA — Shader NOS/AeroWater
// Laguna turquesa de la Estación Frutiger Aero.
//
// Vértice: suma de tres ondas de Gerstner (crestas más agudas que una senoide):
//   para cada onda con dirección d, inclinación s y longitud λ:
//     k = 2π/λ,  c = √(g/k) (velocidad de fase en aguas profundas),  f = k·(d·p − c·t)
//     desplazamiento = (d.x·a·cos f, a·sin f, d.y·a·cos f), con a = s/k
//   las tangentes analíticas de cada onda se suman y la normal es cross(binormal, tangente).
// Fragmento:
//   - normal de detalle: gradiente por diferencias finitas de dos octavas de ruido que se
//     desplazan en sentidos distintos (ondulación fina sin texturas de normales)
//   - Fresnel de Schlick con F₀ = 0,02 (agua): F = F₀ + (1 − F₀)·(1 − N·V)⁵
//   - reflejo: el mismo gradiente de cielo que el skybox (NOS_SkyGradient)
//   - sol: destello Blinn-Phong muy concentrado + chispas (celdas de ruido que superan un
//     umbral y titilan)
//   - _NOS_REFRACTION (PC, con texturas de opacos y profundidad): se refracta la imagen de
//     la escena con un desplazamiento proporcional a la normal; el color se absorbe con la
//     profundidad del agua (Beer-Lambert: 1 − e^(−k·d)) y aparece espuma en la orilla
//     donde el fondo está a menos de _FoamDepth metros
//   - sin refracción (móvil): mezcla alfa con color por ángulo de vista
// Cola Transparent-50 y escritura de profundidad: se dibuja antes que las burbujas y las
// oculta correctamente cuando quedan bajo la superficie.
//
// Cómo lo pensé, en pocas palabras:
//   - Toda la forma de las olas sale del vértice; el píxel solo agrega detalle fino, brillo
//     y color. Así la malla del agua no necesita texturas de normales ni de espuma.
//   - La palabra clave _NOS_REFRACTION no la toco a mano: la enciende FrutigerAtmosphere
//     cuando el asset de URP activo genera la textura de opacos y la de profundidad (PC).
//     En móvil queda apagada y el shader usa la variante barata de mezcla alfa.
//   - El orden de dibujo importa: agua (2950) → espuma NOS/AeroFoam (2960) → burbujas
//     NOS/AeroBubble (3000). Como el agua escribe profundidad, lo que flota debajo de ella
//     queda oculto por la prueba de profundidad normal.
// =============================================================================

Shader "NOS/AeroWater"
{
    // Todo lo que está aquí aparece en el Inspector del material M_Frutiger_Water.
    // Ojo: FrutigerLevelBuilder sobrescribe _ShallowColor, _DeepColor y las tres ondas con
    // olas más bajitas (inclinaciones 0,045 / 0,035 / 0,025) para que el agua no tape la arena.
    Properties
    {
        _ShallowColor ("Agua somera", Color) = (0.24, 0.9, 0.82, 1)
        _DeepColor ("Agua profunda", Color) = (0, 0.42, 0.78, 1)
        _FoamColor ("Espuma", Color) = (1, 1, 1, 1)
        // Cada onda empaqueta 4 números en un Vector: xy = dirección (no hace falta que esté
        // normalizada, el shader la normaliza), z = inclinación s y w = longitud de onda λ en
        // metros. Si la suma de las tres inclinaciones pasa de 1, las crestas se cruzan sobre
        // sí mismas y salen "rizos" invertidos; con los valores por defecto la suma es 0,30.
        _WaveA ("Onda A (dir xy, inclinación, longitud)", Vector) = (1, 0.3, 0.12, 14)
        _WaveB ("Onda B", Vector) = (-0.4, 1, 0.1, 9)
        _WaveC ("Onda C", Vector) = (0.7, -0.6, 0.08, 5.5)
        _WaveSpeed ("Velocidad de las ondas", Float) = 0.6
        _DetailScale ("Escala de la ondulación fina", Float) = 0.35
        _DetailStrength ("Intensidad de la ondulación fina", Range(0, 1)) = 0.35
        _Absorption ("Absorción por metro", Range(0.01, 2)) = 0.35
        _RefractionStrength ("Refracción", Range(0, 0.1)) = 0.03
        _ReflectionStrength ("Reflejo", Range(0, 1.5)) = 1
        _SunSpecular ("Destello del sol", Range(0, 8)) = 3
        _Sparkle ("Chispas", Range(0, 4)) = 1.5
        _FoamDepth ("Profundidad de espuma (m)", Range(0.05, 3)) = 0.8
        _MobileAlpha ("Opacidad sin refracción", Range(0.3, 1)) = 0.78
    }

    SubShader
    {
        // Transparent-50 = cola 2950: se dibuja después de todos los opacos (así la textura de
        // opacos ya tiene el fondo de la laguna) y antes que el resto de transparentes.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode" = "UniversalForward" }
            // Mezcla alfa clásica: resultado = color·α + destino·(1 − α).
            // Con refracción el alfa es 1 (el "fondo" ya viene dentro del color refractado);
            // sin refracción el alfa baja a _MobileAlpha y la GPU mezcla con lo de atrás.
            Blend SrcAlpha OneMinusSrcAlpha
            // Escribo profundidad aunque sea transparente para que las burbujas y la espuma
            // que queden por debajo de la superficie se oculten bien.
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #pragma multi_compile_fog
            // multi_compile (no shader_feature) porque la palabra clave es global y cambia en
            // ejecución según la plataforma: necesito que las dos variantes existan en el build.
            #pragma multi_compile _ _NOS_REFRACTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Estas dos inclusiones declaran _CameraDepthTexture y _CameraOpaqueTexture con sus
            // funciones SampleSceneDepth y SampleSceneColor. Solo se leen dentro del #if de
            // refracción, así que en móvil no generan ninguna lectura de textura.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Assets/_Project/Art/Shaders/Frutiger/FrutigerCommon.hlsl"

            // Todas las propiedades del material viven en UnityPerMaterial: es la condición del
            // SRP Batcher para dejar los datos del material fijos en la GPU y solo cambiar los
            // del objeto entre llamadas de dibujo.
            // Usé half en colores y factores de 0 a algunas unidades (sobra precisión y en móvil
            // se calculan más rápido); las ondas, la escala y la velocidad van en float porque se
            // multiplican por posiciones de mundo y por el tiempo, y half se queda sin decimales.
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float4 _WaveA;
                float4 _WaveB;
                float4 _WaveC;
                float _WaveSpeed;
                float _DetailScale;
                half _DetailStrength;
                half _Absorption;
                half _RefractionStrength;
                half _ReflectionStrength;
                half _SunSpecular;
                half _Sparkle;
                half _FoamDepth;
                half _MobileAlpha;
            CBUFFER_END

            // Solo necesito la posición: la normal la calculo yo a partir de las ondas.
            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            // Una onda de Gerstner: devuelve el desplazamiento y acumula las tangentes.
            // A diferencia de subir y bajar los vértices con un seno, Gerstner también los mueve
            // en horizontal (hacia la cresta), así que se juntan arriba y se separan abajo:
            // crestas afiladas y valles anchos, como el mar de verdad.
            float3 GerstnerWave(float4 wave, float3 p, inout float3 tangent, inout float3 binormal)
            {
                float steepness = wave.z;
                // Número de onda k = 2π/λ: cuántos radianes avanza la fase por metro.
                // Con λ = 14 m, k ≈ 0,449 rad/m.
                float k = NOS_TWO_PI / wave.w;
                // Relación de dispersión de aguas profundas: c = √(g/k) con g = 9,8 m/s².
                // Las olas largas viajan más rápido que las cortas (λ = 14 m → c ≈ 4,67 m/s).
                float c = sqrt(9.8 / k);
                float2 d = normalize(wave.xy);
                // Fase f = k·(d·p − c·t): dot(d, p.xz) mide cuánto avancé en la dirección de la
                // onda; restar c·t hace que el patrón viaje en esa dirección. _WaveSpeed escala
                // el tiempo para calmar o agitar todo el mar a la vez.
                float f = k * (dot(d, p.xz) - c * _Time.y * _WaveSpeed);
                // Amplitud a = s/k: así la inclinación s (= k·a) controla la forma de la ola sin
                // importar su tamaño. Con s = 0,12 y λ = 14 m, a ≈ 0,27 m.
                float a = steepness / k;
                float sinF = sin(f);
                float cosF = cos(f);
                // Derivadas analíticas del desplazamiento respecto a x (tangente) y a z
                // (binormal). Las sumo onda por onda; al final su producto cruz da la normal
                // exacta sin tener que muestrear vértices vecinos.
                tangent += float3(-d.x * d.x * steepness * sinF, d.x * steepness * cosF, -d.x * d.y * steepness * sinF);
                binormal += float3(-d.x * d.y * steepness * sinF, d.y * steepness * cosF, -d.y * d.y * steepness * sinF);
                // Desplazamiento: horizontal a·cos f en la dirección d y vertical a·sin f.
                return float3(d.x * a * cosF, a * sinF, d.y * a * cosF);
            }

            Varyings WaterVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                // Trabajo en espacio de mundo para que las olas sigan alineadas aunque haya
                // varios planos de agua con escalas o rotaciones distintas.
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                // Arranco con las tangentes de un plano horizontal: eje x y eje z.
                float3 tangent = float3(1, 0, 0);
                float3 binormal = float3(0, 0, 1);
                float3 displaced = p;
                // Tres ondas con direcciones y longitudes distintas: al sumarlas el patrón
                // tarda mucho en repetirse y deja de parecer una "sábana" ondulada regular.
                displaced += GerstnerWave(_WaveA, p, tangent, binormal);
                displaced += GerstnerWave(_WaveB, p, tangent, binormal);
                displaced += GerstnerWave(_WaveC, p, tangent, binormal);
                output.positionWS = displaced;
                output.positionCS = TransformWorldToHClip(displaced);
                // El orden cross(binormal, tangente) importa: en el plano plano da (0, 1, 0),
                // normal hacia arriba. Al revés saldría hacia abajo y el agua se vería negra.
                output.normalWS = (half3)normalize(cross(binormal, tangent));
                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Altura de la ondulación fina: dos octavas de ruido que viajan en direcciones opuestas.
            // La segunda octava tiene 2,3 veces más frecuencia y la mitad de peso; el + 11 la
            // desfasa para que no comparta celdas con la primera. Como se mueven en sentidos
            // distintos, el detalle "hierve" en lugar de deslizarse como una textura.
            float DetailHeight(float2 p)
            {
                float t = _Time.y;
                return NOS_ValueNoise2D(p + float2(t * 0.21, t * 0.13)) +
                       0.5 * NOS_ValueNoise2D(p * 2.3 + float2(-t * 0.27, t * 0.19) + 11.0);
            }

            half4 WaterFragment(Varyings input) : SV_Target
            {
                float3 positionWS = input.positionWS;

                // Normal de detalle: gradiente (∂h/∂x, ∂h/∂z) por diferencias finitas, ε = 0,05.
                // Evalúo la altura en el píxel y un poquito a la derecha y al fondo; la
                // diferencia dividida entre ε es la pendiente. La normal de una superficie de
                // alturas es (−∂h/∂x, 1, −∂h/∂z). Cuesta 3 llamadas a DetailHeight (6 ruidos),
                // a cambio de no leer ninguna textura de normales.
                float2 dp = positionWS.xz * _DetailScale;
                const float eps = 0.05;
                float h0 = DetailHeight(dp);
                float hx = DetailHeight(dp + float2(eps, 0));
                float hz = DetailHeight(dp + float2(0, eps));
                float3 detail = float3(-(hx - h0) / eps, 1.0, -(hz - h0) / eps);
                // Solo sumo la inclinación horizontal (x, z) del detalle a la normal de las olas
                // grandes; _DetailStrength en 0 deja el agua lisa, en 1 muy picada.
                float3 N = normalize((float3)input.normalWS + float3(detail.x, 0, detail.z) * _DetailStrength);

                // V: del punto hacia la cámara. L: dirección hacia el sol (luz principal).
                float3 V = GetWorldSpaceNormalizeViewDir(positionWS);
                float3 L = _MainLightPosition.xyz;
                // dot(N, V) es el coseno del ángulo entre la normal y la mirada: 1 si veo el
                // agua desde arriba, cerca de 0 si la veo rasante hacia el horizonte.
                half NdotV = (half)saturate(dot(N, V));
                // Fresnel de Schlick con F₀ = 0,02 (el agua refleja 2 % de frente):
                //   N·V = 1   → 0,02 (casi transparente, veo el fondo)
                //   N·V = 0,5 → 0,05
                //   N·V = 0,2 → 0,34
                //   N·V = 0   → 1,00 (espejo en el horizonte)
                half fresnel = 0.02h + 0.98h * pow(1.0h - NdotV, 5.0h);

                // Reflejo: refleja la mirada en la normal y busca ese color en el mismo
                // gradiente de cielo que pinta el skybox, sin cubemap ni Reflection Probe.
                half3 reflection = NOS_SkyGradient((half3)reflect(-V, N), (half3)L) * _ReflectionStrength;
                // Blinn-Phong: H es el vector medio entre luz y vista. Con exponente 900 el
                // destello solo aparece a menos de ~2,5° de H (0,999^900 ≈ 0,41; 0,995^900 ≈ 0,01):
                // un punto de sol pequeño e intenso, que con HDR alimenta al Bloom.
                float3 H = normalize(L + V);
                half sun = (half)pow(saturate(dot(N, H)), 900.0) * _SunSpecular;
                // Chispas: celdas de 0,4 m cuyo hash supera 0,985 y titilan con fase propia.
                // floor(xz·2,5) parte el agua en celdas de 0,4 m; step(0,985, hash) enciende
                // ~1,5 % de ellas. Sumar floor(t·3) al hash las sortea de nuevo 3 veces por
                // segundo. El exponente 40 (mucho más ancho que el 900 del sol) deja que las
                // chispas aparezcan en un área más grande alrededor del reflejo.
                float2 cell = floor(positionWS.xz * 2.5);
                float glitter = step(0.985, NOS_Hash21(cell + floor(_Time.y * 3.0)));
                half sparkle = (half)(glitter * pow(saturate(dot(N, H)), 40.0)) * _Sparkle;

                half3 color;
                half alpha;
                half foam = 0.0h;
#if defined(_NOS_REFRACTION)
                // ---- Variante PC: refracción real con las texturas de la cámara ----
                // UV de pantalla: en el fragmento, SV_POSITION ya viene en píxeles (la GPU hizo
                // la división de perspectiva xy/w y el mapeo a la ventana). Esta función solo lo
                // divide entre el tamaño de la pantalla para dejarlo entre 0 y 1.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                // Paso las dos profundidades a metros desde la cámara (LinearEyeDepth deshace la
                // curva no lineal del búfer Z). La resta es cuánta agua hay debajo de este píxel
                // medida a lo largo de la mirada.
                float surfaceEye = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depthBelow = max(0.0, sceneEye - surfaceEye);

                // Refracción: desplazamiento según la normal, limitado en la orilla (saturate(d)).
                // N.xz es la inclinación de la superficie; con 0,03 el fondo se corre unos pocos
                // píxeles. saturate(depthBelow) hace que con menos de 1 m de agua el desplazamiento
                // baje a cero y la orilla no se "despegue" de la arena.
                float2 refractUV = screenUV + N.xz * _RefractionStrength * saturate(depthBelow);
                float refractEye = LinearEyeDepth(SampleSceneDepth(refractUV), _ZBufferParams);
                // Si el punto desplazado está delante del agua, no se refracta (evita "fantasmas").
                // (Pasa cuando el desplazamiento cae sobre una roca o isla que está fuera del agua:
                // sin esta prueba se vería un pedazo de esa roca "dentro" de la laguna.)
                if (refractEye < surfaceEye) { refractUV = screenUV; refractEye = sceneEye; }
                float waterDepth = max(0.0, refractEye - surfaceEye);

                half3 scene = SampleSceneColor(refractUV);
                // Absorción tipo Beer-Lambert: la luz que sobrevive tras d metros es e^(−k·d),
                // así que lo absorbido es 1 − e^(−k·d). Con k = 0,35:
                //   d = 1 m → 0,30   d = 2 m → 0,50   d = 5 m → 0,83   d = 10 m → 0,97
                // Uso un solo k para los tres canales (más barato y fácil de calibrar) y dejo
                // que el color profundo lo decida _DeepColor.
                half absorb = 1.0h - exp(-(half)waterDepth * _Absorption);
                // En lo bajito veo el fondo teñido de turquesa (× 1,15 para que no se oscurezca);
                // conforme crece la absorción el color tiende al azul profundo.
                half3 underwater = lerp(scene * _ShallowColor.rgb * 1.15h, _DeepColor.rgb, absorb);

                // Espuma: banda junto a la orilla, recortada por ruido animado.
                // shore vale 1 donde el fondo toca la superficie y baja linealmente a 0 cuando
                // hay _FoamDepth metros de agua (0,8 m → a 0,4 m vale 0,5).
                half shore = saturate(1.0h - (half)depthBelow / _FoamDepth);
                float foamNoise = NOS_ValueNoise2D(positionWS.xz * 1.7 + _Time.y * float2(0.3, -0.2));
                // Umbral con step: junto a la orilla (shore = 1) siempre hay espuma porque
                // ruido + 0,5 ≥ 0,45; más lejos solo pasan las manchas donde el ruido es alto,
                // y eso deshilacha el borde. Multiplicar por shore desvanece la banda hacia afuera.
                foam = shore * step(0.45, foamNoise + shore * 0.5);

                // Mezcla final por Fresnel: de frente domina el fondo refractado, rasante el cielo.
                color = lerp(underwater, reflection, fresnel);
                alpha = 1.0h;
#else
                // ---- Variante móvil: sin texturas de cámara ----
                // Sin profundidad no puedo saber cuánta agua hay, así que uso el ángulo como
                // aproximación: de frente se ve somera, rasante se ve profunda.
                half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, 1.0h - NdotV);
                color = lerp(body, reflection, fresnel);
                // De frente el agua deja ver lo de atrás (_MobileAlpha); en el horizonte es opaca
                // como un espejo, igual que en la realidad.
                alpha = lerp(_MobileAlpha, 1.0h, fresnel);
#endif
                // El sol y las chispas se suman al final con el color HDR del sol (global que
                // publica FrutigerAtmosphere) para que el Bloom los recoja.
                color += (sun + sparkle) * _NOS_SunTint.rgb;
                // En móvil foam se queda en 0 (no hay profundidad); ahí la espuma la pone
                // el anillo de NOS/AeroFoam.
                color = lerp(color, _FoamColor.rgb, foam);
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

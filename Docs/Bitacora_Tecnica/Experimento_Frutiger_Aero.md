# Experimento: nivel Frutiger Aero (rama `experimental/frutiger-aero`)

**Fecha:** 27 de octubre de 2026 · **Estado:** experimental, no integrado en `testing`
**Escena:** `Assets/_Project/Scenes/Stations/20_Verano_Frutiger.unity` (generada)
**Constructor:** *NostalgIA › Experimental › Construir nivel Frutiger Aero*

---

## 1. Objetivo

Explorar el lenguaje visual de la Estación 2 (Verano, Guía de estilo §5.2) con un nivel completo y jugable que use shaders propios en todas las superficies: cielo cian, laguna turquesa, plástico brillante, vidrio Aero, burbujas iridiscentes por todas partes, peces voladores, cintas de luz, haces de sol y arcoíris.

## 2. Recorrido

| Tramo | Contenido | Mecánica |
| :--- | :--- | :--- |
| Isla Inicio | Colina low poly con flores, árboles de copa brillante, cardumen | Checkpoint 0 |
| Plataformas | Seis discos de plástico blanco con canto cian sobre la laguna | Saltos de 2–3 m con desnivel ≤ 0,9 m (salto de 1,27 m con la física actual) |
| Isla Pabellón | Pabellón de vidrio Aero, seis orbes absorbibles (cian semántico) | Checkpoint 1, aspiradora |
| Trampolines | Dos burbujas trampolín con base amarilla | `BounceBubble`: v = √(2·g·h); 10 m y 11 m de altura con empuje de 6 m/s |
| Isla Flotante | Isla con base rocosa invertida a 16 m de altura | Checkpoint 2 |
| Vacío | Dos anclajes naranjas (capa `Grapple`) con discos de descanso | Gancho (19 m y 16,5 m entre puntos, alcance 30 m) |
| Isla Arcoíris | Arco de vidrio, estrella y arcoíris al fondo | Checkpoint 3 |

Caer al agua lleva al `KillVolume` bajo la superficie y reaparece en el último checkpoint.

## 3. Shaders nuevos (`Art/Shaders/Frutiger/`)

| Shader | Técnica principal |
| :--- | :--- |
| `NOS/AeroSky` | Gradiente cénit/horizonte, sol con halo, cúmulos con fBm proyectado en un plano e iluminación falsa por diferencia de densidad hacia el sol |
| `NOS/AeroGloss` | Plástico brillante: gradiente por altura, color de pendiente, flores (puntos redondos antialiasados), difusa envuelta, especular + capa "clearcoat", borde del cielo, sombreado plano opcional (normal por derivadas) y cáusticas bajo el agua |
| `NOS/AeroWater` | Tres ondas de Gerstner con normales analíticas, ondulación fina por diferencias finitas, Fresnel de Schlick, reflejo del cielo, chispas; en PC refracción, absorción de Beer-Lambert y espuma por profundidad |
| `NOS/AeroBubble` | Película delgada (interferencia a 650/530/450 nm), Fresnel, reflejos de ventana; deriva y ondulación en el vértice; modo impostor de 2 triángulos |
| `NOS/AeroBubbleSprite` | La misma burbuja reconstruida sobre partículas billboard |
| `NOS/AeroGlass` | Vidrio Aero: brillo superior, barrido diagonal, borde; en PC fondo refractado y esmerilado |
| `NOS/AeroRibbon` | Cintas de aurora aditivas con flujo y filamentos |
| `NOS/AeroGodRay` | Haces de sol en conos abiertos, con estrías y desvanecimiento al acercarse |
| `NOS/AeroRainbow` | Arcoíris procedural por radio con paleta coseno |
| `NOS/AeroFoam` | Espuma de orilla en corona, válida sin textura de profundidad (móvil) |
| `NOS/AeroFish` | Nado procedural en el vértice, degradado lomo/vientre, rotación de matiz por pez |

Todos declaran `UnityPerMaterial` (SRP Batcher). Las variables de ambiente (`_NOS_SkyZenith`, `_NOS_SunTint`, `_NOS_WaterLevel`…) son globales y las publica `FrutigerAtmosphere`, que además activa `_NOS_REFRACTION` solo si el asset de URP genera las texturas de opacos y profundidad (PC).

## 4. Rendimiento

| Decisión | Efecto |
| :--- | :--- |
| 420 burbujas cercanas con `Graphics.RenderMeshInstanced` en modo impostor | 1 llamada de dibujo y 840 triángulos (la esfera del motor costaría ~320 000) |
| Movimiento de burbujas y peces en el vértice | 0 trabajo de CPU por objeto; un `Transform` por cardumen |
| Dibujo instanciado en `beginCameraRendering` | Lo reciben la cámara de juego, la vista de escena y las capturas, sin `Update` |
| Agua de 160 × 160 celdas sin texturas | Un solo material; refracción y profundidad solo en PC |
| Materiales y mallas como assets | 0 materiales creados en ejecución |
| Presupuesto móvil | Burbujas × 0,45, partículas × 0,4, sin HDR, sin destello de lente (`Volume_Verano_PC`) |

Medición en el editor (Mac de desarrollo, ventana 681 × 383, inicio del nivel): 2,6 ms de CPU principal, 212 lotes, 60 *SetPass*, 177 k triángulos.

## 5. Corrección de la herramienta (incluida en esta rama)

- **Inflado.** El cuerpo `Cube` se desplazaba 8,5 unidades del espacio escalado del modelo (≈ 1,8 m) al llenarse: por eso el arma "se levantaba". Ahora tanques, anillos y cuerpo se inflan alrededor del centro de sus límites (p = p₀ + R·((s₀ − s)∘c)) en función de `almacenados / capacidad`, con un resorte subamortiguado que rebota al tragar y se contrae al disparar.
- **Garras.** En modo aspiradora el cabezal se desplazaba 7 m detrás de la cámara y las garras desaparecían. Ahora siempre se ven: cerradas sobre la boquilla en modo aspiradora, abiertas con las puntas curvadas en modo gancho y cerradas sobre el punto al anclar.

## 6. Pendiente

- Probar el recorrido completo con mando y táctil, y ajustar alturas de los trampolines tras las pruebas de juego.
- El nivel no tiene HUD propio (el HUD provisional vive en la escena del Tutorial).
- Decidir si el nivel se integra como Estación 2 o queda como referencia visual para la Fase 3.

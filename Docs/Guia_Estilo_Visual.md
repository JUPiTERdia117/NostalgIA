# Guía de Estilo Visual y Dirección de Arte Técnico — *NostalgIA*

**Proyecto:** *NostalgIA* — Fundamentos Técnicos de VFX  
**Tarea del roadmap:** F0-B04 (base de las Fases 2 y 3)  
**Versión:** 1.0 — 26 de octubre de 2026  
**Documentos relacionados:** `Docs/GDD.md` (§4.2 y §6), `Docs/TDD_Arquitectura.md` (§8), `Docs/Roadmap_Fases.md`

---

## 1. Propósito

Esta guía fija las decisiones visuales que deben respetar todos los activos, niveles, shaders, partículas, luces, perfiles de postprocesado e interfaz de *NostalgIA*. Su objetivo es que las cuatro Estaciones se perciban distintas entre sí y, al mismo tiempo, que el jugador lea siempre de la misma forma qué puede enganchar, qué puede absorber, qué lo mata y hacia dónde avanzar.

Los valores de color, luz y postprocesado son puntos de partida; se calibran en la Semana 3 sobre PC y móvil y cada cambio se registra en `Docs/Bitacora_Tecnica/`.

---

## 2. Principios de dirección de arte

1. **Collage Y2K intencional.** Se mezclan *low poly* con sombreado plano, modelos de mayor fidelidad, *sprites* 2D dentro del mundo 3D y texturas pixeladas (GDD §6.1). La mezcla es deliberada, nunca accidental: cada Estación declara qué capa de fidelidad domina.
2. **Jerarquía de fidelidad = jerarquía de presupuesto.** Los elementos focales (herramienta, enemigos, puntos de anclaje, meta) reciben los shaders y mallas más trabajados; el entorno general usa materiales compartidos, geometría simple y luz horneada.
3. **La jugabilidad se lee antes que la estética.** Los colores semánticos de la §3 son idénticos en las cuatro Estaciones y ningún elemento decorativo puede usarlos.
4. **Nada se comunica solo con color.** Toda señal de jugabilidad combina color con forma, iconografía, animación o sonido (accesibilidad para daltonismo, GDD §5.1).
5. **Todo es malla.** Colinas, suelos y relieves se modelan con ProBuilder o Blender. El sistema `Terrain` está prohibido y el validador del proyecto lo bloquea.

---

## 3. Lenguaje visual de jugabilidad (común a todas las Estaciones)

| Elemento | Color base | Valor hex | Forma y comportamiento obligatorios | Relación con el código |
| :--- | :--- | :--- | :--- | :--- |
| Superficie de anclaje del gancho | Naranja señal | `#FF8C1F` | Placa con borde emisivo y patrón de rejilla; pulso lento (0,5 Hz) | Mismo tono que el rayo del gancho en `GrapleController` (`RGB 1,0 · 0,55 · 0,12`) |
| Objeto absorbible | Cian datos | `#3FE0FF` | Contorno o franja emisiva; ligera flotación o giro | Capa `Absorbable` (7) |
| Peligro letal (lava electrónica) | Azul eléctrico | `#0640F2` con glifos `#EBF7FF` | Glifos de código desplazándose hacia abajo; borde oscuro `#020814` | Shader final `Tutorial/BlueCodeLava`; no se modifica |
| Enemigo | Rojo alerta | `#FF3B5C` | Ojos o núcleos emisivos; destello blanco al recibir daño | `EnemyHealth` usa `MaterialPropertyBlock` para el destello |
| Estrella coleccionable | Oro | `#FFD166` | Estrella de cinco puntas con giro y destello | Mismo oro que el HUD actual |
| Checkpoint | Verde confirmación | `#38E54D` | Columna de datos que se enciende al activarse | Mismo verde que la confirmación del HUD |
| Puerta de meta | Blanco emisivo + color de la Estación | `#FFFFFF` | La luz clave más intensa de la Estación apunta a ella | `GoalDoor` (Fase 2) |

Reglas:

- Ningún material de decoración puede usar estos siete tonos en superficies grandes.
- Si una Estación tiene un fondo de tono cercano a una señal (por ejemplo, el azul de Primavera con la lava), la señal conserva su contraste mediante borde oscuro y animación.

---

## 4. Métricas de diseño de niveles

Valores calculados a partir de la configuración actual de `Player.prefab` (26 de octubre de 2026). La construcción modular de las Estaciones debe respetarlos.

### 4.1. Datos del jugador

| Parámetro | Valor | Fuente |
| :--- | :-: | :--- |
| Altura de la cápsula | 1,8 m | `CharacterController.height` |
| Radio de la cápsula | 0,4 m | `CharacterController.radius` |
| Altura agachado | 1,0 m | `crouchingCharacterControllerHeight` |
| Altura de cámara (de pie / agachado) | 1,75 m / 1,0 m | `FirstPersonController` |
| Escalón máximo sin saltar | 0,3 m | `stepOffset` |
| Pendiente máxima caminable | 45° | `slopeLimit` |
| Velocidad caminando / corriendo / agachado | 5 / 8 / 1,5 m/s | `walkSpeed` / `sprintSpeed` / `crouchSpeed` |
| Velocidad vertical del salto | 5 m/s | `jumpSpeed` |
| Gravedad | 9,81 m/s² | `gravity` |
| Alcance del gancho / radio de llegada | 60 m / 1,5 m | `GrapleController` |
| Alcance de la aspiradora | 10 m | `ObjectVacum` |

### 4.2. Altura y alcance del salto

La altura máxima de un tiro vertical es:

```latex
h_{max} = \frac{v_0^2}{2g} = \frac{5^2}{2 \cdot 9.81} \approx 1.27\ \text{m}
```

El tiempo de vuelo sobre suelo plano es:

```latex
t_{vuelo} = \frac{2 v_0}{g} = \frac{2 \cdot 5}{9.81} \approx 1.02\ \text{s}
```

| Situación | Alcance horizontal en plano | Nota |
| :--- | :-: | :--- |
| Salto caminando | ≈ 5,1 m | 5 m/s × 1,02 s |
| Salto corriendo (prototipo actual) | ≈ 6,0 m | El control aéreo actual reduce la velocidad de 8 a 5 m/s a 5 m/s² |
| Salto corriendo (tras F1-A06) | ≈ 8,1 m | La Fase 1 conserva el impulso aéreo: 8 m/s × 1,02 s |

### 4.3. Medidas para el kit modular

| Elemento | Medida recomendada | Justificación |
| :--- | :--- | :--- |
| Rejilla del kit | 1 m (subdivisión 0,5 m) | Facilita el *snapping* y el horneado de lightmaps |
| Escalón decorativo | ≤ 0,3 m | Se sube sin saltar |
| Repisa alcanzable con salto | ≤ 1,1 m | Margen de 0,17 m bajo la altura máxima |
| Muro que bloquea el salto | ≥ 1,5 m | Supera la altura máxima con margen |
| Pasaje obligatorio de agachado | Altura libre de 1,2 a 1,4 m | Mayor que la cápsula agachada (1,0 m) y menor que la de pie (1,8 m) |
| Pasillo mínimo | 2 m de ancho | Cinco veces el radio de la cápsula; evita roces con la cámara |
| Hueco de salto básico | ≤ 4 m | Seguro caminando y corriendo, antes y después de la Fase 1 |
| Hueco de salto avanzado (con carrera) | 5 a 5,5 m | Exige correr; válido con ambos controles aéreos |
| Hueco que exige gancho | ≥ 9 m | Fuera del alcance de cualquier salto |
| Distancia a un punto de anclaje | 12 a 40 m | Dentro del alcance de 60 m y legible a distancia |
| Distancia a objetos absorbibles desde la ruta | ≤ 8 m | Dentro del alcance de 10 m con margen |

---

## 5. Dirección por Estación

### 5.1. Estación 1 — Primavera (Webcore / simulación retro)

| Aspecto | Dirección |
| :--- | :--- |
| Concepto | Interior cerrado que emula una simulación temprana de computadora; tutorial |
| Paleta | `#06102B` azul noche · `#0A2A9E` azul pantalla · `#3A6BFF` azul código · `#E8F4FF` blanco fósforo · `#8C8F96` gris columna |
| Fidelidad dominante | Bloques limpios *low poly* con texturas pixeladas (filtrado *Point*, 32 px por metro) |
| Luz clave | Cenital fría (≈ 7 500 K), intensidad media; la emisión de pantallas y de la lava es la fuente dominante |
| Ambiente | Color ambiental azul oscuro; sin niebla |
| Shaders | `NOS/WebcoreScreen` en techos y pisos de código; lava electrónica (final) |
| Partículas | Bits flotantes 0/1 y destellos de datos |
| Postprocesado | `VP_Primavera` (TDD §8.4): contraste alto y Bloom solo en emisivos |
| Referencias (GDD) | Pasillos con texturas de código azul de *ENA*; columnas clásicas grises |

### 5.2. Estación 2 — Verano (Frutiger Aero / collage Y2K)

| Aspecto | Dirección |
| :--- | :--- |
| Concepto | Exterior luminoso del "universo virtual"; contraste entre elementos realistas y colinas *low poly* |
| Paleta | `#2EC5FF` cian cielo · `#0077D9` azul profundo · `#7ED957` verde lima · `#3EE6D2` agua turquesa · `#F4FBFF` blanco nube · `#FFE27A` acento solar |
| Fidelidad dominante | Colinas y edificios *low poly* con sombreado plano; agua, burbujas y vegetación con más detalle |
| Luz clave | Direccional cálida (≈ 5 200 K), intensidad alta, sombras definidas; cielo cian saturado |
| Ambiente | Gradiente cielo-horizonte claro; reflejos de cielo en agua y burbujas |
| Shaders | `NOS/AeroBubble` (Fresnel, iridiscencia, ondulación de vértices) en burbujas, agua y cristal |
| Partículas | Burbujas iridiscentes, polen luminoso, peces voladores (*billboards*) |
| Postprocesado | `VP_Verano`: saturación alta, Bloom suave, *Lens Flare* del sol solo en PC |
| Referencias (GDD) | Frutiger Aero; colinas poligonales turquesa de *ENA*; obstáculos coloridos de *Fall Guys* |

### 5.3. Estación 3 — Otoño (Dreamcore / espacios liminales / Vaporwave)

| Aspecto | Dirección |
| :--- | :--- |
| Concepto | Escenarios solitarios y desorientadores; plataformas escasas y exigentes |
| Paleta | `#1D1B26` negro azulado · `#5E5670` gris violeta · `#B76E9B` magenta apagado · `#6FA8B5` cian apagado · `#E9DCC9` crema |
| Fidelidad dominante | Arquitectura liminal limpia; pisos de damero crema y negro; bustos clásicos con pantallas de TV |
| Luz clave | Baja intensidad, fuentes puntuales escasas y frías; la ruta se guía con luz cálida puntual |
| Ambiente | Niebla exponencial cuadrática violeta desaturada (`#8A7F9E`), que también limita la distancia de dibujo |
| Shaders | Damero procedural opcional; pantallas de los bustos con ruido de estática |
| Partículas | Ceniza y esporas suspendidas en la niebla |
| Postprocesado | `VP_Otono`: desaturación, *split toning* cian/magenta, viñeta marcada |
| Referencias (GDD) | Pasillos retorcidos, bustos grecorromanos con pantallas, paletas magenta/cian |

### 5.4. Estación 4 — Invierno (Motherboard corrupta / Glitch)

| Aspecto | Dirección |
| :--- | :--- |
| Concepto | Laberinto claustrofóbico dentro de una placa base gigante; núcleo de la IA |
| Paleta | `#050807` negro · `#0F5132` verde PCB · `#2BD96B` verde traza · `#C9803B` cobre · `#CFFFE0` blanco CRT · `#FF3B3B` alerta (reservado a la IA) |
| Fidelidad dominante | Paredes de circuito con trazas emisivas; torres de monitores CRT con ojos y alertas (`!`, `X`) |
| Luz clave | Emisión de monitores y trazas; contraluz verde; destellos rojos solo en ataques de la IA |
| Ambiente | Oscuro, sin niebla; contraste alto |
| Shaders | `NOS/GlitchCorruption` controlado por el nivel global de corrupción |
| Partículas | Chispas eléctricas y datos corruptos |
| Postprocesado | `VP_Invierno` + pulsos de aberración cromática y distorsión ligados a la IA |
| Referencias (GDD) | Placa base corrupta; colisión de elementos de las tres Estaciones previas |

---

## 6. Materiales, texturas y mallas

| Categoría | Presupuesto de triángulos (LOD0) | Textura máxima | Notas |
| :--- | :-: | :-: | :--- |
| Herramienta (primera persona) | Modelo final existente | 2048 px | Siempre en pantalla; recibe `NOS/VacuumInflate` en el depósito |
| Enemigos | ≤ 5 000 | 1024 px | LOD1 ≈ 40 % (TDD §8.5.2) |
| Bug (enemigo de enjambre) | ≤ 1 500 | 512 px | Muchas instancias simultáneas |
| Props absorbibles | ≤ 800 | 512 px | Material compartido por tipo |
| Piezas del kit modular | ≤ 2 000 | Atlas de 1024 px por Estación | `Batching Static`; un material por familia de piezas |
| *Billboards* y *sprites* | 2 triángulos | 256 a 512 px | Material sin iluminación; *alpha clip* en lugar de transparencia |

Reglas de materiales:

- Los materiales viven en `Assets/_Project/Art/Materials/<Estación>/` y se comparten por familia para aprovechar el SRP Batcher.
- Las texturas pixeladas usan filtrado *Point* y sin compresión con pérdida visible; el resto, compresión ASTC 6×6 en Android y BC7 en PC.
- El material gris por defecto de ProBuilder no puede quedar en ninguna escena de Estación (tarea F1-B02).

---

## 7. Interfaz de usuario

| Aspecto | Dirección |
| :--- | :--- |
| Estilo | HUD mínimo y limpio sobre el juego; menús con estética de sistema operativo de los 2000 (ventanas translúcidas, bordes brillantes de Frutiger Aero) |
| Disposición del HUD | Corazones abajo al centro, estrellas arriba a la derecha, pausa arriba a la izquierda, retícula al centro (GDD §5.1) |
| Retícula | Cuatro estados: neutra, absorbible (cian), anclaje válido (naranja), fuera de rango (gris con cruz) |
| Tipografía | Una familia de interfaz legible y una familia monoespaciada de estética de terminal; ambas con licencia libre (por ejemplo, SIL Open Font License) y registradas en `CREDITS.md` |
| Iconografía | Contorno de 2 px con relleno; forma reconocible sin color |
| Controles táctiles | Opacidad del 45 %, iconos universales, respeto del área segura de pantalla |
| Glifos de controles | Un juego por esquema: teclado y ratón, mando de Xbox y táctil |

---

## 8. Iluminación y postprocesado: reglas generales

- La luz clave de cada Estación es la única con sombras en tiempo real; el resto de luces decorativas se hornean.
- Cada Estación tiene un solo perfil de `Volume` global; las variaciones locales se resuelven con `Volume` locales de baja prioridad.
- El Bloom se apoya en materiales emisivos, nunca en exceso de exposición general.
- Ningún efecto de postprocesado puede ocultar las señales de la §3.

---

## 9. Lista de verificación de un activo visual

- [ ] Respeta la paleta y la fidelidad dominante de su Estación.
- [ ] No usa colores semánticos de jugabilidad en superficies decorativas.
- [ ] Está dentro del presupuesto de triángulos y textura.
- [ ] Usa un material compartido existente o justifica uno nuevo.
- [ ] Si es externo, está modificado de forma significativa y registrado en `CREDITS.md`.
- [ ] Se revisó en PC y en móvil con su perfil de postprocesado.

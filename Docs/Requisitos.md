# Documentación Oficial de Requerimientos y Rúbrica de Evaluación

**Asignatura:** Fundamentos Técnicos de VFX  
**Docente:** Ing. Carlos Aldair Roman Balbuena  
**Proyecto:** *NostalgIA* (Videojuego 3D en Primera Persona)  
**Fecha de entrega en Classroom:** 19 de noviembre de 2026  
**Fecha de presentación:** 24 de noviembre de 2026  

---

## 1. Objetivo y Alcance Académico

Aplicar y demostrar los conocimientos adquiridos durante el curso de **Fundamentos Técnicos de VFX** mediante el desarrollo de un prototipo escalable de videojuego 3D en primera persona (*NostalgIA*), evaluado en función de su creatividad, originalidad, calidad técnica y factibilidad.

---

## 2. Restricciones y Reglas Estrictas del Curso (Obligatorias en Desarrollo)

> [!CAUTION]
> **Reglas críticas de cumplimiento obligatorio en el repositorio y escenas de Unity:**
> 1. **Prohibición estricta del sistema de Terrenos de Unity:** Queda estrictamente prohibido utilizar el componente `Terrain` nativo de Unity en cualquier escena del proyecto. Todas las superficies, colinas (ej. Estación 2: Verano) y geometrías de nivel deben construirse mediante mallas 3D (modelos Low Poly / modulares).
> 2. **Modificación significativa de Assets externos:** No se permite el uso directo de assets descargados de repositorios externos sin que hayan sido modificados de manera significativa por el equipo (en geometría, texturizado, shaders personalizados o integración técnica) para adecuarlos a la identidad visual de *NostalgIA*. Además, todo contenido externo debe contar con su debida atribución de derechos de autor.
> 3. **Entorno estrictamente 3D / 2.5D:** El prototipo debe ser 3D (con posibilidad de integrar elementos 2D tipo *billboard* o *sprites* dentro del entorno 3D como recurso estético Webcore/Y2K, pero nunca ser un juego 2D).
> 4. **Audio obligatorio:** Todo el prototipo debe integrar audio (como mínimo audio ambiental y retroalimentación sonora de eventos).
> 5. **Congelación de mecánicas (Semana 3):** A partir de la tercera semana no se agregan nuevas mecánicas de jugabilidad; el trabajo se concentra exclusivamente en integración visual, VFX, shaders y optimización.
> 6. **Porcentaje mínimo de avance:** Se debe cumplir con al menos el **85% de avance general** del proyecto para realizar la entrega final.

---

## 3. Los 6 Requerimientos Técnicos de VFX y Arte Técnico

Aunque el curso exige un mínimo de 4 de los 6 requerimientos técnicos, **en este proyecto se decidió implementar y documentar los 6 pilares completos** para garantizar la máxima calificación técnica (120/120 pts en Desarrollo Técnico) y aprovechar la riqueza visual de las 4 Estaciones de *NostalgIA*:

### 3.1. Shaders Personalizados (Código HLSL / Shader Graph en URP)
* **Requisito:** Al menos un shader creado por el equipo con lógica matemática/visual avanzada (no se aceptan shaders básicos de color o textura simple).
* **Aplicación prevista en *NostalgIA*:**
  * *Shader Webcore / Matrix / Dithering (Estación 1 - Primavera):* Desplazamiento de coordenadas UV para flujos de código, pixelado dinámico en espacio de pantalla y tramado (*dithering*).
  * *Shader Acuático / Burbujas Iridescentes Frutiger Aero (Estación 2 - Verano):* Cálculo de efecto *Fresnel* mediante producto punto $\text{dot}(N, V)$, deformación de vértices (*Vertex Displacement*) con ondas sinusoidales y refracción cromática ligera compatible con móviles.
  * *Shader de Corrupción Digital / Glitch en Motherboard (Estación 4 - Invierno):* Desfase de vértices por bloques mediante funciones escalonadas (`step`, `frac`) y ruido procedural, controlado por parámetros desde C#.
  * *Shader de Inflado / Capacidad de la Aspiradora Lanzadora:* Deformación en el Vertex Shader a lo largo de las normales de la malla según el porcentaje de capacidad actual o mediante *BlendShapes* sincronizados con brillo emisivo progresivo.

### 3.2. Efectos Visuales (VFX y Sistemas de Partículas)
* **Requisito:** Sistemas de partículas integrados al prototipo que respondan dinámicamente a eventos, animaciones o interacciones del jugador.
* **Aplicación prevista en *NostalgIA*:**
  * *Vórtice de absorción de la Aspiradora Lanzadora:* Partículas helicoidales y líneas de flujo de aire que se activan al mantener presionado el botón de absorción y se dirigen hacia la boquilla del arma.
  * *Disparo e impacto de proyectiles:* Destello de salida (*muzzle flash* digital), estela (*trail*) del objeto lanzado y explosión de partículas al romper obstáculos o impactar enemigos (Correo de Spam, Bugs, Antivirus).
  * *Conexión y tracción del Gancho:* Chispa de anclaje al impactar una superficie válida, tensión visual en el ducto/cable y líneas de velocidad (*speed lines* perimetrales) durante el balanceo o deslizamiento rápido.
  * *Partículas ambientales por Estación:* Datos flotantes en Primavera, burbujas y polen luminoso en Verano, ceniza/esporas en la niebla liminal de Otoño y chispas eléctricas/datos corruptos en Invierno.

### 3.3. Iluminación Técnica
* **Requisito:** Uso consciente y justificado de iluminación estática (*Baked*), dinámica (*Realtime*) o mixta (*Mixed*) que aporte lectura visual (guiar al jugador hacia plataformas, puntos de anclaje y la puerta final) y atmósfera diferenciada.
* **Aplicación prevista en *NostalgIA*:**
  * Uso de iluminación **Mixta (Mixed Lighting)** y *Lightmapping* en objetos estáticos para proteger el presupuesto de rendimiento en dispositivos móviles, reservando luces direccionales/puntuales dinámicas para dar contraste a las plataformas clave y la retroalimentación de las herramientas.

### 3.4. Postprocesado Justificado (URP Volumes)
* **Requisito:** Implementación de efectos como *Bloom*, *Depth of Field*, *Color Grading*, *Vignette* o *Chromatic Aberration* con justificación técnica y artística (no simplemente activados por defecto).
* **Aplicación prevista en *NostalgIA*:**
  * Perfiles de volumen (*Volume Profiles*) específicos para cada una de las 4 Estaciones:
    * *Primavera (Webcore):* Alto contraste digital y ligero *Bloom* en textos de código.
    * *Verano (Frutiger Aero):* *Color Grading* saturado y brillante, *Bloom* suave en superficies de agua/cristal.
    * *Otoño (Dreamcore / Liminal):* Niebla densa (*Fog*), desaturación fría en sombras y *Vignette* sutil para acentuar la sensación de encierro y tensión.
    * *Invierno (Motherboard Corrupta):* *Chromatic Aberration* y distorsión de lente dinámica sincronizada con los ataques de la IA y el daño recibido por el jugador.

### 3.5. Optimización y Rendimiento (PC y Dispositivos Móviles)
* **Requisito:** Aplicación comprobable y documentada de técnicas de optimización (*LODs*, *Occlusion/Frustum Culling*, reducción de *Draw Calls*, optimización de shaders y partículas).
* **Aplicación prevista en *NostalgIA*:**
  * *SRP Batcher* activo en todos los shaders personalizados de URP.
  * Uso de `MaterialPropertyBlock` para variar propiedades de shaders por instancia sin romper el *batching* ni instanciar materiales en memoria.
  * *Object Pooling* para todos los proyectiles de la Aspiradora, enemigos (*Bugs* generados por el *Troyano*) y sistemas de partículas de impacto (0 asignaciones de *Garbage Collection* en tiempo de ejecución).
  * Configuración de *LOD Groups* en mallas complejas, compresión de texturas (ASTC en móvil / DXT en PC), límite estricto de partículas máximas y control de *Overdraw* en transparencias.

### 3.6. Integración de Sistemas
* **Requisito:** Sincronización limpia entre animaciones, VFX, shaders y lógica del juego mediante eventos, *Timeline* o scripts desacoplados.
* **Aplicación prevista en *NostalgIA*:**
  * Arquitectura basada en eventos de C# donde las acciones del controlador en primera persona (correr, deslizarse, absorber, inflar el depósito, disparar, engancharse, recibir daño o recolectar estrellas) disparan en tiempo real los controladores de Shaders, sistemas de partículas y audio.

---

## 4. Entregables Documentales Obligatorios del Curso

Para cumplir al 100% con los criterios de evaluación en Classroom, la carpeta `Docs/` del proyecto contendrá y mantendrá actualizados los siguientes documentos formales:

1. **Planeación e Ingeniería del Proyecto (`Docs/Planeacion_Costos_y_Gantt.md`):**
   * Objetivo, alcance y limitantes del proyecto.
   * Requerimientos técnicos de los dispositivos objetivo (PC y dispositivos móviles).
   * Metodología de desarrollo de software y análisis justificado de la tecnología (Unity 6.3.7f1 + URP).
   * Diagrama de Gantt con entregas semanales de avances.
   * Análisis de costos detallado, análisis de viabilidad técnica/económica y determinación del precio de venta.
2. **Game Design Document Completo (`Docs/GDD.md`):**
   * Incluye los 8 apartados reglamentarios exigidos para videojuegos: Concepto General, Mecánicas de Juego, Narrativa, Diseño de Niveles, Enemigos y NPCs, Sistema de Economía/Progresión, Interfaz y Experiencia de Usuario (UI/UX), y Estética/Dirección de Arte.
3. **Memorias Técnicas de VFX, Shaders y Optimización (`Docs/Reportes_PDF/`):**
   * Explicación matemática, algorítmica y de rendimiento de cada shader, sistema de partículas, perfil de iluminación/postprocesado y técnica de optimización implementada, redactada por el equipo para su entrega en PDF y defensa final.
4. **Manual de Usuario (`Docs/Manual_de_Usuario.md`):**
   * Guía de interacción dentro del escenario (controles en PC, Móvil y Mando Xbox), objetivos planteados por estación y resultados obtenidos.

---

## 5. Rúbrica Oficial de Evaluación (250 Puntos Totales)

### 1. Desarrollo Técnico (120 puntos)
| Criterio | Puntos |
| :--- | :---: |
| Implementación de requerimientos técnicos mínimos (10 pts c/u) | 40 pts |
| Calidad técnica de los shaders (estructura, parámetros, uso) | 20 pts |
| Calidad y uso de VFX (integración, control, intención visual) | 20 pts |
| Iluminación y postprocesado (uso técnico, coherencia visual) | 20 pts |
| Optimización y rendimiento (evidencia y aplicación) | 20 pts |
| **Subtotal Desarrollo Técnico** | **120 pts** |

### 2. Documentación y Entregas Semanales (40 puntos)
| Criterio | Puntos |
| :--- | :---: |
| Planeación del proyecto (objetivo, alcance, limitantes) | 10 pts |
| Documentación técnica (explicación de shaders, VFX, optimización) | 10 pts |
| Entregas semanales y avances documentados | 10 pts |
| Repositorio en GitHub (estructura, commits, claridad) | 10 pts |
| **Subtotal Documentación** | **40 pts** |

### 3. Creatividad y Originalidad (20 puntos)
| Criterio | Puntos |
| :--- | :---: |
| Propuesta visual y conceptual | 10 pts |
| Uso creativo de VFX, shaders e iluminación | 10 pts |
| **Subtotal Creatividad** | **20 pts** |

### 4. Análisis de Costos y Viabilidad (20 puntos)
| Criterio | Puntos |
| :--- | :---: |
| Análisis de costos actualizado | 10 pts |
| Viabilidad técnica y económica del proyecto | 10 pts |
| **Subtotal Costos y Viabilidad** | **20 pts** |

### 5. Presentación Final (50 puntos)
| Criterio | Puntos |
| :--- | :---: |
| Funcionamiento del prototipo | 15 pts |
| Explicación técnica del proyecto | 15 pts |
| Claridad visual y narrativa de la presentación | 10 pts |
| Respuesta a preguntas técnicas | 10 pts |
| **Subtotal Presentación Final** | **50 pts** |

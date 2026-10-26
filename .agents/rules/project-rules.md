---
trigger: always_on
description: Reglas técnicas y académicas de NostalgIA (motor, prohibiciones del curso, rendimiento, VFX y autoría). Sincronizadas con CLAUDE.md.
---

# NostalgIA — Reglas técnicas y académicas del proyecto

> **Sincronización obligatoria.** Este archivo y su par (`CLAUDE.md` en la raíz y `.agents/rules/project-rules.md`) contienen exactamente las mismas reglas. Todo cambio se aplica en ambos archivos en el mismo *commit*.
>
> **Fuentes de verdad:** `Docs/GDD.md` (diseño), `Docs/Requisitos.md` (rúbrica y restricciones), `Docs/TDD_Arquitectura.md` (arquitectura), `Docs/Roadmap_Fases.md` (fases y tareas). Ante un conflicto entre este archivo y esos documentos, prevalece `Docs/Requisitos.md` y se reporta la discrepancia.

---

## 1. Contexto

*NostalgIA* es un juego de plataformas 3D en primera persona para la asignatura **Fundamentos Técnicos de VFX**. El jugador controla un dron cámara con dos herramientas intercambiables: la **Aspiradora Lanzadora** (absorbe objetos, los almacena en cola FIFO y los dispara) y el **Gancho** (tracción con conservación de impulso). El juego tiene cuatro Estaciones: Primavera (Webcore), Verano (Frutiger Aero), Otoño (Dreamcore/Liminal) e Invierno (Motherboard corrupta).

Fechas fijas: congelación de mecánicas el **8 de noviembre de 2026**; entrega en Classroom el **19 de noviembre**; defensa el **24 de noviembre**. Desde el 9 de noviembre no se agregan mecánicas nuevas: solo integración visual, VFX, shaders, iluminación, postprocesado, audio, optimización y corrección de errores.

---

## 2. Especificaciones del motor

| Elemento | Valor |
| :--- | :--- |
| Motor | **Unity 6.3.7f1** (`6000.3.7f1`) |
| Pipeline | **Universal Render Pipeline (URP) 17.3** — `PC_RPAsset` (Forward+) y `Mobile_RPAsset` (Forward) |
| Entrada | **Input System 1.18** como único manejador (`activeInputHandler` = Input System). Prohibido `UnityEngine.Input` |
| Plataformas | **PC** (Windows/macOS) y **Móvil** (Android/iOS) |
| Esquemas de control | Teclado y ratón · **Mando de Xbox** (en PC y en móvil) · Táctil con pads dinámicos |
| Android | IL2CPP, ARM64, Vulkan + OpenGL ES 3, API mínima 29, orientación horizontal |
| Espacio de color | Lineal |
| Modelado de nivel | ProBuilder 6 y Blender; solo mallas |

Reglas de entrada:

- Todo el código lee **acciones lógicas** desde `InputReader`. Prohibido consultar dispositivos concretos (`Keyboard.current`, `Gamepad.current`, `Mouse.current`, `Touchscreen.current`) desde jugabilidad.
- Las acciones y sus *bindings* siguen la tabla de controles del GDD (§2.2 y §2.3) y del TDD (§7.1).
- Ninguna función puede funcionar solo con un esquema: toda acción se prueba con teclado y ratón, mando de Xbox y táctil.

---

## 3. Prohibiciones estrictas del curso

1. **Cero uso de `Unity Terrain`.** Prohibidos los componentes `Terrain` y `TerrainCollider` y los assets `TerrainData` en cualquier escena o prefab. Toda superficie, colina o topografía se construye con **mallas 3D** (ProBuilder o Blender). El validador del proyecto bloquea el build si detecta uno.
2. **Assets externos modificados y atribuidos.** Ningún asset descargado se usa tal cual: debe modificarse de forma significativa (geometría, texturizado, shader propio o integración técnica) para la identidad visual de *NostalgIA*. Cada asset externo se registra en `CREDITS.md` con autor, fuente, licencia y modificación realizada, en el mismo *commit* en que se agrega.
3. **Audio obligatorio.** Toda Estación tiene audio ambiental y todo evento de jugabilidad relevante tiene retroalimentación sonora, enrutada por `AudioEventRouter` y el `AudioMixer`.
4. **Juego estrictamente 3D.** Se permiten elementos 2D como *billboards* o *sprites* dentro del mundo 3D; nunca jugabilidad 2D.
5. **Congelación de mecánicas** a partir de la Semana 3 (9 de noviembre).

---

## 4. Arquitectura obligatoria (resumen de `Docs/TDD_Arquitectura.md`)

- **Separación simulación / presentación.** El ensamblado `NostalgIA.Gameplay` nunca referencia a `NostalgIA.Presentation`. La jugabilidad publica eventos; VFX, audio, postprocesado, HUD y Animator los escuchan.
- **Bus de eventos:** `EventBus<T>` con eventos `struct` de solo lectura. Suscripción en `OnEnable`, cancelación en `OnDisable`, sin excepción.
- **Ensamblados:** `NostalgIA.Core`, `NostalgIA.Input`, `NostalgIA.Gameplay`, `NostalgIA.Presentation`, `NostalgIA.Editor`.
- **Espacios de nombres:** raíz `NostalgIA` (por ejemplo, `NostalgIA.Gameplay.Tools`).
- **Datos de diseño en `ScriptableObject`** (`PlayerSettings`, `VacuumStats`, `GrappleStats`, `EnemyData`, `StationData`, `AtmosphereProfile`, `QualityProfile`, `PoolDefinition`, `SfxCue`). Los `ScriptableObject` son inmutables en tiempo de ejecución; las mejoras se aplican sobre copias en clases C#.
- **Código nuevo** en `Assets/_Project/` según la estructura del TDD §14; terceros en `Assets/ThirdParty/`.
- **Capas físicas** según el TDD §13 (`Player`, `Absorbable`, `Grapple`, `Enemy`, `Hazard`, `PlayerProjectile`, `EnemyProjectile`, `Debris`, `Trigger`). Capas, etiquetas, identificadores de shader y de Animator en constantes (`Layers`, `Tags`, `ShaderIds`, `AnimatorIds`).
- **Prohibido:** búsquedas por nombre (`GameObject.Find`), `FindObjectOfType`/`FindAnyObjectByType` y `FindGameObjectWithTag` fuera de inicialización, coordenadas de nivel escritas en código, recargar la escena para reaparecer, y lógica de nivel en scripts genéricos.

---

## 5. Estándares de rendimiento y VFX

### 5.1. Memoria y CPU

- **Cero asignaciones de memoria (`GC Alloc`) en `Update`, `FixedUpdate` y `LateUpdate`** (y en cualquier código que se ejecute cada frame o cada paso de física). Verificado en el Profiler: 0 B por frame en juego.
- En consecuencia, en el bucle de juego está prohibido: `new` de clases o arreglos, LINQ, concatenación o interpolación de cadenas, `foreach` sobre colecciones que asignan, *boxing*, `GetComponent` sin caché, `Physics.RaycastAll`/`OverlapSphere`/`SphereCastAll` (usar las variantes `NonAlloc` con búferes preasignados), corrutinas creadas por frame y `Debug.Log` fuera de `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
- Textos de HUD con `TMP_Text.SetText("{0}/{1}", a, b)` y solo al recibir el evento que los cambia.

### 5.2. Object Pooling obligatorio

- **Todo proyectil, sistema de partículas de eventos, enemigo generado (en particular los *Bugs* del Troyano), fragmento visual, texto flotante y `AudioSource` dinámico se obtiene del `PoolService`.**
- Prohibido `Instantiate` y `Destroy` durante el juego para esos objetos; los *pools* se precalientan durante la pantalla de carga.
- Los objetos reutilizables implementan `IPoolable` (`OnSpawned` / `OnDespawned`) y restablecen todo su estado al salir del *pool*.
- Prohibido `GameObject.CreatePrimitive` en tiempo de ejecución; los fragmentos se resuelven con partículas de malla.

### 5.3. Shaders

- **Todo shader propio es compatible con el SRP Batcher:** todas las propiedades del material dentro de `CBUFFER_START(UnityPerMaterial)` / `CBUFFER_END`, texturas y *samplers* fuera, misma disposición en todos los pases.
- Pases: `UniversalForward`; `ShadowCaster` si proyecta sombra; `DepthOnly` y `DepthNormals` en opacos.
- Variantes: `shader_feature_local` para opciones de material; la única palabra clave global de plataforma es `_NOS_MOBILE`.
- Biblioteca común en `NostalgIA_Common.hlsl`; `half` para color y factores normalizados, `float` para posiciones y tiempo.
- Todo shader tiene su variante o configuración móvil definida en el TDD §8.1.3.

### 5.4. Materiales en tiempo de ejecución

- **Prohibido instanciar materiales en tiempo de ejecución:** ni `new Material(...)` ni el acceso a `renderer.material` / `renderer.materials` (que duplica el material). Los materiales son assets en `Assets/_Project/Art/Materials/`.
- **Para variar propiedades por objeto se usa `MaterialPropertyBlock`** con identificadores cacheados (`Shader.PropertyToID`).
- Nota técnica obligatoria: un `Renderer` con `MaterialPropertyBlock` queda fuera del SRP Batcher. Por eso el MPB se reserva para pocos objetos dinámicos y relevantes (inflado de la herramienta, destello y disolución de enemigos, *glitch* por objeto). Los efectos que afectan a toda una Estación usan **propiedades globales** (`Shader.SetGlobalFloat`), y la geometría de nivel usa materiales compartidos.

### 5.5. Presupuestos (TDD §8.5.4)

| Métrica | PC mínimo (GTX 1050 Ti, 1080p) | Móvil mínimo (Adreno 618 / Mali-G76) |
| :--- | :-: | :-: |
| FPS | ≥ 60 | ≥ 30 |
| *SetPass calls* | ≤ 300 | ≤ 120 |
| *Batches* | ≤ 1 500 | ≤ 250 |
| Partículas simultáneas | ≤ 4 000 | ≤ 1 000 |
| GC por frame | 0 B | 0 B |

Iluminación mixta con geometría estática horneada; luces decorativas en `Baked`; en `Realtime` solo las de retroalimentación del jugador. Postprocesado mediante un perfil de `URP Volume` por Estación más el Volume de retroalimentación.

---

## 6. Autoría y documentación académica

### 6.1. Prohibición de menciones a IA

**Prohibido mencionar a Claude, Gemini, Argon, ChatGPT, Copilot o cualquier IA o herramienta de generación** en:

- comentarios de código y de shaders;
- nombres de clases, métodos, variables, archivos, assets, escenas o ramas;
- documentos del proyecto (`Docs/`, `CREDITS.md`, manuales, memorias PDF);
- mensajes de *commit*, descripciones de *pull request* y etiquetas.

No se agregan líneas de coautoría, firmas ni enlaces de sesión de ninguna herramienta en los *commits* ni en los *pull requests*. Todo el trabajo se documenta como elaborado por el equipo.

### 6.2. Comentarios obligatorios en C#

Todo script C# se comenta **exhaustivamente en español**, con tono profesional de ingeniería y en **primera persona del plural o forma impersonal** (*"En esta sección se decidió…"*, *"Se utiliza una cola para garantizar el orden FIFO…"*):

- Cada clase, interfaz, `struct`, método y propiedad pública lleva comentario XML (`/// <summary>`, `<param>`, `<returns>`).
- Cada campo serializado lleva `[Tooltip]` en español con unidades (m, s, m/s, N·s).
- Cada fórmula física o matemática se explica con su ecuación y el significado de cada término (por ejemplo, *F = m·a*, conservación de impulso, interpolaciones).
- Cada decisión de optimización se justifica (por qué `NonAlloc`, por qué *pooling*, por qué caché).
- Los bloques no triviales llevan un comentario que explica el **porqué**, no solo el qué.

```csharp
/// <summary>
/// Almacén de la Aspiradora Lanzadora. Se utiliza una cola para garantizar el orden FIFO
/// exigido por el GDD (§2.3 A): el primer objeto absorbido es el primero en dispararse.
/// </summary>
/// <remarks>
/// En esta sección se decidió preasignar la capacidad de la cola para que almacenar y
/// disparar no generen asignaciones de memoria durante el juego.
/// </remarks>
public sealed class VacuumStorage { /* ... */ }
```

### 6.3. Comentarios obligatorios en shaders (`.shader` / `.hlsl`)

Todo shader propio se comenta en español con tono de Technical Art y sirve como evidencia para la memoria técnica de VFX:

- **Cabecera** con propósito, Estación, técnica, lista de propiedades con su rango y significado, pases incluidos y costo estimado en PC y móvil.
- **Cada fórmula** explicada en el punto donde se aplica, con su ecuación (*"Se aplica el producto punto entre la normal N y la dirección de vista V para calcular el término de Fresnel: F = (1 − saturate(N·V))^p"*).
- **Cada decisión de rendimiento** justificada (precisión `half`, ausencia de ramas dinámicas, compatibilidad con SRP Batcher, alternativa móvil).

```hlsl
// -----------------------------------------------------------------------------
// NOS/AeroBubble — Estación 2 (Verano), estética Frutiger Aero.
// Se combina un término de Fresnel con una paleta coseno para producir la
// iridiscencia, y se desplazan los vértices con una onda senoidal.
// Costo estimado: bajo en PC; en móvil se desactiva la refracción (_NOS_MOBILE).
// -----------------------------------------------------------------------------

// Se aplica el producto punto entre la normal y la dirección de vista para
// calcular el efecto Fresnel: F = (1 - saturate(dot(N, V)))^_FresnelPower.
// El término crece hacia los bordes, donde una burbuja real refleja más luz.
half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
```

### 6.4. Documentación

- Toda decisión técnica relevante se registra en `Docs/Bitacora_Tecnica/` con fecha, identificador de tarea del roadmap y evidencia (captura del Profiler, Frame Debugger o comparativa).
- Los documentos se redactan con tono formal de ingeniería, en primera persona del plural o forma impersonal.

---

## 7. Activos finales (no modificar)

Se consideran finales; solo se les agregan animaciones o integración:

- Modelo de la herramienta `robot.fbx`.
- Modelos de enemigos: Antivirus `winrarModel.fbx`, Correo de Spam `spamMail.fbx` y Troyano `troyano.fbx`.
- Shader de lava electrónica `Tutorial/BlueCodeLava`.

---

## 8. Flujo de trabajo y control de versiones

- Ramas `feature/<area>-<tema>` integradas en `testing` por *pull request*; `main` recibe solo las versiones etiquetadas de entrega.
- Mensajes de *commit* en español con tipo e identificador de tarea del roadmap: `feat(F1-A09): almacenamiento FIFO y detección en cono de la aspiradora`. Sin menciones a IA ni líneas de coautoría (§6.1).
- No versionar `Library/`, `Temp/`, `Logs/`, `Builds/`, `obj/`, `UserSettings/`, archivos `.apk`/`.aab`, `.DS_Store`, `.idea/` ni `.vscode/`.
- Una persona edita una escena a la vez; preferir cambios en prefabs.
- Antes de cada build se ejecuta el validador del proyecto (`NostalgIA/Validar proyecto`).

---

## 9. Lista de verificación antes de entregar un cambio

- [ ] Funciona con teclado y ratón, mando de Xbox y táctil.
- [ ] 0 B de GC por frame en el Profiler para el código del bucle de juego tocado.
- [ ] Proyectiles, partículas y *Bugs* salen del `PoolService`.
- [ ] Shaders nuevos o modificados compatibles con SRP Batcher.
- [ ] Ningún `new Material` ni acceso a `renderer.material` en tiempo de ejecución.
- [ ] Ningún `Terrain`; assets externos registrados en `CREDITS.md`.
- [ ] El evento nuevo tiene respuesta de audio cuando corresponde.
- [ ] Comentarios completos en español (XML en C#, cabecera y fórmulas en shaders).
- [ ] Ninguna mención a IA en código, nombres, documentos o *commit*.
- [ ] Después del 8 de noviembre: el cambio no agrega mecánicas.

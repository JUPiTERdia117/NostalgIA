# Bitácora Técnica — Semana 1 (26 de octubre – 1 de noviembre de 2026)

**Proyecto:** *NostalgIA* — Fundamentos Técnicos de VFX  
**Fases del roadmap:** Fase 0 (26–27 de octubre) y Fase 1 (28 de octubre – 1 de noviembre)  
**Entrega semanal 1:** documentación base aprobada y prototipo de movimiento y herramientas jugable con teclado y ratón, mando de Xbox y pantalla táctil (`Docs/Roadmap_Fases.md` §4).

---

## 1. Objetivos de la semana

| Objetivo | Estado al 26 de octubre |
| :--- | :--- |
| Repositorio, estructura de carpetas, ensamblados y validación (Fase 0, Pista A) | Completado |
| Limpieza de contenido externo, créditos, manual y guía de estilo (Fase 0, Pista B) | Completado |
| Entrada unificada, bus de eventos, motor del jugador, herramientas, vida y reaparición (Fase 1, Pista A) | Completado el 26 de octubre: compila sin errores, montaje aplicado y 66 pruebas aprobadas |

---

## 2. Registro de decisiones y trabajo realizado

| Fecha | Tarea | Decisión o trabajo | Evidencia |
| :--- | :--- | :--- | :--- |
| 26 oct | F0-A08 | Se etiquetó el prototipo previo como `v0.1-prototipo` antes de cualquier cambio. | Etiqueta git sobre `e1ba801` |
| 26 oct | F0-A01 | Se versionaron `Docs/` y los archivos de reglas técnicas y académicas del proyecto; se retiraron del índice los archivos de sistema operativo y de IDE. | Commit `d2e5564` |
| 26 oct | F0-A02 | Se reorganizaron 355 archivos en `Assets/_Project/` y `Assets/ThirdParty/`, moviendo cada activo con su `.meta`. Se comprobó que la escena del Tutorial, el prefab del jugador y los assets de URP quedaron idénticos byte a byte. | Commit `9afe174`; comparación de *hashes* de git |
| 26 oct | F0-A03 | Se crearon los ensamblados `NostalgIA.Core`, `.Input`, `.Gameplay`, `.Presentation` y `.Editor`. `Gameplay` no referencia a `Presentation` (principio P1 del TDD). | Commit `c1ad60f`; compilación exitosa de las cinco DLL en el editor (26 oct, 03:04) |
| 26 oct | F0-A04 | Se implementó el validador del proyecto y su ejecución obligatoria antes de cada build. | Commit `c08b9ba` |
| 26 oct | F0-A05 a A07 | Escena `00_Boot` como primera del build; Player Settings (NostalgIA, API 29, solo horizontal); capas `Absorbable` y `Grapple` con los mismos índices y seis capas nuevas; matriz de colisiones del TDD. | Commit `04b56ca` |
| 26 oct | F0-B01 | Se retiraron la plantilla de URP, el skybox externo no modificado y el contenido de demostración del controlador externo. Se conservó el material del jugador como `Player_Placeholder.mat` con su GUID. | Commit `3d108b6` |
| 26 oct | QA | Se eliminaron tres instancias huérfanas del Correo de Spam en el Tutorial y dos en la escena de prueba `EnemyTest`. Apuntaban al GUID anterior de `FlyingEmail.prefab`, que cambió al regenerarse su `.meta` en el commit `32fce7d`. Eran duplicados de una versión previa; los seis Correos vigentes del Tutorial no cambian. | Commits `007247e` y `3c1ca82` |
| 26 oct | QA | Se agregaron pruebas EditMode y PlayMode de la Fase 0 y se mejoró el validador (lectura única de escenas y verificación robusta de GUID). | Commit `6b16aa6` |
| 26 oct | F0-B02 a B04 | `CREDITS.md`, esqueleto de `Docs/Manual_de_Usuario.md`, esta bitácora y `Docs/Guia_Estilo_Visual.md`. | Commit de documentación de la Fase 0 |
| 26 oct | F1-A01 | `EventBus<T>` estático por tipo, con eventos `readonly struct` y oyentes `Handler(in T)`: publicar no asigna memoria. Catálogo completo del TDD §4.2 en `GameEvents.cs`, más dos eventos de interfaz (`HudMessageRequested` y `DimensionalPortalCrossed`). `ServiceLocator` con una ranura genérica estática por contrato, sin diccionarios. | `Scripts/Core/Events`, `Scripts/Core/Services` |
| 26 oct | F1-A02 | Asset de acciones con los mapas `Gameplay` y `UI` y tres esquemas. Los controles de herramienta siguen el GDD: clic derecho absorbe o engancha, clic izquierdo dispara, `F` o `Y` cambian de herramienta y `L3` corre. **Decisión:** no se genera la clase C# del asset, porque el asset funciona como acciones de todo el proyecto y la clase crearía una segunda copia en memoria; `InputReader` cachea las referencias una sola vez (TDD §7.1 actualizado). | `Input/InputSystem_Actions.inputactions` |
| 26 oct | F1-A03 a A05 | `InputReader` (SO) suma las tres fuentes de mirada (ratón, arrastre táctil normalizado por la altura de pantalla y stick con la rampa del prototipo). `InputSchemeResolver` distingue el mando virtual de los controles en pantalla (no nativo) del mando físico. Controles táctiles: pad dinámico, zona de mirada, pad de herramienta contextual, botones `OnScreenButton` y `SafeAreaFitter`. | `Scripts/Input` |
| 26 oct | F1-A06 | `PlayerMotor` reutiliza el GUID del controlador derivado, de modo que el prefab conserva su referencia. Estados en un enumerado: las transiciones no crean objetos. Conservación de impulso aéreo: el control solo acelera hasta `maxAirSpeed` y un arrastre bajo actúa sobre el exceso. Deslizamiento desde `Crouch` al correr en los tres esquemas; conserva la rapidez de carrera si supera la del deslizamiento. Gancho integrado como estado `Grappling` con subpasos a 90 Hz. Nado y cornisa retirados. Se añadieron un búfer de salto de 0,12 s y un *coyote time* de 0,1 s. FOV, balanceo, inclinación y sacudida pasaron a `PlayerCameraFeedback` (Presentation). | `Scripts/Gameplay/Player/PlayerMotor.cs` |
| 26 oct | F1-A07 a A10 | `AimService`: un único `Physics.Raycast` por frame compartido por herramientas y retícula, con 0 llamadas a `RaycastAll`. `ToolController` resuelve las acciones según la herramienta activa y administra el enfriamiento compartido; el enfriamiento empieza al soltar un enganche exitoso, como en el prototipo. `VacuumTool`: cono con `OverlapSphereNonAlloc` + ángulo + línea de visión, hasta 3 absorciones simultáneas, almacén **FIFO** (el prototipo usaba una pila) y disparo con verificación de salida desde la cámara. `GrappleTool`: misma física del prototipo, parametrizada en `GrappleStats`, y **solo engancha la capa `Grapple`** (corrige la máscara 507). | `Scripts/Gameplay/Aim`, `Scripts/Gameplay/Tools` |
| 26 oct | F1-A11 a A13 | `PlayerHealth` (invulnerabilidad, *instakill*, caída letal a ≥ 24 m/s); `PlayerContactDamage` con caché por collider; `KillVolume` sustituye el umbral global y = −15. `RespawnService` reubica en el último checkpoint en 0,8 s sin recargar la escena y reinicia los `IResettable`. Los absorbibles rotos vuelven a su lugar; los enemigos eliminados permanecen así hasta `EnemyBase` (Fase 2). `PoolService` sobre `ObjectPool<T>`: los fragmentos de rotura salen de un pool en lugar de `CreatePrimitive` + `Destroy`. | `Scripts/Gameplay/Player`, `Scripts/Core/Pooling` |
| 26 oct | F1-A14 y A15 | `GameStateMachine` (escala de tiempo, cursor y mapa de entrada por estado). `GameRoot` vive en `Prefabs/Core/Resources/` y se instancia antes de la primera escena (TDD §3.1 actualizado). SO con los valores del prototipo. La capacidad de la aspiradora (20) y el alcance del gancho (30 m) toman los valores con que se jugaba el Tutorial, que sobrescribía los del prefab (5 y 60 m). | `Data/`, `Prefabs/Core/Resources/GameRoot.prefab` |
| 26 oct | F1-A16 | Se retiró el controlador monolítico. Su parte visual pasó a `TutorialHudPresenter`, que reutiliza su GUID y conserva las referencias del HUD y solo usa eventos. La reaparición pasó a `RespawnService`. La tabla de coordenadas de checkpoints, la reubicación de estrellas, las islas estáticas y la disposición del HUD se aplican una vez en el editor con *NostalgIA › Fase 1 › Montar todo*. Se retiraron de la escena `TutorialZonePrompt` (5) y `ThrowableDamage` (19, con valores idénticos a los de `Absorbable`). Los rayos finos de puntería (`ToolRayVisual`) se reemplazaron por la retícula de cuatro estados. | `Scripts/Editor/Setup/Phase1Builder.cs` |
| 26 oct | QA-F1 | Verificación en el editor: los siete ensamblados compilan sin errores (queda una advertencia heredada de `NonEuclideanPortal` por una API obsoleta de URP). *Montar todo* terminó con 0 errores de validación y 1 advertencia (material por defecto de ProBuilder, F1-B02). Test Runner: 61/61 pruebas EditMode y 5/5 PlayMode aprobadas. Prueba manual en modo de juego desde `00_Boot`: carga el Tutorial con el HUD por eventos (vida, estrellas, herramienta y guía 1/7), caminar, saltar y cambiar de herramienta con `F`; el cabeceo de la mirada se aplica al pivote de cámara. | Reporte `Reporte_Validacion_Proyecto.md`; commit del montaje |
| 26 oct | F1-B05 | `NostalgIA_Common.hlsl`: *hash* PCG (1D, 2D y 3D), ruido de valor 2D/3D con interpolación quíntica, fBm de hasta 4 octavas, paleta coseno, Fresnel, matriz de Bayer 4×4 codificada en cuatro constantes de 16 bits, pixelado y rejilla con antialiasing analítico (`fwidth`). No declara propiedades de material: cada shader conserva su propio `UnityPerMaterial`. | `Art/Shaders/Include/` |
| 26 oct | F1-B02 | Shader `NOS/StationKit` (Blinn-Phong de URP con sombras, Forward+ y niebla): proyección triplanar dura en espacio de mundo, baldosas con tono por celda, muros con gradiente, segmentos de "código" y banda de escaneo, rejilla emisiva. Sin texturas: cero muestreos por fragmento. Los patrones de alta frecuencia (grano y caracteres) se apagan cuando una celda mide menos de dos píxeles, lo que elimina el parpadeo en muros lejanos detectado en la revisión visual. Materiales `M_Primavera_Kit` y `M_Verano_Kit`. En el Tutorial se sustituyeron el gris de ProBuilder y la cuadrícula de prototipo `Test 2` (16 ranuras) y se limpiaron las 709 referencias obsoletas por cara de ProBuilder. La regla del validador pasó de advertencia a **error**. | `Art/Shaders/Environment/`, `Art/Materials/Primavera`, `Verano` |
| 26 oct | F1-B02 (luz y postprocesado) | Primavera: luz clave a 7 500 K con sombras suaves, ambiente en tres tonos azul noche, sin niebla y cielo procedural oscuro sin disco solar. `VP_Primavera` (Neutral, contraste +20, saturación −10, Bloom 0,9/0,6 sin filtrado de alta calidad, viñeta 0,2) como único Volume global de la escena. La cámara del jugador tenía el postprocesado desactivado: se activó con *dithering*. `Mobile_RPAsset` sin HDR (TDD §8.4). | `Rendering/Volumes/`, `Tutorial.unity` |
| 26 oct | F1-B03 | Seis iconos táctiles (relleno blanco con contorno oscuro, 256 px) en los botones; las etiquetas de texto se ocultan. Retícula fuera de alcance: la «+» gira 45° y forma una cruz «×» (forma además de color). `InputGlyphSet` con las etiquetas de las 11 acciones en los tres esquemas, tomadas de `InputSystem_Actions`. El HUD usa ♦, √ y » en lugar de ★, ✓ y ✦, que la fuente no tiene. | `Art/UI/Sprites/Touch/`, `Data/Input/InputGlyphSet_Default.asset` |
| 26 oct | VFX F1 | Shader aditivo `NOS/DataParticle` (cuadro con halo de Chebyshev o dígito 0/1 de 3×5 según `StableRandom.x`, con parpadeo por partícula). `SuctionVortexFx`: vórtice de ≤ 60 partículas en la boquilla, activado por `SuctionStateChanged`; sustituye el viento heredado, que creaba en ejecución una textura, dos materiales y seis `LineRenderer`. `GameplayVfxDirector`: un sistema persistente por efecto en espacio de mundo que se reposiciona y llama a `Emit` (salida de objetos, chispas de anclaje, bits de estrella y checkpoint), sin `Instantiate`. En móvil las partículas se multiplican por 0,4. `PostFXController`: anima solo el peso de un Volume de prioridad 100 con `VP_Feedback` (viñeta roja, aberración 0,6, desaturación) con la curva (1 − t/0,35)²; mantiene el efecto durante la muerte y publica `_NOS_DamagePulse`, que tiñe la rejilla del kit. | `Scripts/Presentation/VFX`, `Camera/PostFXController.cs` |
| 26 oct | QA-F1 (visual) | **Corregido:** en táctil, la pausa ocultaba todos los controles y no había forma de reanudar. Se añadió una capa de pausa en `GameRoot` con el resumen de controles por esquema y un botón «Continuar» (toque, ratón o mando, seleccionado al abrir). **Corregido:** la línea de acento y el destello de daño del HUD asignaban `Graphic.color` cada frame, lo que reconstruía el lienzo; ahora el color se escribe al cambiar de estado y el pulso usa `CanvasRenderer.SetAlpha`. Verificación en el editor: compilación sin errores ni advertencias nuevas; *Montar todo* con 0 errores y 0 advertencias; 77/77 pruebas EditMode y 9/9 PlayMode; revisión en juego del kit, la pausa y la reanudación. | `Phase1VisualBuilder.cs`, `Reporte_Validacion_Proyecto.md` |

---

## 3. Resultados de validación

| Verificación | Resultado | Método |
| :--- | :--- | :--- |
| Componentes `Terrain` / `TerrainCollider` / assets `TerrainData` | 0 en 20 escenas y prefabs | Revisión de cabeceras YAML (identificadores de clase 218, 154 y 156) |
| Referencias a scripts inexistentes | 0 | Resolución de GUID contra los `.meta` del proyecto y los paquetes |
| Instancias de prefabs inexistentes | 0 (tras la corrección de QA) | Igual que la anterior |
| Material por defecto de ProBuilder en el Tutorial | 711 referencias el 26 oct (advertencia); 0 tras F1-B02, y la regla es ahora un error | Validador del proyecto |
| Compilación de los ensamblados nuevos | Correcta | DLL generadas por el editor en `Library/ScriptAssemblies` |

El reporte oficial del validador se genera desde el editor con *NostalgIA › Validar proyecto* y se guarda en esta carpeta como `Reporte_Validacion_Proyecto.md`.

---

## 4. Pruebas automatizadas

| Ensamblado | Pruebas | Qué cubren |
| :--- | :-: | :--- |
| `NostalgIA.Tests.EditMode` | 77 | Fase 0 (32): capas, matriz de colisiones, Build y Player Settings, escena de arranque, créditos y validador. Fase 1, acabado visual (16): compilación de los shaders, `UnityPerMaterial` en todos los pases, materiales, ausencia del material de ProBuilder, perfiles de Volume con los valores del TDD, HDR móvil, glifos de las 11 acciones, iconos táctiles, vórtice, ráfagas, volumen de retroalimentación y pausa. Fase 1, ingeniería (29): bus de eventos (incluida la comprobación de 0 B por publicación), localizador de servicios, registro de reinicio, descripción de daño, asset de acciones contra las tablas del GDD, almacén FIFO, máscara de puntería, ausencia de `RaycastAll` y montaje de la Fase 1 |
| `NostalgIA.Tests.PlayMode` | 9 | Pulso de postprocesado por daño y su apagado; efecto de muerte mantenido hasta reaparecer; retícula en cruz fuera de alcance; capa de pausa solo en `Paused`; flujo de arranque; vida e invulnerabilidad; *instakill*; reaparición en el checkpoint en ≤ 1 s sin recargar la escena; conservación de la velocidad horizontal en el aire |

Ejecución: *Window › General › Test Runner*, pestañas *EditMode* y *PlayMode*. Resultado del 26 de octubre (cierre del acabado visual): 77/77 EditMode y 9/9 PlayMode aprobadas.

---

## 5. Hallazgos para la Fase 1

| Hallazgo | Impacto | Tarea que lo resuelve |
| :--- | :--- | :--- |
| La máscara del gancho en `Player.prefab` valía 507: el gancho se enganchaba a las capas Default, TransparentFX, Wall, Water, UI, Player, Absorbable y Grapple, no solo a Grapple como indica el TDD. | El diseño de niveles no podía usar el color naranja de anclaje como regla fiable. | **Resuelto en F1-A10.** Los 13 anclajes del Tutorial (`GrappleAnchor_*`) ya estaban en la capa `Grapple`; el kit de F1-B01 debe mantener esa regla. |
| El control aéreo reducía la velocidad de 8 a 5 m/s durante el salto: el alcance corriendo bajaba de ≈ 8,1 m a ≈ 6,0 m. | Contradecía la conservación de impulso del GDD. | **Resuelto en F1-A06** (prueba PlayMode `Motor_ConservaLaVelocidadHorizontalEnElAire`) |
| El renderer de PC referencia recursos internos de URP que no están en `Library/PackageCache` (se resuelven desde la instalación de Unity). | Ninguno; se documenta para no confundirlo con referencias rotas. | — |
| La velocidad máxima del stick derecho heredada del prototipo es 37,5 °/s (2,5 × 15). | Girar 180° con el mando tarda unos 5 s. | Revisar en las pruebas de juego de la Fase 2 (`InputReader.stickMaxSpeedX/Y`) |
| Los enemigos heredados escriben en consola con cadenas interpoladas en cada golpe y se destruyen al morir. | Asignaciones en combate; un enemigo eliminado no reaparece al reiniciar el checkpoint. | `EnemyBase` con *pooling* e `IResettable` (Fase 2) |
| `ToolVisualController` y `LavaHazard` aún crean materiales y texturas en tiempo de ejecución. | Asignaciones de carga y lotes fuera del SRP Batcher. | **Parcial:** el viento de succión ya no se crea si existe `SuctionVortexFx`. Quedan el tubo del gancho y la lava: Fase 3 (materiales como assets, TDD §6.5) |
| Al iniciar el Tutorial mueren por la lava electrónica dos Bugs y el guardia WinRar: el volumen de `Lava (4)` (≈ 55 × 243 m, escalado en la escena) cubre sus posiciones. Ya ocurría en el prototipo: la colocación es de la versión `6c0c22c`. | Enemigos que no llegan a enfrentarse al jugador. | Reubicar los enemigos o ajustar el volumen al rediseñar la Gran Canal (F2, diseño de nivel) |
| La fuente LiberationSans no tiene el glifo ★ del HUD provisional. | Se muestra un cuadro en su lugar (advertencia de TextMeshPro). | **Resuelto en F1-B03** con glifos WGL4; las fuentes definitivas (Guía de estilo §7) llegan con el HUD de la Fase 4 |
| Los cubos absorbibles del Tutorial usan `Objects.mat` (blanco), no el cian semántico de la Guía de estilo §3. | La señal de "absorbible" depende solo de la retícula. | Material de absorbibles (Fase 3, junto con el contorno emisivo) |
| F1-B01 (kit modular) y F1-B04 (`AnimatorController` de la herramienta) no se realizaron en esta entrega. | El Tutorial conserva su geometría ProBuilder con el material nuevo; la herramienta se sigue animando por código (`ToolVisualController`). | F1-B01 se modela en Blender/ProBuilder con las métricas de la Guía §4.3; F1-B04 junto con los clips en la Fase 3 |

---

## 6. Métricas de rendimiento

*Pendiente.* La línea base del Tutorial en PC y Android se registra al cierre de la Fase 1 (`Docs/Roadmap_Fases.md` §4, criterios de aceptación).

| Fecha | Build | Dispositivo | Escenario | FPS medio | FPS 1 % inferior | GC por frame | Captura |
| :--- | :--- | :--- | :--- | :-: | :-: | :-: | :--- |
| 26 oct | Editor (modo de juego) | Mac de desarrollo, ventana de juego 1488 × 837 | Inicio del Tutorial, objetivo iOS | — (CPU principal 6,3 ms) | — | No medido | Estadísticas del editor: 833 lotes, 62 *SetPass*, 346 k triángulos. Referencia; no sustituye la medición en build |

---

## 7. Riesgos de la semana

| Riesgo | Señal | Respuesta |
| :--- | :--- | :--- |
| La Fase 1 concentra 16 tareas de ingeniería en 5 días | Tareas F1-A06 a F1-A12 abiertas el 31 de octubre | Reducir Verano, Otoño e Invierno a segmentos de 3 minutos (`Docs/Roadmap_Fases.md` §11) |

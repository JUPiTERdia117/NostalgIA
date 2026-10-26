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
| Entrada unificada, bus de eventos, motor del jugador, herramientas, vida y reaparición (Fase 1, Pista A) | Código integrado el 26 de octubre; montaje en el editor y pruebas pendientes de ejecutar |

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

---

## 3. Resultados de validación

| Verificación | Resultado | Método |
| :--- | :--- | :--- |
| Componentes `Terrain` / `TerrainCollider` / assets `TerrainData` | 0 en 20 escenas y prefabs | Revisión de cabeceras YAML (identificadores de clase 218, 154 y 156) |
| Referencias a scripts inexistentes | 0 | Resolución de GUID contra los `.meta` del proyecto y los paquetes |
| Instancias de prefabs inexistentes | 0 (tras la corrección de QA) | Igual que la anterior |
| Material por defecto de ProBuilder en el Tutorial | 711 referencias (advertencia) | Se resuelve en la tarea F1-B02 |
| Compilación de los ensamblados nuevos | Correcta | DLL generadas por el editor en `Library/ScriptAssemblies` |

El reporte oficial del validador se genera desde el editor con *NostalgIA › Validar proyecto* y se guarda en esta carpeta como `Reporte_Validacion_Proyecto.md`.

---

## 4. Pruebas automatizadas

| Ensamblado | Pruebas | Qué cubren |
| :--- | :-: | :--- |
| `NostalgIA.Tests.EditMode` | 62 | Fase 0 (32): capas, matriz de colisiones, Build y Player Settings, escena de arranque, créditos y validador. Fase 1 (30): bus de eventos (incluida la comprobación de 0 B por publicación), localizador de servicios, registro de reinicio, descripción de daño, asset de acciones contra las tablas del GDD, almacén FIFO, máscara de puntería, ausencia de `RaycastAll` y montaje de la Fase 1 |
| `NostalgIA.Tests.PlayMode` | 5 | Flujo de arranque; vida e invulnerabilidad; *instakill*; reaparición en el checkpoint en ≤ 1 s sin recargar la escena; conservación de la velocidad horizontal en el aire |

Ejecución: *Window › General › Test Runner*, pestañas *EditMode* y *PlayMode*. Resultado: *pendiente de la primera ejecución en el editor*.

---

## 5. Hallazgos para la Fase 1

| Hallazgo | Impacto | Tarea que lo resuelve |
| :--- | :--- | :--- |
| La máscara del gancho en `Player.prefab` valía 507: el gancho se enganchaba a las capas Default, TransparentFX, Wall, Water, UI, Player, Absorbable y Grapple, no solo a Grapple como indica el TDD. | El diseño de niveles no podía usar el color naranja de anclaje como regla fiable. | **Resuelto en F1-A10.** Los 13 anclajes del Tutorial (`GrappleAnchor_*`) ya estaban en la capa `Grapple`; el kit de F1-B01 debe mantener esa regla. |
| El control aéreo reducía la velocidad de 8 a 5 m/s durante el salto: el alcance corriendo bajaba de ≈ 8,1 m a ≈ 6,0 m. | Contradecía la conservación de impulso del GDD. | **Resuelto en F1-A06** (prueba PlayMode `Motor_ConservaLaVelocidadHorizontalEnElAire`) |
| El renderer de PC referencia recursos internos de URP que no están en `Library/PackageCache` (se resuelven desde la instalación de Unity). | Ninguno; se documenta para no confundirlo con referencias rotas. | — |
| La velocidad máxima del stick derecho heredada del prototipo es 37,5 °/s (2,5 × 15). | Girar 180° con el mando tarda unos 5 s. | Revisar en las pruebas de juego de la Fase 2 (`InputReader.stickMaxSpeedX/Y`) |
| Los enemigos heredados escriben en consola con cadenas interpoladas en cada golpe y se destruyen al morir. | Asignaciones en combate; un enemigo eliminado no reaparece al reiniciar el checkpoint. | `EnemyBase` con *pooling* e `IResettable` (Fase 2) |
| `ToolVisualController` y `LavaHazard` aún crean materiales y texturas en tiempo de ejecución. | Asignaciones de carga y lotes fuera del SRP Batcher. | Fase 3 (materiales como assets, TDD §6.5) |

---

## 6. Métricas de rendimiento

*Pendiente.* La línea base del Tutorial en PC y Android se registra al cierre de la Fase 1 (`Docs/Roadmap_Fases.md` §4, criterios de aceptación).

| Fecha | Build | Dispositivo | Escenario | FPS medio | FPS 1 % inferior | GC por frame | Captura |
| :--- | :--- | :--- | :--- | :-: | :-: | :-: | :--- |
| — | — | — | — | — | — | — | — |

---

## 7. Riesgos de la semana

| Riesgo | Señal | Respuesta |
| :--- | :--- | :--- |
| La Fase 1 concentra 16 tareas de ingeniería en 5 días | Tareas F1-A06 a F1-A12 abiertas el 31 de octubre | Reducir Verano, Otoño e Invierno a segmentos de 3 minutos (`Docs/Roadmap_Fases.md` §11) |

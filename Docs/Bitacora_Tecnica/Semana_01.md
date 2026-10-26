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
| Entrada unificada, bus de eventos, motor del jugador, herramientas, vida y reaparición (Fase 1) | Pendiente |

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
| `NostalgIA.Tests.EditMode` | 32 | Constantes y nombres de capas, matriz de colisiones (incluida una prueba de regresión de las colisiones del prototipo), Build y Player Settings, escena de arranque, registro de créditos, reporte de validación y validador completo como prueba de aceptación |
| `NostalgIA.Tests.PlayMode` | 1 | Flujo de arranque: `00_Boot` carga el Tutorial y se descarga |

Ejecución: *Window › General › Test Runner*, pestañas *EditMode* y *PlayMode*. Resultado: *pendiente de la primera ejecución en el editor*.

---

## 5. Hallazgos para la Fase 1

| Hallazgo | Impacto | Tarea que lo resuelve |
| :--- | :--- | :--- |
| La máscara del gancho en `Player.prefab` vale 507: el gancho se engancha a las capas Default, TransparentFX, Wall, Water, UI, Player, Absorbable y Grapple, no solo a Grapple como indica el TDD. | El diseño de niveles no puede usar el color naranja de anclaje como regla fiable. | F1-A10 (`GrappleStats` con máscara `Layers.Masks.Grapple`) junto con F1-B01 (marcar las superficies de anclaje en el kit) |
| El control aéreo actual reduce la velocidad de 8 a 5 m/s durante el salto: el alcance corriendo pasa de ≈ 8,1 m a ≈ 6,0 m. | Contradice la conservación de impulso del GDD. | F1-A06 |
| El renderer de PC referencia recursos internos de URP que no están en `Library/PackageCache` (se resuelven desde la instalación de Unity). | Ninguno; se documenta para no confundirlo con referencias rotas. | — |

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

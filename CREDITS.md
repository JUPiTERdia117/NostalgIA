# Créditos y atribuciones — *NostalgIA*

**Proyecto:** *NostalgIA* — Fundamentos Técnicos de VFX  
**Equipo:** Integrante 1 (Ingeniería de Gameplay, Físicas, IA y Optimización) · Integrante 2 (Technical Artist)  
**Última actualización:** 26 de octubre de 2026

Este registro cumple la restricción 2 de los requisitos del curso: todo contenido externo se modifica de forma significativa y se documenta con su atribución. Cada activo nuevo se registra aquí **en el mismo *commit* en que se incorpora al proyecto**.

---

## 1. Contenido propio del equipo

| Activo | Ruta | Descripción | Herramienta |
| :--- | :--- | :--- | :--- |
| Dron cámara / herramienta (modelo y texturas) | `Assets/_Project/Art/Models/Tool/` (`robot.fbx`, `body.png`, `detail.png`) | Modelo final con rig de huesos para garras, aletas y depósito | Blender 5.2 |
| Antivirus (modelo y texturas) | `Assets/_Project/Art/Models/Enemies/Antivirus/` (`winrarModel.fbx`, `libroTextura.png`, `dientesTextura.png`) | Modelo final | Blender 5.2 |
| Correo de Spam (modelo) | `Assets/_Project/Art/Models/Enemies/SpamMail/spamMail.fbx` | Modelo final | Blender 5.2 |
| Troyano (modelo) | `Assets/_Project/Art/Models/Enemies/Trojan/troyano.fbx` | Modelo final | Blender 5.2 |
| Shader de lava electrónica | `Assets/_Project/Resources/BlueCodeLava.shader` | Shader HLSL final para URP | Unity / HLSL |
| Shader del portal dimensional | `Assets/_Project/Resources/DimensionalPortal.shader` | Shader HLSL para URP | Unity / HLSL |
| Shader del rayo de herramienta | `Assets/_Project/Resources/ToolRay.shader` | Shader HLSL para URP | Unity / HLSL |
| Código C# | `Assets/_Project/Scripts/` | Arquitectura, jugabilidad, herramientas de editor y pruebas | Unity / C# |
| Niveles | `Assets/_Project/Scenes/` | Geometría modular con ProBuilder; sin `Terrain` | Unity / ProBuilder |

---

## 2. Contenido externo

| Activo | Ruta | Autor / origen | Licencia | Modificación realizada por el equipo |
| :--- | :--- | :--- | :--- | :--- |
| EasyPeasy First Person Controller (documentación; código derivado en `_Project`) | `Assets/ThirdParty/EasyPeasyFirstPersonController/` | Unity Asset Store — publicador: *pendiente de registrar* | Standard Unity Asset Store EULA | Durante el prototipo se integró el gancho y se adaptó la entrada al Input System. Se retiraron la escena, el prefab, los materiales y la textura de demostración. Después el código se reestructuró por completo: los estados de movimiento, el salto, el agachado, el deslizamiento y el remolque del gancho pasaron a `Assets/_Project/Scripts/Gameplay/Player/PlayerMotor.cs`, y las curvas de FOV, balanceo, inclinación y sacudida a `Assets/_Project/Scripts/Presentation/Camera/PlayerCameraFeedback.cs`. Ambos archivos conservan esta atribución en su cabecera. En `Assets/ThirdParty/` solo permanece la documentación original. Los estados de nado y agarre de cornisa se descartaron por no formar parte del GDD. |
| TextMesh Pro — recursos esenciales | `Assets/TextMesh Pro/` | Unity Technologies | Unity Companion License | Sin modificar; recurso de la interfaz de usuario |
| Fuente Liberation Sans (incluida en TextMesh Pro) | `Assets/TextMesh Pro/Fonts/` | Red Hat, Inc. | SIL Open Font License 1.1 | Sin modificar; se sustituirá por las fuentes definitivas de la interfaz |
| Paquetes de Unity (URP, Input System, ProBuilder, AI Navigation, Timeline, Test Framework) | `Packages/manifest.json` | Unity Technologies | Unity Companion License | Dependencias del motor; no se redistribuyen modificadas |

---

## 3. Audio

Aún no se ha incorporado audio. Cada clip se registrará con esta estructura:

| Clip | Ruta | Autor / origen | Licencia | Modificación (edición, mezcla, procesado) |
| :--- | :--- | :--- | :--- | :--- |
| — | — | — | — | — |

Licencias aceptadas para audio externo: CC0, CC BY (con atribución en esta tabla) o licencias comerciales que permitan uso en videojuegos.

---

## 4. Referencias e inspiraciones citadas en el GDD

Las siguientes obras y marcas se citan como **referencia estética o conceptual**; el proyecto no incluye material original de ellas. Las marcas pertenecen a sus respectivos titulares y su mención no implica afiliación.

| Referencia | Uso en *NostalgIA* | Titular |
| :--- | :--- | :--- |
| *Portal* | Referencia principal de mecánicas | Valve Corporation |
| *ENA* | Inspiración visual principal | Joel Guerra |
| *Smash Hit* | Interacción física y destrucción | Mediocre AB |
| *Fall Guys* | Diseño de obstáculos y color | Mediatonic / Epic Games |
| Icono de WinRAR | Inspiración del enemigo Antivirus (reinterpretado en un modelo propio) | win.rar GmbH |
| Cursores e iconografía de Windows XP | Inspiración del Correo de Spam y de la estética Webcore | Microsoft Corporation |
| HEXBUG | Inspiración de los Bugs | Innovation First, Inc. |
| Escultura *El Caballito* (Sebastián, CDMX) | Inspiración del Troyano | Sebastián (Enrique Carbajal) |

---

## 5. Procedimiento para registrar un activo nuevo

1. Verificar que la licencia permita su uso en un videojuego y su modificación.
2. Modificarlo de forma significativa (geometría, texturizado, shader propio o integración técnica) y describir el cambio en la tabla correspondiente.
3. Guardarlo en `Assets/_Project/` si fue modificado o en `Assets/ThirdParty/` si se conserva como dependencia.
4. Agregar la fila en este archivo dentro del mismo *commit*.

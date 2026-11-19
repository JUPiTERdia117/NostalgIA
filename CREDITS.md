# Créditos y atribuciones — *NostalgIA*

En cumplimiento de los lineamientos del curso, el proyecto mantiene el registro completo de autoría y modificaciones en este archivo.

## Contenido propio

Los modelos del dron cámara/herramienta, los enemigos (Antivirus, Spam y Troyano), los shaders documentados, el código C#, el diseño de niveles y el audio de la aspiradora son de autoría propia.

## Contenido externo

Los recursos externos integrados y adaptados son:

| Recurso externo | Origen / Licencia | Modificación realizada |
| :--- | :--- | :--- |
| EasyPeasy First Person Controller (base inicial) | Unity Asset Store (Standard EULA) | Refactorizado por completo en `PlayerMotor.cs` y `PlayerCameraFeedback.cs`; adaptado a Input System y gancho; se descartaron escenas y estados ajenos al GDD. |
| Modern Menu 1 (SlimUI) | Unity Asset Store (Standard EULA) | Traducido al español e integrado con el flujo de escenas, selección de estación y panel de ajustes gráficos propios. |
| Paquetes de SFX de Kenney (10 clips seleccionados) | Kenney (CC0 1.0) | Integrados en Audio Random Containers con variación de tono/volumen y controlados desde `GameSfx.cs`. |
| Paquetes oficiales de Unity y TextMesh Pro | Unity Technologies (Companion License) / Liberation Sans (OFL 1.1) | Dependencias estándar del motor y tipografía base de interfaz. |

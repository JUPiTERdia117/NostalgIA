# Manual de Usuario — *NostalgIA*

**Proyecto:** *NostalgIA* — Fundamentos Técnicos de VFX  
**Versión del documento:** 0.1 (esqueleto, tarea F0-B03) — 26 de octubre de 2026  
**Estado:** los controles corresponden al esquema definitivo del GDD, que se implementa en la tarea F1-A02. Las secciones marcadas como *pendiente* se completan en las fases indicadas y la versión final se entrega el 19 de noviembre de 2026.

---

## 1. Acerca del juego

*NostalgIA* es un juego de plataformas 3D en primera persona. El jugador controla un dron cámara que explora cuatro ecosistemas digitales de la *World Wide Web* para detener a una Inteligencia Artificial que los está homogeneizando. Para avanzar se combinan el movimiento ágil con dos herramientas: la **Aspiradora Lanzadora** y el **Gancho**.

---

## 2. Requisitos del sistema

| Perfil | Mínimo | Recomendado |
| :--- | :--- | :--- |
| PC | Windows 10 (64 bits) o macOS 13; Intel Core i5-8400 / AMD Ryzen 3 3100 / Apple M1; 8 GB de RAM; GTX 1050 Ti / RX 560 | Windows 11 o macOS 14; Ryzen 5 5600X / Apple M2; 16 GB de RAM; RTX 2060 / RX 6600 |
| Móvil | Android 10 (API 29) o iOS 15; Snapdragon 720G / Helio G95 / Apple A13; 4 GB de RAM | Android 13 o iOS 17; Snapdragon 8 Gen 1 / Apple A15; 6 a 8 GB de RAM |

Detalle completo en `Docs/Planeacion_Costos_y_Gantt.md` (§4).

---

## 3. Instalación

*Pendiente (Fase 4).* Se describirán los pasos para el build de PC y para instalar el APK en Android.

---

## 4. Controles

El juego detecta automáticamente el dispositivo en uso. Si se conecta un mando de Xbox, la pantalla muestra sus botones; en móvil, los controles táctiles se ocultan mientras se usa el mando.

### 4.1. Movimiento

| Acción | PC (teclado y ratón) | Móvil (táctil) | Mando de Xbox |
| :--- | :--- | :--- | :--- |
| Moverse | `W` `A` `S` `D` | Pad virtual izquierdo | Stick izquierdo |
| Mirar | Ratón | Deslizar el dedo en la mitad derecha | Stick derecho |
| Saltar | `Espacio` | Botón de salto | `A` |
| Correr | Mantener `Shift` | Llevar el pad izquierdo hasta arriba | Mantener `L3` al moverse |
| Agacharse | `C` | Botón de agacharse | `R3` |
| Deslizarse | Agacharse mientras se corre | Agacharse mientras se corre | `R3` mientras se corre |
| Pausa | `Esc` | Botón `||` del HUD | `Start` |

### 4.2. Herramientas

Solo una herramienta está activa a la vez.

| Acción | PC (teclado y ratón) | Móvil (táctil) | Mando de Xbox |
| :--- | :--- | :--- | :--- |
| Cambiar de herramienta | `F` | Botón de cambio de herramienta | `Y` |
| Absorber objetos (Aspiradora) | Mantener clic derecho | Mantener el pad derecho | Mantener `LT` |
| Disparar objeto (Aspiradora) | Clic izquierdo | Tocar el pad derecho | `RT` |
| Usar el gancho | Clic derecho | Botón de gancho | `RT` |
| Cancelar el gancho | Volver a pulsar clic derecho | Volver a pulsar el botón de gancho | Volver a pulsar `RT` |

Las teclas de cambio de herramienta y de agacharse se pueden reasignar desde *Opciones* (*pendiente, Fase 2*).

---

## 5. Cómo se juega

### 5.1. Objetivo de cada Estación

Recorrer la Estación desde el inicio hasta la **puerta de meta**. En cada Estación hay **3 estrellas** en rutas opcionales o más difíciles; recogerlas todas "platina" la Estación.

### 5.2. Aspiradora Lanzadora

- Mantener la acción de absorber crea un vórtice frente al dron que atrae los objetos compatibles.
- Los objetos se guardan hasta la capacidad máxima; el depósito de la herramienta se infla para indicar cuánto falta.
- Al disparar sale primero el objeto que se absorbió primero.
- Los objetos lanzados dañan enemigos, rompen barreras, activan mecanismos y empujan objetos pesados.
- No absorbe enemigos.

### 5.3. Gancho

- Se engancha a las superficies marcadas con placas naranjas dentro de su alcance.
- Tira del dron hacia el punto de anclaje; se puede cancelar en cualquier momento para conservar el impulso y encadenar saltos.
- Tras usarlo hay un breve enfriamiento en el que ninguna herramienta responde.
- No se engancha a enemigos.

### 5.4. Vida y reaparición

- El dron tiene 3 corazones. Los enemigos y algunos obstáculos restan corazones.
- Caer al vacío, tocar la lava electrónica o ser aplastado por el Antivirus provoca muerte instantánea.
- Al morir, el dron reaparece en el último punto de control activado.

### 5.5. Interfaz (HUD)

| Elemento | Ubicación | Significado |
| :--- | :--- | :--- |
| Corazones | Abajo al centro | Vida restante |
| Estrellas | Arriba a la derecha | Estrellas recogidas en la Estación (de 3) |
| Botón de pausa | Arriba a la izquierda | Abre el menú de pausa |
| Retícula | Centro | Indica si se apunta a un objeto absorbible o a un anclaje válido |
| Herramienta | Abajo a la derecha | Herramienta activa y llenado del depósito |

---

## 6. Estaciones

| Estación | Ambiente | Qué se aprende o exige |
| :--- | :--- | :--- |
| 1 — Primavera | Simulación retro de computadora (Webcore) | Tutorial: movimiento, aspiradora, gancho y primeros enemigos |
| 2 — Verano | Universo virtual luminoso (Frutiger Aero) | Saltos largos y más enemigos en espacios abiertos |
| 3 — Otoño | Espacios liminales con niebla (Dreamcore) | Plataformas escasas y precisión |
| 4 — Invierno | Interior de una placa base corrupta | Laberinto final frente a la IA |

Al completar cada Estación se obtiene una mejora permanente: mayor alcance del gancho o mayor capacidad de la aspiradora.

---

## 7. Enemigos

| Enemigo | Comportamiento | Cómo enfrentarlo |
| :--- | :--- | :--- |
| Correo de Spam | Flota y dispara proyectiles a distancia | Lanzarle objetos o cubrirse |
| Bugs | Persiguen en enjambre y atacan por contacto | Mantener distancia y lanzarles objetos |
| Troyano | Parece parte del escenario y libera Bugs al detectar al dron | Destruirlo antes de que libere más Bugs |
| Antivirus | Cae desde arriba y aplasta todo lo que está debajo | Observar su aviso y cruzar en el momento correcto |

---

## 8. Opciones

*Pendiente (Fase 2).* Sensibilidad de cámara, inversión del eje Y, reasignación de controles, calidad gráfica y volumen.

---

## 9. Resultados obtenidos

*Pendiente (Fase 4).* Se reportarán el rendimiento medido en PC y móvil, el grado de cumplimiento de los requisitos y el estado final de cada Estación.

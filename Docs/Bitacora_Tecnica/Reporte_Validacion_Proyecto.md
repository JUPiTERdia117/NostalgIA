# Reporte de validación del proyecto — NostalgIA

**Fecha:** 2026-10-26 12:15  
**Unity:** 6000.3.7f1  
**Resultado:** APROBADO — 0 error(es), 1 advertencia(s)

Reporte generado por el validador del proyecto (menú *NostalgIA › Validar proyecto* y paso previo a cada build). Las reglas provienen de `Docs/Requisitos.md` y `Docs/TDD_Arquitectura.md` (§15).

## Reglas cumplidas

- Serialización de texto
- Sin Terrain: 0 componentes Terrain/TerrainCollider y 0 assets TerrainData en 24 escenas y prefabs.
- Sin scripts faltantes: 24 escenas y prefabs revisados.
- Sin prefabs faltantes: 24 escenas y prefabs revisados.
- Build Settings: 3 escena(s), 00_Boot primero, sin escenas de prueba.
- Capas físicas (TDD §13)
- Matriz de colisiones (TDD §13)
- Dependencias entre ensamblados (TDD §2.1)

## Hallazgos

| Gravedad | Regla | Activo | Detalle |
| :--- | :--- | :--- | :--- |
| Advertencia | Sin material por defecto de ProBuilder en Estaciones | `Assets/_Project/Scenes/Stations/Tutorial.unity` | 711 referencia(s) al material gris de prototipo de ProBuilder. Reemplazar por materiales de la Estación (tarea F1-B02). |

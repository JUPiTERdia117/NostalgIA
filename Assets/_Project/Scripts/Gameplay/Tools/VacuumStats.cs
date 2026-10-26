// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: VacuumStats.cs
// Propósito: parámetros de diseño de la Aspiradora Lanzadora (TDD §10,
// tarea F1-A15).
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Gameplay.Tools
{
    /// <summary>
    /// Parámetros de la Aspiradora Lanzadora. Asset inmutable en tiempo de ejecución.
    /// </summary>
    /// <remarks>
    /// Valores por defecto tomados del prototipo. La capacidad base es 20 porque el Tutorial
    /// sobrescribía en su escena la capacidad del prefab (5); la revisión de balance queda para
    /// la congelación de mecánicas de la Fase 2.
    /// </remarks>
    [CreateAssetMenu(menuName = "NostalgIA/Herramientas/Vacuum Stats", fileName = "VacuumStats")]
    public sealed class VacuumStats : ScriptableObject
    {
        [Header("Detección en cono")]
        [Tooltip("Alcance de la succión, en metros.")]
        [Min(0.5f)] public float range = 10f;
        [Tooltip("Semiángulo del cono de succión, en grados.")]
        [Range(1f, 89f)] public float coneHalfAngle = 25f;
        [Tooltip("Objetos que pueden recorrer su trayectoria de absorción a la vez.")]
        [Range(1, 16)] public int maxSimultaneousPulls = 3;

        [Header("Trayectoria de absorción")]
        [Tooltip("Duración de la trayectoria, en segundos, independiente de la distancia.")]
        [Min(0.01f)] public float absorptionDuration = 1f;
        [Tooltip("Radio máximo de la espiral, en metros (cero la elimina).")]
        [Min(0f)] public float spiralRadius = 0.5f;
        [Tooltip("Vueltas de la espiral durante la trayectoria completa.")]
        [Min(0f)] public float spiralTurns = 2f;
        [Tooltip("Distancia a la boquilla que completa la absorción, en metros.")]
        [Min(0f)] public float absorptionDistance = 0.2f;
        [Tooltip("Segundos para recuperar el tamaño al interrumpir la absorción.")]
        [Min(0.01f)] public float scaleRestoreDuration = 0.2f;

        [Header("Almacenamiento")]
        [Tooltip("Capacidad base del depósito (antes de mejoras).")]
        [Min(1)] public int baseCapacity = 20;

        [Header("Disparo")]
        [Tooltip("Impulso de salida, en N·s. La velocidad resultante es impulso / masa.")]
        [Min(0f)] public float launchImpulse = 50f;
        [Tooltip("Separación entre la boquilla y el objeto al aparecer, en metros.")]
        [Min(0.01f)] public float launchClearance = 0.2f;
        [Tooltip("Intensidad del giro inicial (se divide por el tamaño del objeto).")]
        [Min(0f)] public float launchSpin = 2f;
        [Tooltip("Giro inicial máximo, en rad/s.")]
        [Min(0f)] public float maxLaunchSpin = 5f;
        [Tooltip("Distancia del punto de convergencia del disparo cuando la retícula no apunta a nada, en metros.")]
        [Min(1f)] public float launchAimDistance = 60f;
    }
}

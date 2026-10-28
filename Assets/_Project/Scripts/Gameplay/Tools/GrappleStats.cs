// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: GrappleStats.cs
// Propósito: parámetros de diseño del gancho (TDD §10, tarea F1-A15).
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Gameplay.Tools
{
    /// <summary>
    /// Parámetros del gancho. Asset inmutable en tiempo de ejecución; las mejoras de alcance se
    /// aplican sobre el valor de ejecución de <c>GrappleTool</c>.
    /// </summary>
    /// <remarks>
    /// Valores por defecto del prototipo. El alcance base es 30 m porque el Tutorial sobrescribía
    /// en su escena el alcance del prefab (60 m).
    /// </remarks>
    [CreateAssetMenu(menuName = "NostalgIA/Herramientas/Grapple Stats", fileName = "GrappleStats")]
    public sealed class GrappleStats : ScriptableObject
    {
        [Tooltip("Distancia máxima de enganche en metros (antes de mejoras).")]
        [Min(1f)] public float maxRange = 30f;
        [Tooltip("Radio de llegada alrededor del ancla, medido desde el centro del jugador, en metros.")]
        [Min(0f)] public float arrivalRadius = 1.5f;
        [Tooltip("Tiempo máximo de tracción por enganche, en segundos.")]
        [Min(0.1f)] public float maxPullDuration = 3f;
        [Tooltip("Distancia que se puede rebasar el ancla en la dirección inicial antes de soltar, en metros.")]
        [Min(0.1f)] public float overshootDistance = 2f;
        [Tooltip("Gravedad durante el enganche, en m/s². Cero usa el 35 % de la gravedad del jugador.")]
        [Min(0f)] public float gravity;
        [Tooltip("Aceleración de tracción hacia el ancla, en m/s².")]
        [Min(0f)] public float pullAcceleration = 38f;
        [Tooltip("Velocidad máxima añadida hacia el ancla, en m/s.")]
        [Min(1f)] public float pullSpeed = 24f;
        [Tooltip("Tiempo de la rampa de aceleración, en segundos.")]
        [Min(0.01f)] public float pullRampTime = 0.2f;
        [Tooltip("Aceleración del control aéreo durante el remolque, en m/s².")]
        [Min(0f)] public float airControl = 8f;
        [Tooltip("Fracción de velocidad conservada al cancelar el gancho.")]
        [Range(0f, 1f)] public float releaseMomentumRetention = 0.85f;
        [Tooltip("Impulso vertical al engancharse desde el suelo, en m/s.")]
        [Min(0f)] public float launchLift = 4f;
        [Tooltip("Enfriamiento compartido de herramientas tras soltar el gancho, en segundos.")]
        [Min(0f)] public float cooldown = 1.5f;
    }
}

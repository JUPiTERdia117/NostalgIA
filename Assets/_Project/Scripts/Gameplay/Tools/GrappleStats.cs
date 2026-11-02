// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: GrappleStats.cs
// Propósito: parámetros de diseño del gancho.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Gameplay.Tools
{
    /// <summary>
    /// Parámetros del gancho. Asset inmutable en tiempo de ejecución; las mejoras de alcance se
    /// aplican sobre el valor de ejecución de <c>GrappleTool</c>.
    /// </summary>
    /// <remarks>
    /// El alcance base es 30 m porque el Tutorial sobrescribía en su escena el alcance del prefab
    /// (60 m). La tracción la ajusté tomando como referencia el Grappleshot de Halo Infinite: jala
    /// fuerte desde el primer instante, casi en línea recta y sin colgarse, y se puede cancelar con
    /// un salto que conserva el impulso.
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
        [Tooltip("Respaldo: distancia que se puede rebasar el ancla en la dirección inicial antes de soltar, en metros. Normalmente suelta antes, en cuanto el jugador deja de acercarse.")]
        [Min(0.1f)] public float overshootDistance = 2f;
        [Tooltip("Gravedad durante el enganche, en m/s². Cero usa el 35 % de la gravedad del jugador.")]
        [Min(0f)] public float gravity = 2f;
        [Tooltip("Aceleración de tracción hacia el ancla, en m/s².")]
        [Min(0f)] public float pullAcceleration = 70f;
        [Tooltip("Velocidad máxima añadida hacia el ancla, en m/s.")]
        [Min(1f)] public float pullSpeed = 26f;
        [Tooltip("Tiempo de la rampa de aceleración, en segundos.")]
        [Min(0.01f)] public float pullRampTime = 0.08f;
        [Tooltip("Aceleración del control aéreo justo después de soltar dentro del mismo estado de gancho, en m/s².")]
        [Min(0f)] public float airControl = 8f;

        [Header("Control mientras jala")]
        [Tooltip("Aceleración con la que el jugador desvía su trayectoria (perpendicular al cable), en m/s².")]
        [Min(0f)] public float steerAcceleration = 30f;
        [Tooltip("Velocidad máxima de desvío perpendicular al cable que puede dar la entrada, en m/s.")]
        [Min(0f)] public float steerMaxSpeed = 11f;
        [Tooltip("Amortiguación de la velocidad perpendicular al cable SOLO cuando no hay entrada, en 1/s. Endereza la trayectoria si sueltas el control.")]
        [Min(0f)] public float lateralDamping = 1.5f;

        [Header("Control al soltar")]
        [Tooltip("Aceleración del control aéreo durante un rato después de soltar el gancho, en m/s² (la normal en el aire es 5).")]
        [Min(0f)] public float releaseAirAcceleration = 18f;
        [Tooltip("Cuánto dura ese control aéreo reforzado después de soltar, en segundos.")]
        [Min(0f)] public float releaseAirControlTime = 1.2f;
        [Tooltip("Fracción de velocidad conservada al cancelar el gancho.")]
        [Range(0f, 1f)] public float releaseMomentumRetention = 0.85f;
        [Tooltip("Impulso vertical al engancharse desde el suelo, en m/s.")]
        [Min(0f)] public float launchLift = 4f;

        [Header("Salto con gancho")]
        [Tooltip("Velocidad vertical mínima al saltar mientras el gancho jala, en m/s (el salto normal es 5).")]
        [Min(0f)] public float jumpLaunchSpeed = 6.5f;
        [Tooltip("Impulso extra hacia donde mira la cámara al saltar con el gancho, en m/s.")]
        [Min(0f)] public float jumpForwardBoost = 2.5f;

        [Header("Llegada")]
        [Tooltip("Velocidad vertical mínima al llegar a un ancla en pared o esfera, en m/s. Sirve para subir al borde.")]
        [Min(0f)] public float arrivalLift = 5f;

        [Tooltip("Enfriamiento compartido de herramientas tras soltar el gancho, en segundos.")]
        [Min(0f)] public float cooldown = 1.5f;
    }
}

// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: PlayerSettings.cs
// Propósito: parámetros de diseño del jugador: movimiento, salto, deslizamiento,
// alturas y vida.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Gameplay.Player
{
    /// <summary>
    /// Definición inmutable de los parámetros del jugador.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los valores por defecto son los del prototipo jugable (<c>Player.prefab</c> previo a la
    /// reestructuración), de modo que el cambio de arquitectura no altera la sensación de control. Con salto
    /// v₀ = 5 m/s y g = 9,81 m/s², la altura máxima es h = v₀² / 2g ≈ 1,27 m y el tiempo de vuelo
    /// t = 2v₀ / g ≈ 1,02 s.
    /// </para>
    /// <para>
    /// No se debe modificar el asset en tiempo de ejecución: los valores alterables
    /// por mejoras viven en objetos C# creados a partir de él.
    /// </para>
    /// <para>
    /// Nota de nomenclatura: comparte nombre con <c>UnityEditor.PlayerSettings</c>; el código de
    /// editor que use ambas clases debe calificar el espacio de nombres.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "NostalgIA/Jugador/Player Settings", fileName = "PlayerSettings")]
    public sealed class PlayerSettings : ScriptableObject
    {
        [Header("Velocidades (m/s)")]
        [Tooltip("Velocidad al caminar.")]
        [Min(0f)] public float walkSpeed = 5f;
        [Tooltip("Velocidad al correr (requiere Sprint y avance positivo).")]
        [Min(0f)] public float sprintSpeed = 8f;
        [Tooltip("Velocidad agachado.")]
        [Min(0f)] public float crouchSpeed = 1.5f;
        [Tooltip("Aceleración en el suelo hacia la velocidad objetivo, en m/s².")]
        [Min(0f)] public float groundAcceleration = 50f;
        [Tooltip("Desaceleración en el suelo sin entrada, en m/s².")]
        [Min(0f)] public float groundDeceleration = 60f;

        [Header("Salto y aire")]
        [Tooltip("Velocidad vertical inicial del salto, en m/s.")]
        [Min(0f)] public float jumpSpeed = 5f;
        [Tooltip("Gravedad, en m/s².")]
        [Min(0f)] public float gravity = 9.81f;
        [Tooltip("Velocidad horizontal máxima que el control aéreo puede alcanzar acelerando. Por encima solo actúa el arrastre aéreo (conservación de impulso).")]
        [Min(0f)] public float maxAirSpeed = 8f;
        [Tooltip("Aceleración del control aéreo hacia la entrada, en m/s² (prototipo: 5).")]
        [Min(0f)] public float airAcceleration = 5f;
        [Tooltip("Arrastre aéreo aplicado solo al exceso de velocidad sobre maxAirSpeed, en 1/s.")]
        [Min(0f)] public float airDrag = 0.15f;
        [Tooltip("Tiempo de gracia para saltar tras dejar el suelo (coyote time), en segundos.")]
        [Min(0f)] public float coyoteTime = 0.1f;
        [Tooltip("Tiempo durante el cual se recuerda una pulsación de salto antes de aterrizar, en segundos.")]
        [Min(0f)] public float jumpBufferTime = 0.12f;

        [Header("Deslizamiento")]
        [Tooltip("Duración del deslizamiento en terreno plano, en segundos.")]
        [Min(0.05f)] public float slideDuration = 1.5f;
        [Tooltip("Velocidad del deslizamiento al iniciar, en m/s.")]
        [Min(0f)] public float slideSpeed = 6f;
        [Tooltip("Velocidad horizontal mínima para iniciar un deslizamiento desde la carrera, en m/s.")]
        [Min(0f)] public float slideMinSpeed = 3f;
        [Tooltip("Velocidad lateral de dirección durante el deslizamiento, en m/s.")]
        [Min(0f)] public float slideSteerControl = 4f;
        [Tooltip("Multiplicador de consumo del deslizamiento cuesta arriba.")]
        [Min(0f)] public float slideUphillFriction = 3f;
        [Tooltip("Si está activo, el deslizamiento cuesta abajo recupera duración (deslizamiento continuo).")]
        public bool enableSlopeSliding = true;

        [Header("Alturas (m)")]
        [Tooltip("Altura de la cámara de pie, medida desde los pies.")]
        [Min(0.1f)] public float standingCameraHeight = 1.75f;
        [Tooltip("Altura de la cámara agachado.")]
        [Min(0.1f)] public float crouchingCameraHeight = 1f;
        [Tooltip("Altura de la cápsula agachado.")]
        [Min(0.1f)] public float crouchingCapsuleHeight = 1f;
        [Tooltip("Velocidad de transición de la cápsula al agacharse, en m/s.")]
        [Min(0.1f)] public float crouchTransitionSpeed = 10f;
        [Tooltip("Velocidad de seguimiento de la altura de la cámara, en 1/s.")]
        [Min(0.1f)] public float cameraHeightSharpness = 8f;

        [Header("Mirada")]
        [Tooltip("Ángulo máximo de cabeceo hacia arriba y hacia abajo, en grados.")]
        [Range(10f, 90f)] public float maxPitch = 90f;

        [Header("Gancho")]
        [Tooltip("Frecuencia de los subpasos de integración del remolque, en Hz (≈ 90 Hz en el prototipo).")]
        [Min(30f)] public float grappleSubstepRate = 90f;
        [Tooltip("Tiempo bloqueado contra una superficie tras el cual se suelta el gancho, en segundos.")]
        [Min(0.05f)] public float grappleBlockedRelease = 0.25f;

        [Header("Vida")]
        [Tooltip("Corazones máximos.")]
        [Min(1)] public int maxHearts = 3;
        [Tooltip("Invulnerabilidad tras recibir daño normal, en segundos.")]
        [Min(0f)] public float invulnerabilityDuration = 0.5f;
        [Tooltip("Rapidez vertical de aterrizaje a partir de la cual la caída es letal, en m/s (0 desactiva). 24 m/s ≈ caída libre de 29 m.")]
        [Min(0f)] public float lethalFallSpeed = 24f;

        [Header("Reaparición")]
        [Tooltip("Duración de la secuencia de muerte antes de reubicar al jugador, en segundos (≈ 0,8 s; meta ≤ 1 s).")]
        [Range(0f, 1f)] public float respawnDelay = 0.8f;
    }
}

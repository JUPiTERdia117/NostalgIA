// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: GameplayVfxDirector.cs
// Propósito: ráfagas de partículas reactivas a eventos de jugabilidad: salida de
// objetos, anclaje del gancho, estrellas y checkpoints (TDD §8.2 y §8.6).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using UnityEngine;

namespace NostalgIA.Presentation.VFX
{
    /// <summary>
    /// Director de ráfagas: un sistema de partículas persistente por tipo de efecto, que se
    /// reposiciona y emite al recibir el evento correspondiente.
    /// </summary>
    /// <remarks>
    /// <para>
    /// En este módulo se decidió no instanciar un sistema por ráfaga: cada efecto usa un único
    /// <see cref="ParticleSystem"/> con simulación en espacio de mundo. Al llegar un evento se
    /// coloca el emisor en el punto del evento y se llama a <see cref="ParticleSystem.Emit(int)"/>;
    /// las partículas ya emitidas siguen su curso donde nacieron porque la simulación es de
    /// mundo. Así varias ráfagas simultáneas comparten un solo sistema, sin
    /// <c>Instantiate</c>, sin <c>Destroy</c> y sin asignaciones (equivale a un pool de
    /// partículas gestionado por el propio motor).
    /// </para>
    /// <para>
    /// Orientación de cada ráfaga: el eje Z local del emisor apunta a la dirección de salida
    /// (disparo) o a la normal de la superficie (anclaje), mediante
    /// <see cref="Quaternion.LookRotation(Vector3)"/>; la forma de cono del sistema abre las
    /// partículas alrededor de ese eje.
    /// </para>
    /// <para>
    /// La cantidad de partículas por ráfaga se escala en móvil por
    /// <see cref="mobileBurstMultiplier"/> (TDD §8.2: presupuesto de 1 000 partículas en móvil).
    /// </para>
    /// </remarks>
    public sealed class GameplayVfxDirector : MonoBehaviour
    {
        [Header("Sistemas (simulación en espacio de mundo)")]
        [Tooltip("Destello de salida digital al disparar un objeto (ObjectLaunched).")]
        [SerializeField] private ParticleSystem launchBurst;
        [Tooltip("Chispas de anclaje del gancho (GrappleAttached).")]
        [SerializeField] private ParticleSystem anchorSparks;
        [Tooltip("Bits ascendentes al recoger una estrella o activar un checkpoint.")]
        [SerializeField] private ParticleSystem collectBits;

        [Header("Partículas por ráfaga (PC)")]
        [SerializeField, Min(1)] private int launchCount = 18;
        [SerializeField, Min(1)] private int anchorCount = 24;
        [SerializeField, Min(1)] private int collectCount = 36;

        [Header("Plataforma")]
        [Tooltip("Multiplicador de partículas en móvil (TDD §8.2: 0,4).")]
        [SerializeField, Range(0.1f, 1f)] private float mobileBurstMultiplier = 0.4f;

        [Header("Colores semánticos (Guía de estilo §3)")]
        [SerializeField] private Color starColor = new Color(1f, 0.82f, 0.4f, 1f);        // #FFD166
        [SerializeField] private Color checkpointColor = new Color(0.22f, 0.9f, 0.3f, 1f); // #38E54D

        /// <summary>Multiplicador efectivo según la plataforma (se fija en Awake).</summary>
        private float burstScale = 1f;

        /// <summary>Fija el presupuesto de plataforma.</summary>
        private void Awake() => burstScale = Application.isMobilePlatform ? mobileBurstMultiplier : 1f;

        /// <summary>Suscripciones.</summary>
        private void OnEnable()
        {
            EventBus<ObjectLaunched>.Subscribe(OnObjectLaunched);
            EventBus<GrappleAttached>.Subscribe(OnGrappleAttached);
            EventBus<StarCollected>.Subscribe(OnStarCollected);
            EventBus<CheckpointActivated>.Subscribe(OnCheckpointActivated);
        }

        /// <summary>Cancela suscripciones.</summary>
        private void OnDisable()
        {
            EventBus<ObjectLaunched>.Unsubscribe(OnObjectLaunched);
            EventBus<GrappleAttached>.Unsubscribe(OnGrappleAttached);
            EventBus<StarCollected>.Unsubscribe(OnStarCollected);
            EventBus<CheckpointActivated>.Unsubscribe(OnCheckpointActivated);
        }

        private void OnObjectLaunched(in ObjectLaunched evt) => Burst(launchBurst, evt.Origin, evt.Direction, launchCount, null);

        private void OnGrappleAttached(in GrappleAttached evt) => Burst(anchorSparks, evt.Point, evt.Normal, anchorCount, null);

        private void OnStarCollected(in StarCollected evt) => Burst(collectBits, evt.Position, Vector3.up, collectCount, starColor);

        private void OnCheckpointActivated(in CheckpointActivated evt) =>
            Burst(collectBits, evt.SpawnPosition, Vector3.up, collectCount, checkpointColor);

        /// <summary>
        /// Coloca el emisor, lo orienta hacia <paramref name="direction"/> y emite.
        /// </summary>
        /// <param name="system">Sistema a usar (se ignora si es nulo).</param>
        /// <param name="position">Punto del evento.</param>
        /// <param name="direction">Eje de la ráfaga (dirección de salida o normal).</param>
        /// <param name="count">Partículas en PC.</param>
        /// <param name="tint">Color inicial opcional; sin valor se conserva el del sistema.</param>
        private void Burst(ParticleSystem system, Vector3 position, Vector3 direction, int count, Color? tint)
        {
            if (system == null) return;
            Transform t = system.transform;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
            t.SetPositionAndRotation(position, rotation);

            if (tint.HasValue)
            {
                // El color inicial se fija en el módulo principal antes de emitir; solo afecta a
                // las partículas nuevas (las existentes conservan el suyo).
                ParticleSystem.MainModule main = system.main;
                main.startColor = tint.Value;
            }

            system.Emit(Mathf.Max(1, Mathf.RoundToInt(count * burstScale)));
        }
    }
}

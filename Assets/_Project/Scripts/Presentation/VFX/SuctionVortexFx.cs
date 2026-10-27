// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: SuctionVortexFx.cs
// Propósito: vórtice de partículas de la aspiradora, encendido y apagado por el
// evento SuctionStateChanged (TDD §8.2, Pilar 2; matriz de integración §8.6).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using UnityEngine;

namespace NostalgIA.Presentation.VFX
{
    /// <summary>
    /// Controla el sistema de partículas del vórtice de succión, montado en la boquilla.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El sistema vive permanentemente en la herramienta (pool de tamaño 1 según el TDD §8.2):
    /// nunca se instancia ni se destruye; solo se reproduce y se detiene. Las partículas nacen
    /// dentro de un cono orientado hacia delante y viajan hacia su vértice (velocidad inicial
    /// negativa) con velocidad orbital alrededor del eje, lo que dibuja la espiral de aire que
    /// converge en la boquilla.
    /// </para>
    /// <para>
    /// En móvil se multiplica la tasa de emisión por <see cref="mobileEmissionMultiplier"/>
    /// (0,4 en el TDD §8.2) una sola vez al iniciar, para respetar el presupuesto de 60
    /// partículas simultáneas.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class SuctionVortexFx : MonoBehaviour
    {
        [Tooltip("Multiplicador de emisión en plataformas móviles (TDD §8.2: 0,4).")]
        [SerializeField, Range(0.1f, 1f)] private float mobileEmissionMultiplier = 0.4f;

        /// <summary>Sistema del vórtice.</summary>
        private ParticleSystem vortex;

        /// <summary>Cachea el sistema y aplica el presupuesto de plataforma.</summary>
        private void Awake()
        {
            vortex = GetComponent<ParticleSystem>();
            if (Application.isMobilePlatform)
            {
                // Los módulos de ParticleSystem son structs que envuelven al sistema nativo:
                // asignar sus propiedades escribe directamente en el sistema.
                ParticleSystem.EmissionModule emission = vortex.emission;
                emission.rateOverTimeMultiplier *= mobileEmissionMultiplier;
            }
            vortex.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>Suscripción al evento de succión.</summary>
        private void OnEnable() => EventBus<SuctionStateChanged>.Subscribe(OnSuctionStateChanged);

        /// <summary>Cancela la suscripción y detiene la emisión.</summary>
        private void OnDisable()
        {
            EventBus<SuctionStateChanged>.Unsubscribe(OnSuctionStateChanged);
            if (vortex != null) vortex.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>
        /// Enciende o apaga la emisión. Al detenerse se deja terminar a las partículas vivas
        /// (StopEmitting) para que la espiral se disipe en lugar de desaparecer de golpe.
        /// </summary>
        private void OnSuctionStateChanged(in SuctionStateChanged evt)
        {
            if (evt.Active)
            {
                if (!vortex.isEmitting) vortex.Play(true);
            }
            else
            {
                vortex.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}

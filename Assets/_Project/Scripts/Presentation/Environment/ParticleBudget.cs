// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: ParticleBudget.cs
// Propósito: reduce emisión y tope de partículas decorativas en móvil
// (multiplicador 0,4).
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Presentation.Environment
{
    /// <summary>Aplica el presupuesto móvil a un sistema de partículas ambiental, una sola vez.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ParticleBudget : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 1f)] private float mobileMultiplier = 0.4f;

        private void Awake()
        {
            if (!Application.isMobilePlatform) return;
            var system = GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(main.maxParticles * mobileMultiplier));
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTimeMultiplier *= mobileMultiplier;
        }
    }
}

// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: BubbleWobble.cs
// Propósito: deformación elástica de una burbuja trampolín al rebotar el jugador
// (rama experimental Frutiger Aero).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using UnityEngine;

namespace NostalgIA.Presentation.Environment
{
    /// <summary>
    /// Aplasta y estira la burbuja con un resorte amortiguado cuando recibe <see cref="BubbleBounced"/>.
    /// </summary>
    /// <remarks>
    /// Deformación con conservación aproximada de volumen: escala vertical (1 − d) y horizontal
    /// 1/√(1 − d), de modo que x·y·z ≈ 1. El valor d sigue un resorte d'' = −k·d − c·d' que
    /// recibe un impulso al rebotar; con c pequeño la burbuja tiembla unas veces y se detiene.
    /// Solo hace trabajo mientras el resorte está activo.
    /// </remarks>
    public sealed class BubbleWobble : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float stiffness = 140f;
        [SerializeField, Min(0f)] private float damping = 6f;
        [SerializeField] private float impulse = 6f;
        [SerializeField, Range(0.05f, 0.6f)] private float maxSquash = 0.35f;

        [Tooltip("Partículas opcionales que se emiten al rebotar.")]
        [SerializeField] private ParticleSystem popParticles;
        [SerializeField, Min(1)] private int popCount = 20;

        private Vector3 baseScale;
        private float squash;
        private float velocity;
        private bool active;

        private void Awake() => baseScale = transform.localScale;

        private void OnEnable() => EventBus<BubbleBounced>.Subscribe(OnBounced);

        private void OnDisable()
        {
            EventBus<BubbleBounced>.Unsubscribe(OnBounced);
            transform.localScale = baseScale;
            active = false;
        }

        private void OnBounced(in BubbleBounced evt)
        {
            if (evt.Source != transform) return;
            velocity += impulse;
            active = true;
            if (popParticles != null)
            {
                popParticles.transform.position = evt.Position;
                popParticles.Emit(popCount);
            }
        }

        private void Update()
        {
            if (!active) return;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            velocity += (-stiffness * squash - damping * velocity) * dt;
            squash += velocity * dt;
            float d = Mathf.Clamp(squash, -maxSquash, maxSquash);
            float lateral = 1f / Mathf.Sqrt(Mathf.Max(0.2f, 1f - d));
            transform.localScale = new Vector3(baseScale.x * lateral, baseScale.y * (1f - d), baseScale.z * lateral);

            if (Mathf.Abs(squash) < 0.001f && Mathf.Abs(velocity) < 0.01f)
            {
                squash = 0f;
                velocity = 0f;
                transform.localScale = baseScale;
                active = false;
            }
        }
    }
}

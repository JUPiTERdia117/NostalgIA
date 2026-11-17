// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: BreakableBlock.cs
// Propósito: obstáculo que se rompe en fragmentos al golpearlo con un objeto
// disparado.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Damage;
using NostalgIA.Core.Events;
using NostalgIA.Core.Lifecycle;
using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Bloque rompible: al recibir un impacto con fuerza suficiente publica
    /// <see cref="BreakableDestroyed"/> (los fragmentos los emite <c>DebrisBurstEmitter</c> con
    /// el mismo material del bloque) y se desactiva.
    /// </summary>
    /// <remarks>
    /// Con <see cref="hitsToBreak"/> mayor que 1 aguanta varios golpes, para muros más resistentes.
    /// Al reaparecer el jugador vuelve a su lugar, igual que los absorbibles rotos.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BreakableBlock : MonoBehaviour, IImpactReceiver, IResettable
    {
        [Tooltip("Fuerza mínima de un golpe para contar, en newtons.")]
        [SerializeField, Min(0f)] private float minForce = 150f;
        [Tooltip("Golpes necesarios para romperlo.")]
        [SerializeField, Min(1)] private int hitsToBreak = 1;
        [Tooltip("Fragmentos que salen al romperse.")]
        [SerializeField, Min(0)] private int fragments = 10;

        private Renderer blockRenderer;
        private int hits;

        private void Awake()
        {
            blockRenderer = GetComponentInChildren<Renderer>();
            ResettableRegistry.Register(this);
        }

        private void OnDestroy() => ResettableRegistry.Unregister(this);

        /// <inheritdoc />
        public void ReceiveImpact(Vector3 point, float force)
        {
            if (!gameObject.activeSelf || force < minForce) return;
            hits++;
            if (hits < hitsToBreak) return;

            Vector3 size = transform.lossyScale;
            Material material = blockRenderer != null ? blockRenderer.sharedMaterial : null;
            EventBus<BreakableDestroyed>.Raise(new BreakableDestroyed(transform.position, fragments, material,
                Mathf.Max(size.x, Mathf.Max(size.y, size.z))));
            gameObject.SetActive(false);
        }

        /// <inheritdoc />
        public void ResetState()
        {
            hits = 0;
            gameObject.SetActive(true);
        }
    }
}

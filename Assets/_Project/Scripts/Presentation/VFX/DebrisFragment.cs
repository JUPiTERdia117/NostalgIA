// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: DebrisFragment.cs
// Propósito: fragmento físico reutilizable del pool de estallidos (TDD §8.5.1).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Pooling;
using UnityEngine;

namespace NostalgIA.Presentation.VFX
{
    /// <summary>
    /// Fragmento: hereda el material del objeto roto y recibe un impulso al salir del pool.
    /// </summary>
    /// <remarks>
    /// Se asigna <see cref="Renderer.sharedMaterial"/> (no <c>material</c>) para no crear
    /// instancias de material: todos los fragmentos de una caja comparten el material de la caja
    /// y siguen siendo compatibles con el SRP Batcher.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DebrisFragment : MonoBehaviour, IPoolable
    {
        private Rigidbody body;
        private Renderer meshRenderer;
        private Material defaultMaterial;

        /// <summary>Cachea componentes y el material por defecto.</summary>
        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            meshRenderer = GetComponentInChildren<Renderer>();
            if (meshRenderer != null) defaultMaterial = meshRenderer.sharedMaterial;
        }

        /// <summary>Asigna material y aplica impulso y giro.</summary>
        /// <param name="material">Material del objeto roto (o <c>null</c> para el material por defecto).</param>
        /// <param name="impulse">Impulso lineal, en N·s.</param>
        /// <param name="angularVelocity">Velocidad angular inicial, en rad/s.</param>
        public void Launch(Material material, Vector3 impulse, Vector3 angularVelocity)
        {
            if (meshRenderer != null) meshRenderer.sharedMaterial = material != null ? material : defaultMaterial;
            body.AddForce(impulse, ForceMode.Impulse);
            body.angularVelocity = angularVelocity;
        }

        /// <inheritdoc />
        public void OnSpawned()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }

        /// <inheritdoc />
        public void OnDespawned()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}

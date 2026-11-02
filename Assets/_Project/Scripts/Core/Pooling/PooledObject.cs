// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: PooledObject.cs
// Propósito: componente raíz de todo objeto administrado por PoolService.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Core.Pooling
{
    /// <summary>
    /// Raíz de un objeto en pool: conoce su pool de origen y notifica a sus <see cref="IPoolable"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        [Tooltip("Segundos tras los cuales el objeto vuelve solo al pool. Cero lo desactiva (lo devuelve quien lo pidió).")]
        [SerializeField, Min(0f)] private float autoReleaseAfter;

        /// <summary>Componentes <see cref="IPoolable"/> del objeto, capturados una vez.</summary>
        private IPoolable[] poolables;

        /// <summary>Tiempo restante hasta la devolución automática.</summary>
        private float remainingLifetime;

        /// <summary>Indica si el objeto está fuera del pool (en uso).</summary>
        public bool IsSpawned { get; private set; }

        /// <summary>Definición del pool al que pertenece (la asigna <see cref="PoolService"/>).</summary>
        public PoolDefinition Definition { get; private set; }

        /// <summary>Servicio que creó el objeto.</summary>
        internal PoolService Owner { get; private set; }

        /// <summary>Duración de vida automática configurada, en segundos.</summary>
        public float AutoReleaseAfter
        {
            get => autoReleaseAfter;
            set => autoReleaseAfter = Mathf.Max(0f, value);
        }

        /// <summary>Asocia el objeto a su pool. Lo llama <see cref="PoolService"/> al crearlo.</summary>
        internal void Bind(PoolService owner, PoolDefinition definition)
        {
            Owner = owner;
            Definition = definition;
            // GetComponentsInChildren asigna un arreglo: se hace una sola vez al crear el objeto.
            poolables = GetComponentsInChildren<IPoolable>(true);
        }

        /// <summary>Notifica la salida del pool.</summary>
        internal void NotifySpawned()
        {
            IsSpawned = true;
            remainingLifetime = autoReleaseAfter;
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnSpawned();
        }

        /// <summary>Notifica el regreso al pool.</summary>
        internal void NotifyDespawned()
        {
            IsSpawned = false;
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnDespawned();
        }

        /// <summary>Devuelve el objeto a su pool (o lo destruye si no pertenece a ninguno).</summary>
        public void Release()
        {
            if (!IsSpawned) return;
            if (Owner != null) Owner.Release(this);
            else Destroy(gameObject);
        }

        /// <summary>Cuenta regresiva de la devolución automática.</summary>
        private void Update()
        {
            if (autoReleaseAfter <= 0f || !IsSpawned) return;
            remainingLifetime -= Time.deltaTime;
            if (remainingLifetime <= 0f) Release();
        }
    }
}

// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: IPoolable.cs
// Propósito: contrato de los componentes que viven en un pool.
// -----------------------------------------------------------------------------

namespace NostalgIA.Core.Pooling
{
    /// <summary>
    /// Componente que reacciona al ciclo de vida de un objeto en pool.
    /// </summary>
    /// <remarks>
    /// Sustituye a <c>Awake</c>/<c>OnDestroy</c> como puntos de inicialización: un objeto en pool
    /// se crea una sola vez y se reutiliza muchas, por lo que su estado debe restablecerse en
    /// <see cref="OnSpawned"/> y limpiarse en <see cref="OnDespawned"/> (por ejemplo, suscripciones
    /// al bus de eventos, velocidades o temporizadores).
    /// </remarks>
    public interface IPoolable
    {
        /// <summary>El objeto se acaba de tomar del pool y está activo.</summary>
        void OnSpawned();

        /// <summary>El objeto vuelve al pool; se desactivará a continuación.</summary>
        void OnDespawned();
    }
}

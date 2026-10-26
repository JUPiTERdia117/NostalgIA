// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: PoolDefinition.cs
// Propósito: definición de datos de un pool de objetos (TDD §8.5.1, tarea F1-A13).
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Core.Pooling
{
    /// <summary>
    /// Asset que describe un pool: prefab y tamaños inicial y máximo por plataforma.
    /// </summary>
    /// <remarks>
    /// Separamos los tamaños de PC y móvil porque el presupuesto de partículas y fragmentos de
    /// móvil es menor (TDD §8.5.4). El tamaño inicial se instancia durante la carga
    /// (<i>prewarm</i>) para que el primer uso en juego no cree objetos; el máximo acota la
    /// memoria: los objetos devueltos por encima del máximo se destruyen.
    /// </remarks>
    [CreateAssetMenu(menuName = "NostalgIA/Pooling/Pool Definition", fileName = "PoolDefinition")]
    public sealed class PoolDefinition : ScriptableObject
    {
        [Tooltip("Prefab instanciado por el pool. Debe tener el componente PooledObject en su raíz.")]
        [SerializeField] private PooledObject prefab;

        [Tooltip("Objetos creados durante la carga en PC.")]
        [SerializeField, Min(0)] private int initialSizePC = 16;

        [Tooltip("Máximo de objetos conservados en PC.")]
        [SerializeField, Min(1)] private int maxSizePC = 64;

        [Tooltip("Objetos creados durante la carga en móvil.")]
        [SerializeField, Min(0)] private int initialSizeMobile = 8;

        [Tooltip("Máximo de objetos conservados en móvil.")]
        [SerializeField, Min(1)] private int maxSizeMobile = 32;

        /// <summary>Prefab del pool.</summary>
        public PooledObject Prefab => prefab;

        /// <summary>Tamaño inicial para la plataforma actual.</summary>
        public int InitialSize => Application.isMobilePlatform ? initialSizeMobile : initialSizePC;

        /// <summary>Tamaño máximo para la plataforma actual (siempre ≥ tamaño inicial).</summary>
        public int MaxSize => Mathf.Max(InitialSize, Application.isMobilePlatform ? maxSizeMobile : maxSizePC);

        /// <summary>Configura la definición desde código (constructores de editor y pruebas).</summary>
        /// <param name="pooledPrefab">Prefab con <see cref="PooledObject"/>.</param>
        /// <param name="initialPC">Tamaño inicial en PC.</param>
        /// <param name="maxPC">Máximo en PC.</param>
        /// <param name="initialMobile">Tamaño inicial en móvil.</param>
        /// <param name="maxMobile">Máximo en móvil.</param>
        public void Configure(PooledObject pooledPrefab, int initialPC, int maxPC, int initialMobile, int maxMobile)
        {
            prefab = pooledPrefab;
            initialSizePC = Mathf.Max(0, initialPC);
            maxSizePC = Mathf.Max(1, maxPC);
            initialSizeMobile = Mathf.Max(0, initialMobile);
            maxSizeMobile = Mathf.Max(1, maxMobile);
        }
    }
}

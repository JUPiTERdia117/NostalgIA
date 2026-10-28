// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: LevelEntrance.cs
// Propósito: punto de llegada de un nivel cuando se entra por una puerta de nivel.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Marco de llegada de un nivel. La puerta del nivel anterior se enlaza con él: lo que el
    /// jugador ve a través de la puerta es lo que hay delante de este marco, y al cruzar aparece
    /// aquí con la misma posición relativa, orientación y velocidad.
    /// </summary>
    /// <remarks>
    /// Mismo convenio que <see cref="NostalgIA.Core.Levels.PortalMath"/>: pivote en el suelo, en el
    /// centro de la abertura, y eje forward hacia el interior del nivel. Tras cruzar, en este
    /// lugar aparece por unos instantes la abertura de regreso, que muestra el nivel anterior
    /// mientras se disuelve.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LevelEntrance : MonoBehaviour
    {
        [Tooltip("Identificador opcional; la puerta busca la entrada con el mismo identificador. Vacío = primera entrada de la escena.")]
        [SerializeField] private string id = string.Empty;
        [Tooltip("Distancia hacia el interior del nivel donde se fija el primer punto de reaparición, en m.")]
        [SerializeField, Min(0f)] private float spawnForwardOffset = 2f;
        [Tooltip("Tamaño de la abertura dibujado en la vista de escena (ancho × alto, m). Solo informativo.")]
        [SerializeField] private Vector2 gizmoAperture = new Vector2(3.2f, 4.4f);

        /// <summary>Identificador de la entrada.</summary>
        public string Id => id;

        /// <summary>Primer punto de reaparición del nivel: unos metros delante del marco.</summary>
        public Vector3 SpawnPosition => transform.position + transform.forward * spawnForwardOffset + Vector3.up * 0.1f;

        /// <summary>Orientación de reaparición: mirando hacia el interior del nivel.</summary>
        public Quaternion SpawnRotation => Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up), Vector3.up);

        /// <summary>Configura la entrada desde un constructor de editor.</summary>
        /// <param name="newId">Identificador.</param>
        /// <param name="aperture">Abertura que se dibuja como referencia.</param>
        public void Configure(string newId, Vector2 aperture)
        {
            id = newId ?? string.Empty;
            gizmoAperture = aperture;
        }

        /// <summary>Dibuja la abertura y la dirección de entrada.</summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.35f, 1f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, gizmoAperture.y * 0.5f, 0f), new Vector3(gizmoAperture.x, gizmoAperture.y, 0.05f));
            Gizmos.DrawLine(new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 2f));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}

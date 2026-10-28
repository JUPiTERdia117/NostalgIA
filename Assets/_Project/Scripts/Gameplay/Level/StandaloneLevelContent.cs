// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: StandaloneLevelContent.cs
// Propósito: activar el jugador y los sistemas propios de un nivel solo cuando el
// nivel se abre por sí mismo (no cuando llega en segundo plano desde una puerta).
// -----------------------------------------------------------------------------

using NostalgIA.Gameplay.Player;
using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Contenedor del contenido que un nivel necesita para jugarse de forma independiente
    /// (jugador, <c>EventSystem</c>). Se guarda <b>inactivo</b> en la escena y este componente,
    /// colocado en un objeto activo, decide en <c>Awake</c> si lo enciende.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cuando una puerta carga el nivel de forma aditiva, Unity ejecuta <c>Awake</c> y
    /// <c>OnEnable</c> de todos los objetos activos antes de que el código de la puerta pueda
    /// intervenir. Si el jugador del nivel estuviera activo, se registrarían dos jugadores en el
    /// <c>ServiceLocator</c> y dos <c>AudioListener</c>. Guardar el grupo inactivo evita por
    /// completo ese costo y esos conflictos: sus <c>Awake</c> no llegan a ejecutarse.
    /// </para>
    /// <para>
    /// La búsqueda de un <see cref="PlayerMotor"/> existente ocurre una sola vez, durante la
    /// inicialización, como permiten las reglas del proyecto.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class StandaloneLevelContent : MonoBehaviour
    {
        [Tooltip("Grupo guardado inactivo con el jugador y los sistemas propios del nivel.")]
        [SerializeField] private GameObject content;

        /// <summary>Grupo que se activa al abrir el nivel por sí mismo.</summary>
        public GameObject Content => content;

        /// <summary>Configura el grupo desde un constructor de editor.</summary>
        /// <param name="group">Grupo con el contenido independiente.</param>
        public void Configure(GameObject group) => content = group;

        /// <summary>Enciende el grupo solo si no hay ya un jugador en otra escena.</summary>
        private void Awake()
        {
            if (content == null || content.activeSelf) return;
            if (FindAnyObjectByType<PlayerMotor>() != null) return;
            content.SetActive(true);
        }
    }
}

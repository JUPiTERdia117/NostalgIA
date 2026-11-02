// -----------------------------------------------------------------------------
// NostalgIA — Entrada (NostalgIA.Input)
// Archivo: TouchControlsVisibility.cs
// Propósito: mostrar los controles táctiles solo con el esquema Táctil y durante
// el juego.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Core.Game;
using NostalgIA.Core.Services;
using UnityEngine;

namespace NostalgIA.Input.TouchControls
{
    /// <summary>
    /// Controla la visibilidad e interacción del lienzo táctil mediante un <see cref="CanvasGroup"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regla: visible ⇔ (esquema = Táctil) ∧ (estado = Playing). Al conectar un mando a un
    /// teléfono el esquema pasa a Gamepad y los controles se ocultan; al tocar la pantalla vuelven.
    /// </para>
    /// <para>
    /// Se ocultan con <c>alpha = 0</c>, <c>interactable = false</c> y
    /// <c>blocksRaycasts = false</c> en lugar de desactivar el GameObject. Desactivarlo destruiría
    /// el mando virtual de los <c>OnScreenControl</c> y el esquema volvería a cambiar al
    /// recrearlo. Ocultar mediante el grupo también evita reconstruir el lienzo.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TouchControlsVisibility : MonoBehaviour
    {
        [Tooltip("Parámetros compartidos (opacidad de los controles).")]
        [SerializeField] private TouchLayoutSettings settings;

        [Tooltip("Muestra los controles en el editor aunque el esquema no sea Táctil (para diseñar la disposición).")]
        [SerializeField] private bool forceVisibleInEditor;

        /// <summary>Grupo que gobierna la visibilidad.</summary>
        private CanvasGroup group;

        /// <summary>Último esquema conocido.</summary>
        private InputScheme scheme;

        /// <summary>Último estado de juego conocido.</summary>
        private GameState state = GameState.Playing;

        /// <summary>Cachea el grupo.</summary>
        private void Awake() => group = GetComponent<CanvasGroup>();

        /// <summary>Lee el estado actual y se suscribe a los cambios.</summary>
        private void OnEnable()
        {
            if (ServiceLocator.TryGet(out InputReader reader)) scheme = reader.CurrentScheme;
            else scheme = Application.isMobilePlatform ? InputScheme.Touch : InputScheme.KeyboardMouse;
            if (ServiceLocator.TryGet(out GameStateMachine machine)) state = machine.State;

            EventBus<InputSchemeChanged>.Subscribe(OnSchemeChanged);
            EventBus<GameStateChanged>.Subscribe(OnStateChanged);
            Refresh();
        }

        /// <summary>Cancela las suscripciones.</summary>
        private void OnDisable()
        {
            EventBus<InputSchemeChanged>.Unsubscribe(OnSchemeChanged);
            EventBus<GameStateChanged>.Unsubscribe(OnStateChanged);
        }

        private void OnSchemeChanged(in InputSchemeChanged evt)
        {
            scheme = evt.Current;
            Refresh();
        }

        private void OnStateChanged(in GameStateChanged evt)
        {
            state = evt.Current;
            Refresh();
        }

        /// <summary>Aplica la regla de visibilidad.</summary>
        private void Refresh()
        {
            bool visible = (scheme == InputScheme.Touch || (forceVisibleInEditor && Application.isEditor))
                           && state == GameState.Playing;
            group.alpha = visible ? (settings != null ? settings.Opacity : 0.45f) : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}

// -----------------------------------------------------------------------------
// NostalgIA — Entrada (NostalgIA.Input)
// Archivo: InputSchemeResolver.cs
// Propósito: detectar el esquema de control en uso y publicar
// InputSchemeChanged (TDD §7.4, tarea F1-A04).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NostalgIA.Input
{
    /// <summary>
    /// Determina el esquema activo a partir del dispositivo que originó la última acción.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Escucha <see cref="InputSystem.onActionChange"/> y, ante cada acción realizada, clasifica el
    /// dispositivo: teclado o ratón → <see cref="InputScheme.KeyboardMouse"/>; mando físico →
    /// <see cref="InputScheme.Gamepad"/>; pantalla táctil o el mando <b>virtual</b> que crean los
    /// controles en pantalla (<c>OnScreenControl</c>) → <see cref="InputScheme.Touch"/>.
    /// </para>
    /// <para>
    /// El mando virtual se distingue del físico con <see cref="InputDevice.native"/>: los
    /// dispositivos que reporta el sistema operativo son nativos y los creados por código no lo son.
    /// Así un mando Bluetooth conectado a un teléfono cambia el esquema a Gamepad y oculta los
    /// controles táctiles, mientras que tocar el pad virtual lo devuelve a Touch.
    /// </para>
    /// <para>
    /// Para ignorar ruido analógico (deriva del stick, gatillos apenas rozados) se exige una
    /// magnitud mínima del control que originó la acción. El callback recibe la acción como
    /// <c>object</c> sin <i>boxing</i> (es un tipo por referencia) y la comparación del
    /// enumerado no asigna memoria.
    /// </para>
    /// </remarks>
    public sealed class InputSchemeResolver
    {
        /// <summary>Magnitud mínima de un control analógico para considerar que el jugador lo usa.</summary>
        private const float MinimumMagnitude = 0.2f;

        /// <summary>Indica si está escuchando al Input System.</summary>
        private bool listening;

        /// <summary>Esquema actual.</summary>
        public InputScheme Current { get; private set; }

        /// <summary>Crea el resolutor con el esquema predeterminado de la plataforma.</summary>
        public InputSchemeResolver()
        {
            Current = Application.isMobilePlatform ? InputScheme.Touch : InputScheme.KeyboardMouse;
        }

        /// <summary>Empieza a escuchar cambios de acciones.</summary>
        public void Enable()
        {
            if (listening) return;
            InputSystem.onActionChange += OnActionChange;
            listening = true;
        }

        /// <summary>Deja de escuchar.</summary>
        public void Disable()
        {
            if (!listening) return;
            InputSystem.onActionChange -= OnActionChange;
            listening = false;
        }

        /// <summary>
        /// Notifica explícitamente el uso de un esquema. La usan los controles táctiles que
        /// inyectan valores directamente en <see cref="InputReader"/> sin pasar por una acción.
        /// </summary>
        /// <param name="scheme">Esquema en uso.</param>
        public void Report(InputScheme scheme)
        {
            if (scheme == Current) return;
            InputScheme previous = Current;
            Current = scheme;
            EventBus<InputSchemeChanged>.Raise(new InputSchemeChanged(previous, scheme));
        }

        /// <summary>Clasifica un dispositivo en un esquema.</summary>
        /// <param name="device">Dispositivo de origen.</param>
        /// <param name="scheme">Esquema resultante.</param>
        /// <returns><c>false</c> si el dispositivo no corresponde a ningún esquema soportado.</returns>
        public static bool TryClassify(InputDevice device, out InputScheme scheme)
        {
            switch (device)
            {
                case Touchscreen _:
                    scheme = InputScheme.Touch;
                    return true;
                case Gamepad _:
                    scheme = device.native ? InputScheme.Gamepad : InputScheme.Touch;
                    return true;
                case Keyboard _:
                case Mouse _:
                    scheme = InputScheme.KeyboardMouse;
                    return true;
                default:
                    scheme = InputScheme.KeyboardMouse;
                    return false;
            }
        }

        /// <summary>Callback global del Input System.</summary>
        private void OnActionChange(object actionOrMap, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed) return;
            if (!(actionOrMap is InputAction action)) return;

            InputControl control = action.activeControl;
            if (control == null) return;
            if (control.EvaluateMagnitude() < MinimumMagnitude) return;

            if (TryClassify(control.device, out InputScheme scheme)) Report(scheme);
        }
    }
}

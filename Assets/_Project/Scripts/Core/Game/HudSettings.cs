// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: HudSettings.cs
// Propósito: ajuste «Mostrar HUD» compartido por el menú principal, la pausa,
// el HUD y los controles táctiles.
// -----------------------------------------------------------------------------

using System;
using UnityEngine;

namespace NostalgIA.Core.Game
{
    /// <summary>
    /// Ajuste «Mostrar HUD», guardado con la misma clave que usa el menú principal («ShowHUD»).
    /// </summary>
    /// <remarks>
    /// Por defecto el HUD se muestra. Al cambiar, avisa con <see cref="Changed"/> para que el HUD
    /// y los controles táctiles se actualicen sin consultar <c>PlayerPrefs</c> cada frame.
    /// </remarks>
    public static class HudSettings
    {
        private const string Key = "ShowHUD";

        /// <summary>Se dispara al cambiar el ajuste, con el valor nuevo.</summary>
        public static event Action<bool> Changed;

        /// <summary>Indica si el HUD se muestra.</summary>
        public static bool Show
        {
            get => PlayerPrefs.GetInt(Key, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                Changed?.Invoke(value);
            }
        }
    }
}

// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: InputGlyphSet.cs
// Propósito: glifos de controles por esquema de entrada (teclado y ratón, mando,
// táctil) para la interfaz.
// -----------------------------------------------------------------------------

using System;
using NostalgIA.Core.Events;
using NostalgIA.Core.Services;
using NostalgIA.Input;
using UnityEngine;

namespace NostalgIA.Presentation.UI
{
    /// <summary>
    /// Tabla de datos con la etiqueta de cada acción en los tres esquemas y su icono táctil.
    /// </summary>
    /// <remarks>
    /// <para>
    /// En este módulo se decidió guardar los glifos en un asset en lugar de escribirlos en cada
    /// pantalla: el menú de pausa, el HUD y las pantallas de ayuda leen la misma fuente, y cambiar
    /// una asignación de botón solo exige editar este asset.
    /// </para>
    /// <para>
    /// La búsqueda es lineal sobre unas diez entradas y se hace al construir los textos (en
    /// <c>Awake</c> de cada presentador), nunca por frame, así que no requiere diccionario.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "NostalgIA/Interfaz/Juego de glifos de entrada", fileName = "InputGlyphSet")]
    public sealed class InputGlyphSet : ScriptableObject
    {
        /// <summary>Glifos de una acción del mapa <c>Gameplay</c>.</summary>
        [Serializable]
        public struct Entry
        {
            [Tooltip("Nombre de la acción (ver InputActionNames).")]
            public string action;

            [Tooltip("Nombre legible de la acción en español.")]
            public string displayName;

            [Tooltip("Etiqueta con teclado y ratón.")]
            public string keyboardMouse;

            [Tooltip("Etiqueta con mando (nomenclatura Xbox).")]
            public string gamepad;

            [Tooltip("Etiqueta con controles táctiles.")]
            public string touch;

            [Tooltip("Icono del botón táctil (opcional).")]
            public Sprite touchIcon;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        /// <summary>Entradas en el orden del asset.</summary>
        public Entry[] Entries => entries;

        /// <summary>Busca la entrada de una acción.</summary>
        /// <param name="action">Nombre de la acción.</param>
        /// <param name="entry">Entrada encontrada.</param>
        /// <returns><c>true</c> si existe.</returns>
        public bool TryGet(string action, out Entry entry)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i].action, action, StringComparison.Ordinal))
                {
                    entry = entries[i];
                    return true;
                }
            }
            entry = default;
            return false;
        }

        /// <summary>Etiqueta de una acción en un esquema; cadena vacía si no existe.</summary>
        public string GetLabel(string action, InputScheme scheme)
        {
            if (!TryGet(action, out Entry entry)) return string.Empty;
            switch (scheme)
            {
                case InputScheme.Gamepad: return entry.gamepad ?? string.Empty;
                case InputScheme.Touch: return entry.touch ?? string.Empty;
                default:
                    // Agacharse y cambiar herramienta se pueden reasignar: se muestra la tecla real.
                    if (InputReader.IsRebindable(action) && ServiceLocator.TryGet(out InputReader reader))
                    {
                        string key = reader.GetKeyLabel(action);
                        if (!string.IsNullOrEmpty(key)) return key;
                    }
                    return entry.keyboardMouse ?? string.Empty;
            }
        }

        /// <summary>Icono táctil de una acción; <c>null</c> si no tiene.</summary>
        public Sprite GetTouchIcon(string action) => TryGet(action, out Entry entry) ? entry.touchIcon : null;
    }
}

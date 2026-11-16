// -----------------------------------------------------------------------------
// NostalgIA — Entrada (NostalgIA.Input)
// Archivo: LookSettings.cs
// Propósito: sensibilidad e inversión de la mirada que elige el jugador en el menú
// principal o en el menú de pausa.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Input
{
    /// <summary>
    /// Ajustes de mirada del jugador, guardados en <c>PlayerPrefs</c> con las mismas claves que usa
    /// el menú principal («XSensitivity», «YSensitivity» e «Inverted»).
    /// </summary>
    /// <remarks>
    /// La sensibilidad va de 1 a 10. El 5 deja la velocidad original del juego, así que el
    /// multiplicador es <c>valor / 5</c>: 10 gira el doble y 1 gira a una quinta parte.
    /// Los valores se leen una sola vez y se guardan en memoria, porque <see cref="InputReader"/>
    /// los consulta en cada frame.
    /// </remarks>
    public static class LookSettings
    {
        public const float Min = 1f;
        public const float Max = 10f;
        public const float Default = 5f;

        private const string XKey = "XSensitivity";
        private const string YKey = "YSensitivity";
        private const string InvertKey = "Inverted";

        private static bool loaded;
        private static float sensitivityX;
        private static float sensitivityY;
        private static bool invert;

        /// <summary>Sensibilidad horizontal (1 a 10).</summary>
        public static float SensitivityX
        {
            get { Load(); return sensitivityX; }
            set { Load(); sensitivityX = Mathf.Clamp(value, Min, Max); PlayerPrefs.SetFloat(XKey, sensitivityX); }
        }

        /// <summary>Sensibilidad vertical (1 a 10).</summary>
        public static float SensitivityY
        {
            get { Load(); return sensitivityY; }
            set { Load(); sensitivityY = Mathf.Clamp(value, Min, Max); PlayerPrefs.SetFloat(YKey, sensitivityY); }
        }

        /// <summary>Invierte el eje vertical de la mirada.</summary>
        public static bool Invert
        {
            get { Load(); return invert; }
            set { Load(); invert = value; PlayerPrefs.SetInt(InvertKey, value ? 1 : 0); }
        }

        /// <summary>Multiplicador por eje que se aplica a la mirada.</summary>
        public static Vector2 Multiplier => new Vector2(SensitivityX, SensitivityY) / Default;

        /// <summary>Lee los valores guardados la primera vez que se piden.</summary>
        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            sensitivityX = Mathf.Clamp(PlayerPrefs.GetFloat(XKey, Default), Min, Max);
            sensitivityY = Mathf.Clamp(PlayerPrefs.GetFloat(YKey, Default), Min, Max);
            invert = PlayerPrefs.GetInt(InvertKey, 0) == 1;
        }
    }
}

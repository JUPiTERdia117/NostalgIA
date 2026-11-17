// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: GameProgress.cs
// Propósito: progreso guardado por nivel (completado, mejor cantidad de estrellas
// y platino).
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Core.Levels
{
    /// <summary>
    /// Progreso del jugador guardado en <c>PlayerPrefs</c>, una entrada por escena de nivel.
    /// </summary>
    /// <remarks>
    /// Un nivel está «platinado» cuando se completó al menos una vez con todas sus estrellas.
    /// Se guarda el total de estrellas junto con el récord para que el menú pueda mostrar
    /// «1/1» sin cargar la escena.
    /// </remarks>
    public static class GameProgress
    {
        private const string Prefix = "NostalgIA.Level.";

        /// <summary>Indica si el nivel se completó alguna vez.</summary>
        public static bool IsCompleted(string scene) => PlayerPrefs.GetInt(Prefix + scene + ".Completed", 0) == 1;

        /// <summary>Mayor cantidad de estrellas conseguidas en una partida del nivel.</summary>
        public static int BestStars(string scene) => PlayerPrefs.GetInt(Prefix + scene + ".Stars", 0);

        /// <summary>Estrellas que tiene el nivel (0 si nunca se completó).</summary>
        public static int TotalStars(string scene) => PlayerPrefs.GetInt(Prefix + scene + ".TotalStars", 0);

        /// <summary>Indica si el nivel se completó con todas sus estrellas.</summary>
        public static bool IsPlatinum(string scene)
        {
            int total = TotalStars(scene);
            return IsCompleted(scene) && total > 0 && BestStars(scene) >= total;
        }

        /// <summary>Registra que se completó el nivel y conserva el mejor resultado.</summary>
        /// <param name="scene">Escena del nivel.</param>
        /// <param name="stars">Estrellas conseguidas en esta partida.</param>
        /// <param name="totalStars">Estrellas que tiene el nivel.</param>
        public static void SaveCompletion(string scene, int stars, int totalStars)
        {
            PlayerPrefs.SetInt(Prefix + scene + ".Completed", 1);
            PlayerPrefs.SetInt(Prefix + scene + ".Stars", Mathf.Max(stars, BestStars(scene)));
            PlayerPrefs.SetInt(Prefix + scene + ".TotalStars", totalStars);
            PlayerPrefs.Save();
        }
    }
}

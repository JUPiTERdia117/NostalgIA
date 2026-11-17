// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: LevelCatalog.cs
// Propósito: lista de niveles jugables, su orden y las mejoras de herramientas
// que tiene el jugador en cada uno.
// -----------------------------------------------------------------------------

namespace NostalgIA.Core.Levels
{
    /// <summary>
    /// Niveles que existen en esta versión del juego, en orden de progreso.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Las mejoras son permanentes porque dependen del nivel: en el Tutorial la aspiradora guarda
    /// 3 objetos y, al completarlo, en Verano ya guarda más y el gancho llega más lejos. Así, entrar
    /// a Verano desde el menú da las mismas herramientas que llegar cruzando la puerta.
    /// </para>
    /// <para>
    /// Una puerta de nivel que apunta a una escena que no está aquí (por ejemplo, Otoño, que es
    /// contenido post lanzamiento) se trata como la meta final del juego.
    /// </para>
    /// </remarks>
    public static class LevelCatalog
    {
        /// <summary>Datos de un nivel.</summary>
        public readonly struct Level
        {
            /// <summary>Nombre de la escena.</summary>
            public readonly string Scene;
            /// <summary>Nombre que ve el jugador.</summary>
            public readonly string Title;
            /// <summary>Objetos que guarda la aspiradora en este nivel.</summary>
            public readonly int VacuumCapacity;
            /// <summary>Multiplicador del alcance base del gancho en este nivel.</summary>
            public readonly float GrappleRangeMultiplier;
            /// <summary>Nivel que hay que completar para desbloquear este (vacío si no hace falta).</summary>
            public readonly string RequiredScene;

            public Level(string scene, string title, int vacuumCapacity, float grappleRangeMultiplier, string requiredScene)
            {
                Scene = scene;
                Title = title;
                VacuumCapacity = vacuumCapacity;
                GrappleRangeMultiplier = grappleRangeMultiplier;
                RequiredScene = requiredScene;
            }
        }

        /// <summary>Niveles en orden de progreso.</summary>
        public static readonly Level[] Levels =
        {
            new Level(SceneNames.Tutorial, "Tutorial", 3, 1f, string.Empty),
            // Mejoras al completar el Tutorial: aspiradora de 3 a 8 objetos y +25 % de alcance del gancho.
            new Level(SceneNames.VeranoFrutiger, "Verano", 8, 1.25f, SceneNames.Tutorial),
        };

        /// <summary>Busca un nivel por el nombre de su escena.</summary>
        public static bool TryGet(string scene, out Level level)
        {
            for (int i = 0; i < Levels.Length; i++)
            {
                if (Levels[i].Scene == scene)
                {
                    level = Levels[i];
                    return true;
                }
            }
            level = default;
            return false;
        }

        /// <summary>Indica si la escena es un nivel jugable de esta versión.</summary>
        public static bool IsPlayable(string scene) => TryGet(scene, out _);

        /// <summary>Indica si el nivel ya se puede elegir (su requisito está completado).</summary>
        public static bool IsUnlocked(string scene) =>
            TryGet(scene, out Level level) &&
            (string.IsNullOrEmpty(level.RequiredScene) || GameProgress.IsCompleted(level.RequiredScene));
    }
}

// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: SceneNames.cs
// Propósito: nombres y rutas de las escenas incluidas en el build.
// -----------------------------------------------------------------------------

namespace NostalgIA.Core
{
    /// <summary>
    /// Nombres de escena usados para cargarlas en tiempo de ejecución y rutas usadas por las
    /// herramientas de editor.
    /// </summary>
    /// <remarks>
    /// <c>SceneManager.LoadSceneAsync</c> identifica la escena por su nombre de archivo sin
    /// extensión. Se centralizan aquí para que renombrar una escena implique cambiar una sola
    /// línea. Las rutas siguen la estructura de carpetas del TDD (§14).
    /// </remarks>
    public static class SceneNames
    {
        /// <summary>Escena de arranque: primera del build; prepara los servicios y carga el juego.</summary>
        public const string Boot = "00_Boot";
        /// <summary>Escena del Tutorial, base de la Estación 1 (Primavera).</summary>
        public const string Tutorial = "Tutorial";
        /// <summary>Dimensión enlazada por el portal no euclidiano; se carga de forma aditiva.</summary>
        public const string OtherDimension = "OtherDimension";
        /// <summary>Nivel 2: océano, islas y lote de edificios; se carga en segundo plano desde el Tutorial.</summary>
        public const string Nivel2 = "20_Nivel2_Islas";
        /// <summary>Nivel 3: laberinto circular y combate contra la IA.</summary>
        public const string Nivel3 = "30_Nivel3_Laberinto";
        /// <summary>Estación final: se llega por el portal que deja la IA al ser destruida.</summary>
        public const string EstacionFinal = "50_Estacion_Final";

        /// <summary>Rutas de las escenas dentro del proyecto (solo para herramientas de editor).</summary>
        public static class Paths
        {
            public const string Boot = "Assets/_Project/Scenes/00_Boot.unity";
            public const string Tutorial = "Assets/_Project/Scenes/Stations/Tutorial.unity";
            public const string OtherDimension = "Assets/_Project/Scenes/Stations/OtherDimension.unity";
            public const string Nivel2 = "Assets/_Project/Scenes/Stations/20_Nivel2_Islas.unity";
            public const string Nivel3 = "Assets/_Project/Scenes/Stations/30_Nivel3_Laberinto.unity";
            public const string EstacionFinal = "Assets/_Project/Scenes/Stations/50_Estacion_Final.unity";
            /// <summary>Carpeta de escenas de prueba que nunca deben incluirse en el build.</summary>
            public const string SandboxFolder = "Assets/_Project/Scenes/Sandbox/";
            /// <summary>Carpeta de escenas de las Estaciones.</summary>
            public const string StationsFolder = "Assets/_Project/Scenes/Stations/";
        }
    }
}

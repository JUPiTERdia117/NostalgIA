// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: WarmupManifest.cs
// Propósito: lista guardada de (malla, material) de un nivel, para precalentar sus
// shaders desde la pantalla de carga de otro nivel sin cargar la escena.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NostalgIA.Presentation.Levels
{
    /// <summary>
    /// Lista de dibujos de un nivel. La genera el editor (menú «NostalgIA → Generar listas de
    /// precalentado», y también sola antes de cada build) en <c>Resources/Warmup/&lt;escena&gt;</c>.
    /// </summary>
    /// <remarks>
    /// La pantalla de carga del Tutorial la usa para compilar desde el principio los shaders de los
    /// niveles siguientes: así, al cruzar la puerta, la GPU ya tiene listos sus programas y la carga
    /// de Verano es más corta y sin tirones. Al terminar se libera la lista (y con ella las texturas
    /// que cargó): lo compilado se queda en la caché del driver.
    /// </remarks>
    public sealed class WarmupManifest : ScriptableObject
    {
        /// <summary>Carpeta dentro de <c>Resources</c> donde viven las listas.</summary>
        public const string ResourcesFolder = "Warmup/";

        /// <summary>Un dibujo: malla, submalla, material y capa del objeto original.</summary>
        [Serializable]
        public struct Entry
        {
            public Mesh Mesh;
            public int Submesh;
            public Material Material;
            public int Layer;
        }

        [Tooltip("Escena de la que salió la lista.")]
        [SerializeField] private string sceneName = string.Empty;
        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>Escena de la que salió la lista.</summary>
        public string SceneName => sceneName;

        /// <summary>Dibujos guardados.</summary>
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>Reemplaza el contenido (solo lo usa el generador del editor).</summary>
        public void Set(string scene, List<Entry> newEntries)
        {
            sceneName = scene;
            entries = newEntries;
        }

        /// <summary>Carga la lista de una escena; nulo si no existe.</summary>
        public static WarmupManifest Load(string scene) => Resources.Load<WarmupManifest>(ResourcesFolder + scene);
    }
}

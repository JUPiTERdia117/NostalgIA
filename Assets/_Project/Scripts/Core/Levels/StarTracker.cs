// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: StarTracker.cs
// Propósito: cuenta las estrellas de cada nivel cargado y las recogidas en la
// partida actual.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.Core.Levels
{
    /// <summary>
    /// Conteo de estrellas por escena cargada: cuántas tiene el nivel y cuántas se recogieron.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La estrella (<c>StarController</c>) informa el total al cargarse su escena, contando también
    /// las que empiezan ocultas (por ejemplo, las que aparecen al vencer enemigos), así que el total
    /// sale de la propia escena y no hay que escribirlo a mano.
    /// </para>
    /// <para>
    /// Se identifica cada escena por su <see cref="Scene.handle"/>, que es distinto en cada carga:
    /// al reiniciar el Tutorial, la escena nueva empieza en cero aunque se llame igual. Al
    /// descargarse una escena se borra su conteo.
    /// </para>
    /// </remarks>
    public static class StarTracker
    {
        private struct Count
        {
            public int Total;
            public int Collected;
        }

        private static readonly Dictionary<int, Count> Counts = new Dictionary<int, Count>();

        /// <summary>Limpia los conteos al iniciar el juego y escucha las descargas de escena.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            Counts.Clear();
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene) => Counts.Remove(scene.handle);

        /// <summary>Fija cuántas estrellas tiene la escena (al cargarse).</summary>
        public static void SetTotal(Scene scene, int total)
        {
            Counts.TryGetValue(scene.handle, out Count count);
            count.Total = total;
            Counts[scene.handle] = count;
        }

        /// <summary>Se recogió una estrella de la escena.</summary>
        public static void Collect(Scene scene)
        {
            Counts.TryGetValue(scene.handle, out Count count);
            count.Collected++;
            Counts[scene.handle] = count;
        }

        /// <summary>Estrellas que tiene el nivel.</summary>
        public static int Total(Scene scene) => Counts.TryGetValue(scene.handle, out Count count) ? count.Total : 0;

        /// <summary>Estrellas recogidas en la partida actual.</summary>
        public static int Collected(Scene scene) => Counts.TryGetValue(scene.handle, out Count count) ? count.Collected : 0;

        /// <summary>Indica si ya se recogieron todas las estrellas del nivel.</summary>
        public static bool AllCollected(Scene scene)
        {
            int total = Total(scene);
            return total > 0 && Collected(scene) >= total;
        }
    }
}

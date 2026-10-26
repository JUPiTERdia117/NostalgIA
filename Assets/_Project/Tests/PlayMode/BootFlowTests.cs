// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de modo de juego (NostalgIA.Tests.PlayMode)
// Archivo: BootFlowTests.cs
// Propósito: verificar de extremo a extremo que la escena de arranque carga el
// Tutorial, como ocurre en el build (tarea F0-A05).
// -----------------------------------------------------------------------------

using System.Collections;
using NostalgIA.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NostalgIA.Tests.PlayMode
{
    /// <summary>
    /// Prueba de integración del flujo de arranque: <c>00_Boot</c> → <c>Tutorial</c>.
    /// </summary>
    public sealed class BootFlowTests
    {
        /// <summary>Tiempo máximo de espera de la carga, en segundos de tiempo real.</summary>
        private const float LoadTimeoutSeconds = 30f;

        /// <summary>
        /// Restablece el manejo de mensajes de consola y descarga el Tutorial, para que su jugador
        /// (que escucha eventos globales como <c>PlayerDied</c>) no interfiera con las pruebas
        /// siguientes.
        /// </summary>
        [UnityTearDown]
        public IEnumerator RestablecerRegistro()
        {
            LogAssert.ignoreFailingMessages = false;
            SceneManager.SetActiveScene(SceneManager.CreateScene("PruebasVacia_" + Time.frameCount));
            Scene tutorial = SceneManager.GetSceneByName(SceneNames.Tutorial);
            if (tutorial.isLoaded) yield return SceneManager.UnloadSceneAsync(tutorial);
            Scene dimension = SceneManager.GetSceneByName(SceneNames.OtherDimension);
            if (dimension.isLoaded) yield return SceneManager.UnloadSceneAsync(dimension);
        }

        /// <summary>
        /// Al cargar la escena de arranque, el Tutorial debe convertirse en la escena activa y la
        /// escena de arranque debe descargarse (carga en modo <c>Single</c>).
        /// </summary>
        [UnityTest]
        public IEnumerator Boot_CargaElTutorialYSeDescarga()
        {
            // El código heredado del Tutorial (enemigos y portal, que se reescriben en la Fase 2)
            // escribe mensajes en consola que no forman parte de lo que esta prueba evalúa.
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene(SceneNames.Boot, LoadSceneMode.Single);

            // Se espera cuadro a cuadro, con límite de tiempo real, a que el Tutorial quede activo.
            float deadline = Time.realtimeSinceStartup + LoadTimeoutSeconds;
            while (SceneManager.GetActiveScene().name != SceneNames.Tutorial && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(SceneNames.Tutorial, SceneManager.GetActiveScene().name,
                $"El Tutorial no quedó activo en {LoadTimeoutSeconds} s.");
            Assert.IsFalse(SceneManager.GetSceneByName(SceneNames.Boot).isLoaded,
                "La escena de arranque debe descargarse al cargar el Tutorial.");
        }
    }
}

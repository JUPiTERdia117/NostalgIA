// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de modo de edición (NostalgIA.Tests.EditMode)
// Archivo: BootSceneLoaderTests.cs
// Propósito: verificar el comportamiento defensivo del cargador de la escena de arranque.
// -----------------------------------------------------------------------------

using System.Text.RegularExpressions;
using NostalgIA.Core;
using NostalgIA.Core.Boot;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NostalgIA.Tests.EditMode
{
    /// <summary>
    /// Pruebas unitarias de <see cref="BootSceneLoader"/>.
    /// </summary>
    /// <remarks>
    /// En modo de edición Unity no invoca <c>Start</c> al agregar el componente, por lo que las
    /// pruebas controlan explícitamente cuándo se pide la carga. La carga exitosa solo es posible en
    /// modo de juego y se cubre en la prueba de PlayMode <c>BootFlowTests</c>.
    /// </remarks>
    public sealed class BootSceneLoaderTests
    {
        private GameObject host;
        private BootSceneLoader loader;

        /// <summary>Crea un objeto temporal con el componente bajo prueba.</summary>
        [SetUp]
        public void CrearCargador()
        {
            host = new GameObject("BootSceneLoader_Prueba");
            loader = host.AddComponent<BootSceneLoader>();
        }

        /// <summary>Destruye el objeto temporal para no dejar residuos en la escena del editor.</summary>
        [TearDown]
        public void DestruirCargador()
        {
            Object.DestroyImmediate(host);
        }

        /// <summary>Asigna el campo serializado privado como lo haría el Inspector.</summary>
        private void SetFirstSceneName(string sceneName)
        {
            var serialized = new SerializedObject(loader);
            serialized.FindProperty("firstSceneName").stringValue = sceneName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Por defecto el cargador apunta al Tutorial y aún no ha iniciado ninguna carga.</summary>
        [Test]
        public void ValoresPorDefecto_ApuntanAlTutorialSinCargaEnCurso()
        {
            Assert.AreEqual(SceneNames.Tutorial, loader.FirstSceneName);
            Assert.IsFalse(loader.IsLoading);
            Assert.AreEqual(0f, loader.Progress01);
        }

        /// <summary>
        /// Si la escena no está en el build, el cargador informa el error y no inicia la carga, en
        /// lugar de dejar el juego detenido en la escena de arranque sin explicación.
        /// </summary>
        [Test]
        public void LoadFirstScene_EscenaFueraDelBuild_RegistraErrorYNoCarga()
        {
            SetFirstSceneName("EscenaQueNoExiste");
            LogAssert.Expect(LogType.Error, new Regex("no está incluida en Build Settings"));

            Assert.IsFalse(loader.LoadFirstScene());
            Assert.IsFalse(loader.IsLoading);
            Assert.AreEqual(0f, loader.Progress01);
        }

        /// <summary>Un nombre vacío se trata igual que una escena inexistente.</summary>
        [Test]
        public void LoadFirstScene_NombreVacio_RegistraErrorYNoCarga()
        {
            SetFirstSceneName(string.Empty);
            LogAssert.Expect(LogType.Error, new Regex("no está incluida en Build Settings"));

            Assert.IsFalse(loader.LoadFirstScene());
            Assert.IsFalse(loader.IsLoading);
        }
    }
}

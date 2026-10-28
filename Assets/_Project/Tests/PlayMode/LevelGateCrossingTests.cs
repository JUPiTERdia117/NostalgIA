// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de modo de juego (NostalgIA.Tests.PlayMode)
// Archivo: LevelGateCrossingTests.cs
// Propósito: verificar el ciclo completo de una puerta de nivel: adopción del
// nivel siguiente, cruce con transferencia de pose, cambio de escena activa y
// descarga del nivel anterior.
// -----------------------------------------------------------------------------

using System.Collections;
using NostalgIA.Core;
using NostalgIA.Core.Events;
using NostalgIA.Core.Levels;
using NostalgIA.Gameplay.Level;
using NostalgIA.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NostalgIA.Tests.PlayMode
{
    /// <summary>Prueba de integración de <see cref="LevelGate"/> con dos escenas creadas en memoria.</summary>
    public sealed class LevelGateCrossingTests
    {
        private Scene source;
        private Scene destination;
        private GameObject player;
        private PlayerMotor motor;
        private Transform exit;
        private int crossings;
        private int closings;

        private void OnCrossed(in LevelGateCrossed evt) => crossings++;
        private void OnClosed(in LevelGateClosed evt) => closings++;

        /// <summary>Escena de origen con puerta y jugador; escena destino con suelo y entrada a 3 km.</summary>
        [UnitySetUp]
        public IEnumerator Montar()
        {
            int id = Random.Range(0, int.MaxValue);
            destination = SceneManager.CreateScene("PruebaNivelDestino_" + id);
            source = SceneManager.CreateScene("PruebaNivelOrigen_" + id);

            SceneManager.SetActiveScene(destination);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetPositionAndRotation(new Vector3(3000f, -0.5f, 0f), Quaternion.identity);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            var entranceObject = new GameObject("Entrada");
            entranceObject.transform.SetPositionAndRotation(new Vector3(3000f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            entranceObject.AddComponent<LevelEntrance>();
            exit = entranceObject.transform;

            SceneManager.SetActiveScene(source);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetPositionAndRotation(new Vector3(0f, -0.5f, 5f), Quaternion.identity);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);

            player = new GameObject("JugadorDePrueba") { layer = Layers.Player };
            player.SetActive(false);
            player.transform.position = new Vector3(0f, 0.05f, 1.5f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            motor = player.AddComponent<PlayerMotor>();
            // La puerta detecta el cruce con la cámara principal; va a la altura de los ojos.
            var eye = new GameObject("CamaraDePrueba") { tag = "MainCamera" };
            eye.transform.SetParent(player.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            eye.AddComponent<Camera>();
            player.SetActive(true);

            var gateObject = new GameObject("Puerta");
            gateObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            gateObject.AddComponent<LevelGate>().Configure(destination.name, string.Empty, new Vector2(3.2f, 4.4f));

            crossings = 0;
            closings = 0;
            EventBus<LevelGateCrossed>.Subscribe(OnCrossed);
            EventBus<LevelGateClosed>.Subscribe(OnClosed);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Desmontar()
        {
            EventBus<LevelGateCrossed>.Unsubscribe(OnCrossed);
            EventBus<LevelGateClosed>.Unsubscribe(OnClosed);
            if (source.IsValid() && source.isLoaded) yield return SceneManager.UnloadSceneAsync(source);
            if (destination.IsValid() && destination.isLoaded) yield return SceneManager.UnloadSceneAsync(destination);
        }

        /// <summary>
        /// Al cruzar, el jugador aparece al otro lado del marco de llegada con la pose transferida,
        /// pasa a la escena nueva (que queda activa) y, tras la disolución, la escena anterior se
        /// descarga.
        /// </summary>
        [UnityTest]
        public IEnumerator Cruce_TrasladaAlJugadorYDescargaElNivelAnterior()
        {
            // La precarga empieza tras la espera configurada (2 s) y el nivel despierta al estar cerca.
            float deadline = Time.realtimeSinceStartup + 6f;
            LevelGate gate = Object.FindAnyObjectByType<LevelGate>();
            while (!gate.IsDestinationAwake && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(gate.IsDestinationAwake, "El nivel siguiente no despertó.");
            Assert.AreEqual(exit, gate.Exit, "La puerta debe enlazarse con la LevelEntrance.");

            // Se registra el lado frontal y después se atraviesa el plano dentro de la abertura.
            motor.Teleport(new Vector3(0.3f, 0.05f, 0.6f), Quaternion.Euler(0f, 180f, 0f));
            yield return null;
            Vector3 beforeCross = new Vector3(0.3f, 0.05f, -0.05f);
            Matrix4x4 expected = PortalMath.Transfer(gate.transform, exit);
            Vector3 expectedPosition = expected.MultiplyPoint3x4(beforeCross);
            motor.Teleport(beforeCross, Quaternion.Euler(0f, 180f, 0f));
            yield return null;

            Assert.AreEqual(1, crossings, "No se detectó el cruce.");
            Assert.Less(Vector3.Distance(player.transform.position, expectedPosition), 0.05f, "Posición transferida incorrecta.");
            Assert.Greater(Vector3.Dot(player.transform.forward, exit.forward), 0.99f, "El jugador debe mirar hacia el interior del nivel nuevo.");
            Assert.AreEqual(destination, player.scene, "El jugador debe pasar a la escena nueva.");
            Assert.AreEqual(destination, SceneManager.GetActiveScene(), "La escena nueva debe quedar activa.");

            deadline = Time.realtimeSinceStartup + gate.FadeDuration + 3f;
            while (source.isLoaded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(1, closings);
            Assert.IsFalse(source.isLoaded, "El nivel anterior debe descargarse tras la disolución.");
        }
    }
}

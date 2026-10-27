// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de modo de juego (NostalgIA.Tests.PlayMode)
// Archivo: BounceBubbleTests.cs
// Propósito: verificar la burbuja trampolín y el impulso externo del motor
// (rama experimental Frutiger Aero).
// -----------------------------------------------------------------------------

using System.Collections;
using NostalgIA.Core;
using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Level;
using NostalgIA.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NostalgIA.Tests.PlayMode
{
    /// <summary>Pruebas de integración de <see cref="BounceBubble"/> y <see cref="PlayerMotor.Launch"/>.</summary>
    public sealed class BounceBubbleTests
    {
        private GameObject player;
        private PlayerMotor motor;
        private GameObject bubble;
        private int bounces;
        private float launchSpeed;

        private void OnBounced(in BubbleBounced evt)
        {
            bounces++;
            launchSpeed = evt.LaunchSpeed;
        }

        /// <summary>Jugador mínimo en la capa Player, lejos de cualquier geometría.</summary>
        [UnitySetUp]
        public IEnumerator Montar()
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("PruebasBurbuja_" + Random.Range(0, int.MaxValue)));
            player = new GameObject("JugadorDePrueba") { layer = Layers.Player };
            player.SetActive(false);
            player.transform.position = new Vector3(0f, 300f, 0f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            motor = player.AddComponent<PlayerMotor>();
            player.SetActive(true);
            bounces = 0;
            EventBus<BubbleBounced>.Subscribe(OnBounced);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Desmontar()
        {
            EventBus<BubbleBounced>.Unsubscribe(OnBounced);
            Object.Destroy(player);
            if (bubble != null) Object.Destroy(bubble);
            yield return null;
        }

        /// <summary>Launch fija la velocidad vertical mínima y deja al jugador en el aire.</summary>
        [UnityTest]
        public IEnumerator Launch_ImpulsaHaciaArribaYPasaAlAire()
        {
            motor.Launch(12f, new Vector3(3f, 0f, 0f));
            Assert.AreEqual(LocomotionState.Airborne, motor.State);
            Assert.AreEqual(12f, motor.RawVelocity.y, 1e-4f);
            Assert.GreaterOrEqual(motor.RawVelocity.x, 3f - 1e-4f);
            yield return null;
            Assert.Greater(motor.RawVelocity.y, 0f, "Tras un frame el jugador sigue subiendo.");
        }

        /// <summary>
        /// Al caer sobre la burbuja, el jugador sale lanzado con v = √(2·g·h) y se publica
        /// BubbleBounced una sola vez.
        /// </summary>
        [UnityTest]
        public IEnumerator Burbuja_LanzaAlJugadorConLaAlturaConfigurada()
        {
            const float height = 8f;
            bubble = new GameObject("BurbujaDePrueba");
            bubble.transform.position = player.transform.position + Vector3.down * 2f;
            bubble.AddComponent<SphereCollider>().radius = 1f;
            BounceBubble bounce = bubble.AddComponent<BounceBubble>();
            bounce.Configure(height, 0f);

            float deadline = Time.realtimeSinceStartup + 3f;
            while (bounces == 0 && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.AreEqual(1, bounces, "El jugador no rebotó en la burbuja.");
            float expected = Mathf.Sqrt(2f * motor.Settings.gravity * height);
            Assert.AreEqual(expected, launchSpeed, 1e-3f);
            Assert.Greater(motor.RawVelocity.y, 0f, "Tras el rebote el jugador debe subir.");
        }
    }
}

// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de modo de juego (NostalgIA.Tests.PlayMode)
// Archivo: PlayerSystemsTests.cs
// Propósito: verificar vida, reaparición sin recarga y conservación de impulso
// aéreo del jugador (tareas F1-A06, A11 y A12; criterios de aceptación §4 c).
// -----------------------------------------------------------------------------

using System.Collections;
using NostalgIA.Core.Damage;
using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NostalgIA.Tests.PlayMode
{
    /// <summary>Pruebas de integración del jugador sobre un montaje mínimo creado por código.</summary>
    public sealed class PlayerSystemsTests
    {
        private GameObject player;
        private PlayerMotor motor;
        private PlayerHealth health;
        private RespawnService respawn;

        private int diedCount;
        private int respawnedCount;
        private float diedAt;
        private float respawnedAt;

        private void OnDied(in PlayerDied evt)
        {
            diedCount++;
            diedAt = Time.realtimeSinceStartup;
        }

        private void OnRespawned(in PlayerRespawned evt)
        {
            respawnedCount++;
            respawnedAt = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Crea un jugador mínimo (cápsula, motor, vida y reaparición) en una escena vacía nueva,
        /// lejos de cualquier geometría.
        /// </summary>
        [UnitySetUp]
        public IEnumerator CrearJugador()
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("PruebasJugador_" + Random.Range(0, int.MaxValue)));
            player = new GameObject("JugadorDePrueba");
            player.SetActive(false);
            player.transform.position = new Vector3(0f, 500f, 0f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            motor = player.AddComponent<PlayerMotor>();
            health = player.AddComponent<PlayerHealth>();
            respawn = player.AddComponent<RespawnService>();
            player.SetActive(true);

            diedCount = respawnedCount = 0;
            EventBus<PlayerDied>.Subscribe(OnDied);
            EventBus<PlayerRespawned>.Subscribe(OnRespawned);
            yield return null;
        }

        /// <summary>Destruye el montaje.</summary>
        [UnityTearDown]
        public IEnumerator DestruirJugador()
        {
            EventBus<PlayerDied>.Unsubscribe(OnDied);
            EventBus<PlayerRespawned>.Unsubscribe(OnRespawned);
            Object.Destroy(player);
            yield return null;
        }

        /// <summary>El daño normal resta corazones y la invulnerabilidad bloquea el siguiente golpe.</summary>
        [UnityTest]
        public IEnumerator Vida_DanoNormalEInvulnerabilidad()
        {
            int max = health.MaxHearts;
            health.TakeDamage(DamageInfo.Normal(1, Vector3.zero));
            Assert.AreEqual(max - 1, health.CurrentHearts);

            health.TakeDamage(DamageInfo.Normal(1, Vector3.zero));
            Assert.AreEqual(max - 1, health.CurrentHearts, "Durante la invulnerabilidad no se recibe daño normal.");
            yield return null;
        }

        /// <summary>La muerte instantánea ignora la invulnerabilidad y publica PlayerDied una sola vez.</summary>
        [UnityTest]
        public IEnumerator Vida_InstakillIgnoraInvulnerabilidad()
        {
            health.TakeDamage(DamageInfo.Normal(1, Vector3.zero));
            health.TakeDamage(DamageInfo.Instakill(Vector3.zero));
            health.TakeDamage(DamageInfo.Instakill(Vector3.zero));
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(1, diedCount);
            yield return null;
        }

        /// <summary>
        /// Criterio de aceptación: al morir, el jugador reaparece en el último checkpoint en
        /// menos de 1 s, con los corazones completos y sin recargar la escena.
        /// </summary>
        [UnityTest]
        public IEnumerator Reaparicion_EnElUltimoCheckpointEnMenosDeUnSegundo()
        {
            var checkpointGo = new GameObject("CheckpointDePrueba");
            checkpointGo.transform.position = new Vector3(30f, 400f, -12f);
            checkpointGo.AddComponent<BoxCollider>().isTrigger = true;
            var checkpoint = checkpointGo.AddComponent<Checkpoint>();
            checkpoint.Configure(3, "Prueba");
            checkpoint.Activate();

            Scene sceneBefore = player.scene;
            health.Kill(DeathCause.Void);

            float deadline = Time.realtimeSinceStartup + 2f;
            while (respawnedCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.AreEqual(1, respawnedCount, "No se publicó PlayerRespawned.");
            Assert.LessOrEqual(respawnedAt - diedAt, 1f, "La reaparición debe tardar como máximo 1 s.");
            Assert.IsFalse(health.IsDead);
            Assert.AreEqual(health.MaxHearts, health.CurrentHearts);
            Assert.AreEqual(3, respawn.SpawnCheckpointIndex);
            Assert.Less(Vector3.Distance(checkpoint.SpawnPosition, player.transform.position), 0.5f);
            Assert.IsTrue(sceneBefore.IsValid() && sceneBefore.isLoaded, "La escena no debe recargarse.");
            Object.Destroy(checkpointGo);
        }

        /// <summary>
        /// Conservación de impulso aéreo (TDD §5.1): sin entrada, la velocidad horizontal en el aire
        /// no decae hacia la velocidad de caminata como en el controlador original.
        /// </summary>
        [UnityTest]
        public IEnumerator Motor_ConservaLaVelocidadHorizontalEnElAire()
        {
            // El jugador está a 500 m sin suelo: tras el primer frame queda en el estado aéreo.
            yield return null;
            Assert.AreEqual(LocomotionState.Airborne, motor.State);

            float speed = motor.Settings.sprintSpeed;
            motor.SetVelocity(new Vector3(speed, 0f, 0f));
            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(LocomotionState.Airborne, motor.State);
            Assert.GreaterOrEqual(motor.HorizontalSpeed, speed - 0.05f,
                "La velocidad horizontal en el aire debe conservarse (el original la reducía a la de caminata).");
        }
    }
}

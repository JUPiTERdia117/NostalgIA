// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de modo de juego (NostalgIA.Tests.PlayMode)
// Archivo: PresentationFeedbackTests.cs
// Propósito: verificar la respuesta de la capa de presentación a eventos:
// postprocesado de daño y muerte, retícula fuera de alcance y capa de pausa.
// -----------------------------------------------------------------------------

using System.Collections;
using System.Reflection;
using NostalgIA.Core.Damage;
using NostalgIA.Core.Events;
using NostalgIA.Presentation.CameraFX;
using NostalgIA.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NostalgIA.Tests.PlayMode
{
    /// <summary>Pruebas de los presentadores visuales sobre montajes mínimos creados por código.</summary>
    public sealed class PresentationFeedbackTests
    {
        private static readonly int DamagePulseId = Shader.PropertyToID("_NOS_DamagePulse");
        private GameObject host;

        [TearDown]
        public void Limpiar()
        {
            if (host != null) Object.Destroy(host);
            Shader.SetGlobalFloat(DamagePulseId, 0f);
        }

        /// <summary>Asigna un campo serializado privado (las pruebas de juego no tienen SerializedObject).</summary>
        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        /// <summary>Crea un PostFXController con su Volume, inactivo hasta configurarlo.</summary>
        private (PostFXController controller, Volume volume) CreatePostFx()
        {
            host = new GameObject("PostFXDePrueba");
            host.SetActive(false);
            Volume volume = host.AddComponent<Volume>();
            volume.isGlobal = true;
            PostFXController controller = host.AddComponent<PostFXController>();
            SetField(controller, "feedbackVolume", volume);
            host.SetActive(true);
            return (controller, volume);
        }

        /// <summary>
        /// Un golpe lleva el peso a 1 (y la variable global de shader) y en 0,35 s vuelve a 0,
        /// desactivando el Volume para que URP no lo evalúe.
        /// </summary>
        [UnityTest]
        public IEnumerator PostFX_PulsoDeDanoSubeYSeApaga()
        {
            (PostFXController controller, Volume volume) = CreatePostFx();
            Assert.IsFalse(volume.enabled, "Sin daño, el Volume debe estar desactivado.");

            EventBus<PlayerDamaged>.Raise(new PlayerDamaged(1, 2, 3, Vector3.zero));
            Assert.AreEqual(1f, controller.CurrentWeight, 1e-4f);
            Assert.AreEqual(1f, volume.weight, 1e-4f);
            Assert.IsTrue(volume.enabled);
            Assert.AreEqual(1f, Shader.GetGlobalFloat(DamagePulseId), 1e-4f);

            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(0f, controller.CurrentWeight, 1e-4f);
            Assert.IsFalse(volume.enabled);
            Assert.AreEqual(0f, Shader.GetGlobalFloat(DamagePulseId), 1e-4f);
        }

        /// <summary>La muerte mantiene el efecto hasta reaparecer; después se desvanece.</summary>
        [UnityTest]
        public IEnumerator PostFX_MuerteSeMantieneHastaReaparecer()
        {
            (PostFXController controller, Volume _) = CreatePostFx();

            EventBus<PlayerDied>.Raise(new PlayerDied(default(DeathCause), Vector3.zero));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(1f, controller.CurrentWeight, 1e-4f, "El efecto de muerte debe mantenerse.");

            EventBus<PlayerDamaged>.Raise(new PlayerDamaged(1, 0, 3, Vector3.zero));
            Assert.AreEqual(1f, controller.CurrentWeight, 1e-4f, "Un golpe no debe interrumpir el efecto de muerte.");

            EventBus<PlayerRespawned>.Raise(new PlayerRespawned(0, Vector3.zero, 3));
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(0f, controller.CurrentWeight, 1e-4f);
        }

        /// <summary>Fuera de alcance, la retícula gira 45° (cruz «×») y se reduce (Guía de estilo §7).</summary>
        [UnityTest]
        public IEnumerator Reticula_FueraDeAlcanceMuestraCruz()
        {
            host = new GameObject("ReticulaDePrueba", typeof(RectTransform));
            host.SetActive(false);
            var root = new GameObject("Raiz", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(host.transform, false);
            Image mark = new GameObject("Marca", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            mark.transform.SetParent(root, false);
            ReticlePresenter reticle = host.AddComponent<ReticlePresenter>();
            SetField(reticle, "marks", new Graphic[] { mark });
            SetField(reticle, "scaledRoot", root);
            host.SetActive(true);
            yield return null;

            EventBus<AimTargetChanged>.Raise(new AimTargetChanged(AimTargetKind.None, AimTargetKind.OutOfRange));
            Assert.AreEqual(45f, root.localEulerAngles.z, 0.01f);
            Assert.Less(root.localScale.x, 1f);

            EventBus<AimTargetChanged>.Raise(new AimTargetChanged(AimTargetKind.OutOfRange, AimTargetKind.Anchor));
            Assert.AreEqual(0f, root.localEulerAngles.z, 0.01f, "Con objetivo válido la retícula vuelve a «+».");
            Assert.Greater(root.localScale.x, 1f);
        }

        /// <summary>La capa de pausa solo se dibuja en el estado Paused.</summary>
        [UnityTest]
        public IEnumerator Pausa_CapaVisibleSoloEnPausa()
        {
            host = new GameObject("PausaDePrueba");
            host.SetActive(false);
            Canvas canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            host.AddComponent<PauseOverlayPresenter>();
            host.SetActive(true);
            yield return null;
            Assert.IsFalse(canvas.enabled);

            EventBus<GameStateChanged>.Raise(new GameStateChanged(GameState.Playing, GameState.Paused));
            Assert.IsTrue(canvas.enabled);

            EventBus<GameStateChanged>.Raise(new GameStateChanged(GameState.Paused, GameState.Playing));
            Assert.IsFalse(canvas.enabled);
        }
    }
}

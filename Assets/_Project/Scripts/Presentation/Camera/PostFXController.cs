// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: PostFXController.cs
// Propósito: retroalimentación de postprocesado ante daño y muerte mediante el
// peso de un Volume global de alta prioridad (TDD §8.4, perfil VP_Feedback).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using UnityEngine;
using UnityEngine.Rendering;

namespace NostalgIA.Presentation.CameraFX
{
    /// <summary>
    /// Anima el peso del <see cref="Volume"/> de retroalimentación (viñeta roja, aberración
    /// cromática y desaturación) y publica el mismo valor como variable global de shader
    /// <c>_NOS_DamagePulse</c>, que los materiales del entorno usan para teñir su emisión.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué el peso y no los parámetros.</b> En este módulo se decidió no modificar las
    /// intensidades de los efectos en tiempo de ejecución: el perfil <c>VP_Feedback</c> guarda
    /// el aspecto final del golpe (lo ajusta Arte en el inspector) y el código solo mueve
    /// <see cref="Volume.weight"/> entre 0 y 1. URP interpola cada parámetro entre el perfil base
    /// de la Estación (prioridad 0) y este perfil (prioridad 100) con ese peso, así que no se
    /// instancian perfiles ni componentes (0 B de basura).
    /// </para>
    /// <para>Curvas del peso <c>w</c>:</para>
    /// <list type="bullet">
    /// <item><b>Daño</b>: <c>w(t) = (1 − t/T)²</c> con <c>T = 0,35 s</c>; sube a 1 de golpe y cae
    /// con salida cuadrática, más rápida al principio, que se percibe como impacto.</item>
    /// <item><b>Muerte</b>: <c>w</c> sube a 1 y se mantiene hasta <see cref="PlayerRespawned"/>.</item>
    /// <item><b>Reaparición</b>: desvanecimiento lineal a 0 en <see cref="respawnFadeDuration"/>.</item>
    /// </list>
    /// <para>
    /// Cuando <c>w = 0</c> el Volume se desactiva para que URP no lo evalúe en cada frame, y la
    /// variable global solo se escribe mientras hay animación. Se usa tiempo sin escalar para
    /// que la pausa no congele un destello a medias.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PostFXController : MonoBehaviour
    {
        [Tooltip("Volume global de retroalimentación (prioridad alta, perfil VP_Feedback).")]
        [SerializeField] private Volume feedbackVolume;

        [Tooltip("Duración del pulso de daño (TDD §8.4: 0,35 s).")]
        [SerializeField, Min(0.05f)] private float damagePulseDuration = 0.35f;

        [Tooltip("Duración del desvanecimiento tras reaparecer.")]
        [SerializeField, Min(0.05f)] private float respawnFadeDuration = 0.5f;

        /// <summary>Identificador cacheado de la variable global (evita buscar la cadena cada frame).</summary>
        private static readonly int DamagePulseId = Shader.PropertyToID("_NOS_DamagePulse");

        /// <summary>Modo de la animación en curso.</summary>
        private enum Mode { Idle, DamagePulse, DeathHold, RespawnFade }

        private Mode mode = Mode.Idle;
        private float elapsed;
        private float fadeStartWeight;

        /// <summary>Peso aplicado actualmente (también expuesto para las pruebas).</summary>
        public float CurrentWeight { get; private set; }

        /// <summary>Estado inicial: sin efecto.</summary>
        private void Awake() => ApplyWeight(0f);

        /// <summary>Suscripciones.</summary>
        private void OnEnable()
        {
            EventBus<PlayerDamaged>.Subscribe(OnPlayerDamaged);
            EventBus<PlayerDied>.Subscribe(OnPlayerDied);
            EventBus<PlayerRespawned>.Subscribe(OnPlayerRespawned);
        }

        /// <summary>Cancela suscripciones y limpia la variable global para no dejar la escena teñida.</summary>
        private void OnDisable()
        {
            EventBus<PlayerDamaged>.Unsubscribe(OnPlayerDamaged);
            EventBus<PlayerDied>.Unsubscribe(OnPlayerDied);
            EventBus<PlayerRespawned>.Unsubscribe(OnPlayerRespawned);
            mode = Mode.Idle;
            ApplyWeight(0f);
        }

        private void OnPlayerDamaged(in PlayerDamaged evt)
        {
            if (mode == Mode.DeathHold) return;   // La muerte tiene prioridad visual sobre un golpe.
            mode = Mode.DamagePulse;
            elapsed = 0f;
            ApplyWeight(1f);
        }

        private void OnPlayerDied(in PlayerDied evt)
        {
            mode = Mode.DeathHold;
            ApplyWeight(1f);
        }

        private void OnPlayerRespawned(in PlayerRespawned evt)
        {
            mode = Mode.RespawnFade;
            elapsed = 0f;
            fadeStartWeight = CurrentWeight;
        }

        /// <summary>Avanza la curva activa.</summary>
        private void Update()
        {
            if (mode == Mode.Idle || mode == Mode.DeathHold) return;
            elapsed += Time.unscaledDeltaTime;

            if (mode == Mode.DamagePulse)
            {
                float remaining = 1f - Mathf.Clamp01(elapsed / damagePulseDuration); // 1 → 0
                ApplyWeight(remaining * remaining);                                   // (1 − t/T)²
                if (remaining <= 0f) mode = Mode.Idle;
            }
            else // RespawnFade
            {
                float p = Mathf.Clamp01(elapsed / respawnFadeDuration);
                ApplyWeight(Mathf.Lerp(fadeStartWeight, 0f, p));
                if (p >= 1f) mode = Mode.Idle;
            }
        }

        /// <summary>Escribe el peso en el Volume y en la variable global de shader.</summary>
        private void ApplyWeight(float weight)
        {
            CurrentWeight = weight;
            Shader.SetGlobalFloat(DamagePulseId, weight);
            if (feedbackVolume == null) return;
            feedbackVolume.weight = weight;
            bool active = weight > 0.0001f;
            if (feedbackVolume.enabled != active) feedbackVolume.enabled = active;
        }
    }
}

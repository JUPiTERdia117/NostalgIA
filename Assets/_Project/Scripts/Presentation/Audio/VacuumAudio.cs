// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: VacuumAudio.cs
// Propósito: sonido de la aspiradora en tres partes (arranque, bucle y apagado),
// encendido y apagado por el evento SuctionStateChanged.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using UnityEngine;

namespace NostalgIA.Presentation.Audio
{
    /// <summary>
    /// Reproduce el sonido de la aspiradora: <c>Inicio</c> al activarla, <c>Loop</c> mientras sigue
    /// activa y <c>Fin</c> al soltarla.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uso una fuente de audio por clip. El bucle se programa con <c>PlayScheduled</c> justo en el
    /// instante en que termina el arranque, así no hay hueco ni salto entre los dos.
    /// </para>
    /// <para>
    /// Si se suelta durante el arranque, el apagado no empieza desde el principio (que suena a motor
    /// a toda potencia) sino desde un punto con volumen parecido: el arranque sube y el apagado baja,
    /// así que uso la posición «espejo». Lo mismo al revés si se vuelve a activar mientras se apaga.
    /// Las fuentes que dejan de sonar se desvanecen en unos milisegundos para que no truene el corte.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class VacuumAudio : MonoBehaviour
    {
        [Header("Clips")]
        [Tooltip("Arranque del motor; al terminar sigue el bucle.")]
        [SerializeField] private AudioClip startClip;
        [Tooltip("Motor encendido; se repite mientras la aspiradora siga activa.")]
        [SerializeField] private AudioClip loopClip;
        [Tooltip("Apagado del motor.")]
        [SerializeField] private AudioClip endClip;

        [Header("Mezcla")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [Tooltip("Segundos que tarda en desvanecerse un clip que se corta.")]
        [SerializeField, Range(0.01f, 0.3f)] private float fadeTime = 0.06f;

        /// <summary>Pequeño margen para programar los clips en el reloj de audio.</summary>
        private const double ScheduleDelay = 0.02;

        private AudioSource start, loop, end;

        /// <summary>Crea las tres fuentes (2D: el sonido es del jugador).</summary>
        private void Awake()
        {
            start = CreateSource(startClip, false);
            loop = CreateSource(loopClip, true);
            end = CreateSource(endClip, false);
        }

        private AudioSource CreateSource(AudioClip clip, bool looping)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = looping;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.outputAudioMixerGroup = GameAudio.Sfx; // lo controla el deslizador de efectos
            return source;
        }

        private void OnEnable() => EventBus<SuctionStateChanged>.Subscribe(OnSuctionStateChanged);

        private void OnDisable()
        {
            EventBus<SuctionStateChanged>.Unsubscribe(OnSuctionStateChanged);
            start.Stop();
            loop.Stop();
            end.Stop();
        }

        private void OnSuctionStateChanged(in SuctionStateChanged evt)
        {
            if (startClip == null || loopClip == null || endClip == null) return;
            if (evt.Active) TurnOn();
            else TurnOff();
        }

        /// <summary>Arranque y, justo al acabar, el bucle.</summary>
        private void TurnOn()
        {
            // Si se estaba apagando, retomo el arranque desde el punto con volumen parecido.
            float offset = IsSounding(end) ? MirrorTime(end, startClip) : 0f;

            FadeOut(end);
            double now = AudioSettings.dspTime + ScheduleDelay;
            Play(start, offset, now);
            Play(loop, 0f, now + (startClip.length - offset));
        }

        /// <summary>Apagado; los demás clips se desvanecen.</summary>
        private void TurnOff()
        {
            // Si aún iba en el arranque, el apagado empieza desde el punto con volumen parecido.
            float offset = IsSounding(start) && start.time > 0f ? MirrorTime(start, endClip) : 0f;

            FadeOut(start);
            FadeOut(loop);
            Play(end, offset, AudioSettings.dspTime + ScheduleDelay);
        }

        /// <summary>Posición equivalente en el otro clip: si iba al 30 % de uno, empiezo al 70 % del otro.</summary>
        private static float MirrorTime(AudioSource current, AudioClip other)
        {
            float progress = Mathf.Clamp01(current.time / current.clip.length);
            return Mathf.Min(other.length * (1f - progress), other.length - 0.01f);
        }

        /// <summary>Indica si la fuente suena (o está programada) y no se está desvaneciendo.</summary>
        private bool IsSounding(AudioSource source) => source.isPlaying && source.volume >= volume;

        private void Play(AudioSource source, float time, double dspTime)
        {
            source.Stop();
            source.volume = volume;
            source.time = time;
            source.PlayScheduled(dspTime);
        }

        /// <summary>Marca la fuente para desvanecerse; Update la baja y la detiene.</summary>
        private void FadeOut(AudioSource source)
        {
            // Si solo estaba programada (el bucle que aún no empieza), Stop cancela la programación.
            if (!source.isPlaying) { source.Stop(); return; }
            // Bajarla un poquito la marca como «desvaneciéndose» para IsSounding y Update.
            if (source.volume >= volume) source.volume = volume * 0.999f;
        }

        /// <summary>Baja el volumen de las fuentes que se desvanecen hasta detenerlas.</summary>
        private void Update()
        {
            Fade(start);
            Fade(loop);
            Fade(end);
        }

        private void Fade(AudioSource source)
        {
            if (!source.isPlaying || source.volume >= volume) return;
            source.volume -= volume * Time.unscaledDeltaTime / fadeTime;
            if (source.volume <= 0f) source.Stop();
        }
    }
}

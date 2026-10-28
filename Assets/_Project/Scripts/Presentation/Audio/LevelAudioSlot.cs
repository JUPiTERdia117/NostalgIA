// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: LevelAudioSlot.cs
// Propósito: ranura de audio 100 % a prueba de null para conectar eventos del
// nivel con los clips que asigne el equipo en el Inspector sin tocar código.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using UnityEngine;

namespace NostalgIA.Presentation.Audio
{
    /// <summary>Evento del bus que dispara el clip puntual de una ranura de audio.</summary>
    public enum AudioSlotTrigger
    {
        /// <summary>Solo bucle ambiental o disparo manual.</summary>
        AmbientOrManual,
        /// <summary>Cuando el jugador rebota en esta burbuja trampolín (<see cref="BubbleBounced"/>).</summary>
        BubbleBounce,
        /// <summary>Cuando este obstáculo giratorio golpea al jugador (<see cref="HazardTriggered"/>).</summary>
        HazardHit,
        /// <summary>Cuando se abre este muro o barrera de arena (<see cref="ArenaGateOpened"/>).</summary>
        ArenaGateOpen,
        /// <summary>Cuando el jugador chapotea en el océano poco profundo (<see cref="ShallowWaterEntered"/>).</summary>
        ShallowWaterSplash,
        /// <summary>Cuando el jugador cruza una puerta de nivel o completa la estación.</summary>
        LevelGateOrCompletion
    }

    /// <summary>
    /// Componente ligero de presentación sonora. Dejé todos los campos de <see cref="AudioClip"/>
    /// opcionales: si siguen en <c>null</c> no reproduce nada ni lanza advertencias, y en cuanto el
    /// equipo arrastre sus propios audios en el Inspector funcionarán de inmediato.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public sealed class LevelAudioSlot : MonoBehaviour
    {
        [Header("Descripción para el equipo")]
        [Tooltip("Indica qué sonido va aquí para identificarlo rápido en el Inspector.")]
        [SerializeField] private string slotDescription = "Asignar clip de audio aquí";

        [Header("Modo de activación")]
        [Tooltip("Qué evento del juego dispara el clip puntual de esta ranura.")]
        [SerializeField] private AudioSlotTrigger triggerMode = AudioSlotTrigger.AmbientOrManual;
        [Tooltip("Si está activo, solo responde cuando el evento proviene de este mismo objeto (útil en burbujas o aspas individuales).")]
        [SerializeField] private bool matchThisTransformOnly = true;

        [Header("Clips (dejar en None hasta asignar audios propios)")]
        [Tooltip("Clip en bucle continuo (ambiente de océano, zumbido de plataforma móvil, giro de aspas o puerta).")]
        [SerializeField] private AudioClip loopClip;
        [Tooltip("Clip puntual al dispararse el evento configurado (rebote, golpe, chapoteo, apertura de muro).")]
        [SerializeField] private AudioClip actionClip;

        [Header("Mezcla")]
        [Tooltip("Volumen base del clip (0 a 1).")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [Tooltip("Mezcla espacial: 0 = 2D global (música/ambiente general), 1 = 3D posicional en el mundo.")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [Tooltip("Distancia máxima de escucha cuando es 3D, en metros.")]
        [SerializeField, Min(2f)] private float maxDistance = 35f;
        [Tooltip("Variación aleatoria sutil de tono en golpes o rebotes para que no suene repetitivo.")]
        [SerializeField, Range(0f, 0.2f)] private float pitchJitter = 0.05f;

        private AudioSource source;

        /// <summary>Descripción legible de qué audio corresponde asignar en esta ranura.</summary>
        public string SlotDescription => slotDescription;

        /// <summary>Configura la ranura desde el constructor de escena dejando los clips en null.</summary>
        public void Configure(string description, AudioSlotTrigger trigger, bool matchSelf, float blend3D, float maxDist = 35f, float baseVolume = 0.8f)
        {
            slotDescription = description;
            triggerMode = trigger;
            matchThisTransformOnly = matchSelf;
            spatialBlend = Mathf.Clamp01(blend3D);
            maxDistance = Mathf.Max(2f, maxDist);
            volume = Mathf.Clamp01(baseVolume);

            AudioSource attached = GetComponent<AudioSource>();
            if (attached != null) ApplySourceSettings(attached);
        }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            ApplySourceSettings(source);
        }

        private void Start()
        {
            if (loopClip != null && source != null)
            {
                source.clip = loopClip;
                source.loop = true;
                source.Play();
            }
        }

        private void OnEnable()
        {
            switch (triggerMode)
            {
                case AudioSlotTrigger.BubbleBounce:
                    EventBus<BubbleBounced>.Subscribe(OnBubbleBounced);
                    break;
                case AudioSlotTrigger.HazardHit:
                    EventBus<HazardTriggered>.Subscribe(OnHazardTriggered);
                    break;
                case AudioSlotTrigger.ArenaGateOpen:
                    EventBus<ArenaGateOpened>.Subscribe(OnArenaGateOpened);
                    break;
                case AudioSlotTrigger.ShallowWaterSplash:
                    EventBus<ShallowWaterEntered>.Subscribe(OnWaterEntered);
                    break;
                case AudioSlotTrigger.LevelGateOrCompletion:
                    EventBus<LevelGateCrossed>.Subscribe(OnGateCrossed);
                    EventBus<StationCompleted>.Subscribe(OnStationCompleted);
                    break;
            }
        }

        private void OnDisable()
        {
            switch (triggerMode)
            {
                case AudioSlotTrigger.BubbleBounce:
                    EventBus<BubbleBounced>.Unsubscribe(OnBubbleBounced);
                    break;
                case AudioSlotTrigger.HazardHit:
                    EventBus<HazardTriggered>.Unsubscribe(OnHazardTriggered);
                    break;
                case AudioSlotTrigger.ArenaGateOpen:
                    EventBus<ArenaGateOpened>.Unsubscribe(OnArenaGateOpened);
                    break;
                case AudioSlotTrigger.ShallowWaterSplash:
                    EventBus<ShallowWaterEntered>.Unsubscribe(OnWaterEntered);
                    break;
                case AudioSlotTrigger.LevelGateOrCompletion:
                    EventBus<LevelGateCrossed>.Unsubscribe(OnGateCrossed);
                    EventBus<StationCompleted>.Unsubscribe(OnStationCompleted);
                    break;
            }
        }

        private void ApplySourceSettings(AudioSource target)
        {
            target.playOnAwake = false;
            target.loop = loopClip != null;
            target.volume = volume;
            target.spatialBlend = spatialBlend;
            target.rolloffMode = AudioRolloffMode.Linear;
            target.minDistance = 2f;
            target.maxDistance = maxDistance;
        }

        private void OnBubbleBounced(in BubbleBounced evt)
        {
            if (matchThisTransformOnly && evt.Source != transform) return;
            PlayActionOneShot();
        }

        private void OnHazardTriggered(in HazardTriggered evt)
        {
            if (matchThisTransformOnly && evt.Source != transform) return;
            PlayActionOneShot();
        }

        private void OnArenaGateOpened(in ArenaGateOpened evt)
        {
            if (matchThisTransformOnly && evt.Source != transform) return;
            PlayActionOneShot();
        }

        private void OnWaterEntered(in ShallowWaterEntered evt)
        {
            if (spatialBlend > 0.5f) transform.position = evt.Position;
            PlayActionOneShot();
        }

        private void OnGateCrossed(in LevelGateCrossed evt)
        {
            if (matchThisTransformOnly && evt.Gate != transform) return;
            PlayActionOneShot();
        }

        private void OnStationCompleted(in StationCompleted evt) => PlayActionOneShot();

        /// <summary>Reproduce el clip puntual si el equipo ya asignó uno; con null no hace nada.</summary>
        public void PlayActionOneShot()
        {
            if (actionClip == null || source == null || !source.isActiveAndEnabled) return;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(actionClip, volume);
        }
    }
}

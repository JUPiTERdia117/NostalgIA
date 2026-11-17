// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: GameSfx.cs
// Propósito: efectos de sonido del jugador (movimiento y depósito lleno de la
// aspiradora), cada uno conectado a su evento del bus.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using UnityEngine;
using UnityEngine.Audio;

namespace NostalgIA.Presentation.Audio
{
    /// <summary>
    /// Toca los efectos de sonido del jugador: pasos, aterrizaje, deslizamiento y el aviso de
    /// depósito lleno de la aspiradora (el motor de la aspiradora lo toca <see cref="VacuumAudio"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cada efecto es un <b>Audio Random Container</b> de Unity (Assets ▸ Create ▸ Audio ▸ Audio
    /// Random Container): un asset con la lista de variantes y su volumen, tono y orden aleatorio.
    /// Están en <c>Assets/_Project/Audio/Presets/</c>. Para retocar un sonido se edita su asset; para
    /// agregar uno nuevo: se crea el asset, se agrega un campo aquí y se toca desde su evento con
    /// <see cref="Play"/>. Los campos son <see cref="AudioResource"/>, así que también aceptan un
    /// <c>AudioClip</c> suelto.
    /// </para>
    /// <para>
    /// Cada efecto tiene su propia fuente para que no se corten entre ellos, y todas salen por el
    /// grupo de efectos del mixer, que es el que controla el deslizador del menú.
    /// </para>
    /// <para>
    /// Los pasos no tienen evento: mientras el jugador está en el suelo sumo la distancia
    /// horizontal que recorre y cada <see cref="stepDistance"/> metros toco un paso.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class GameSfx : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] private AudioResource footstep;
        [Tooltip("Metros recorridos en el suelo entre un paso y otro.")]
        [SerializeField] private float stepDistance = 2f;
        [SerializeField] private AudioResource land;
        [SerializeField] private AudioResource slide;

        [Header("Aspiradora")]
        [SerializeField] private AudioResource vacuumFull;
        [Tooltip("Sonido al disparar un objeto con la aspiradora (SoundReality - Gunshot).")]
        [SerializeField] private AudioResource launch;

        [Header("Nivel y Jugador")]
        [Tooltip("Sonido al recoger una estrella (KoiRoylers - Get Coin).")]
        [SerializeField] private AudioResource starCollect;
        [Tooltip("Sonido al saltar en una burbuja trampolín (freesound_community - Boing).")]
        [SerializeField] private AudioResource bubbleBounce;
        [Tooltip("Sonido cuando el jugador muere (freesound_community - Videogame Death Sound).")]
        [SerializeField] private AudioResource playerDeath;

        [Header("Enemigos (Respaldo 2D)")]
        [Tooltip("Sonido cuando el email dispara (Universfield - Arcade Shot).")]
        [SerializeField] private AudioResource emailShoot;
        [Tooltip("Sonido cuando el WinRar muerde (Anon - Mimi Chomp).")]
        [SerializeField] private AudioResource winrarBite;
        [Tooltip("Sonido cuando ataca una araña (DavidDumaisAudio - Small Monster Attack).")]
        [SerializeField] private AudioResource spiderAttack;

        private AudioSource footstepSource, landSource, slideSource, fullSource;
        private AudioSource launchSource, starSource, bubbleSource, deathSource;
        private AudioSource emailShootSource, winrarBiteSource, spiderAttackSource;

        private bool grounded;
        private Vector3 lastPosition;
        private float walked;

        /// <summary>Crea una fuente 2D por efecto, todas en el grupo de efectos.</summary>
        private void Awake()
        {
            footstepSource = CreateSource(footstep);
            landSource = CreateSource(land);
            slideSource = CreateSource(slide);
            fullSource = CreateSource(vacuumFull);
            launchSource = CreateSource(launch);
            starSource = CreateSource(starCollect);
            bubbleSource = CreateSource(bubbleBounce);
            deathSource = CreateSource(playerDeath);
            emailShootSource = CreateSource(emailShoot);
            winrarBiteSource = CreateSource(winrarBite);
            spiderAttackSource = CreateSource(spiderAttack);
            lastPosition = transform.position;
        }

        private AudioSource CreateSource(AudioResource resource)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.resource = resource;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = GameAudio.Sfx;
            return source;
        }

        private void OnEnable()
        {
            EventBus<LocomotionStateChanged>.Subscribe(OnLocomotion);
            EventBus<PlayerLanded>.Subscribe(OnLanded);
            EventBus<SlideStarted>.Subscribe(OnSlide);
            EventBus<VacuumFull>.Subscribe(OnVacuumFull);
            EventBus<ObjectLaunched>.Subscribe(OnObjectLaunched);
            EventBus<StarCollected>.Subscribe(OnStarCollected);
            EventBus<BubbleBounced>.Subscribe(OnBubbleBounced);
            EventBus<PlayerDied>.Subscribe(OnPlayerDied);
            EventBus<EnemyAttacked>.Subscribe(OnEnemyAttacked);
        }

        private void OnDisable()
        {
            EventBus<LocomotionStateChanged>.Unsubscribe(OnLocomotion);
            EventBus<PlayerLanded>.Unsubscribe(OnLanded);
            EventBus<SlideStarted>.Unsubscribe(OnSlide);
            EventBus<VacuumFull>.Unsubscribe(OnVacuumFull);
            EventBus<ObjectLaunched>.Unsubscribe(OnObjectLaunched);
            EventBus<StarCollected>.Unsubscribe(OnStarCollected);
            EventBus<BubbleBounced>.Unsubscribe(OnBubbleBounced);
            EventBus<PlayerDied>.Unsubscribe(OnPlayerDied);
            EventBus<EnemyAttacked>.Unsubscribe(OnEnemyAttacked);
        }

        // ------------------------------------------------------------------ pasos
        /// <summary>Suma la distancia caminada en el suelo y toca un paso cada tantos metros.</summary>
        private void Update()
        {
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;

            // Un salto grande en un solo cuadro es un teletransporte (reaparición), no un paso.
            if (!grounded || distance > 2f || Time.timeScale == 0f) return;

            walked += distance;
            if (walked < stepDistance) return;
            walked = 0f;
            Play(footstepSource);
        }

        private void OnLocomotion(in LocomotionStateChanged e)
        {
            grounded = e.Current == LocomotionState.Grounded || e.Current == LocomotionState.Crouching;
            // El primer paso suena medio paso después de aterrizar o empezar a caminar.
            if (!grounded) walked = stepDistance * 0.5f;
        }

        // ------------------------------------------------------------------ eventos
        /// <summary>El aterrizaje suena más fuerte cuanto más rápido caía (los aterrizajes suaves no suenan).</summary>
        private void OnLanded(in PlayerLanded e)
        {
            if (e.ImpactSpeed < 3f) return;
            Play(landSource, Mathf.InverseLerp(3f, 15f, e.ImpactSpeed) * 0.7f + 0.3f);
        }

        private void OnSlide(in SlideStarted e) => Play(slideSource);
        private void OnVacuumFull(in VacuumFull e) => Play(fullSource);
        private void OnObjectLaunched(in ObjectLaunched e) => Play(launchSource);
        private void OnStarCollected(in StarCollected e) => Play(starSource);
        private void OnBubbleBounced(in BubbleBounced e) => Play(bubbleSource);
        private void OnPlayerDied(in PlayerDied e) => Play(deathSource);

        private void OnEnemyAttacked(in EnemyAttacked e)
        {
            if (e.PlayedLocally) return;
            switch (e.Kind)
            {
                case EnemyKind.SpamMail:
                    Play(emailShootSource);
                    break;
                case EnemyKind.Antivirus:
                    Play(winrarBiteSource);
                    break;
                case EnemyKind.Bug:
                    Play(spiderAttackSource);
                    break;
            }
        }

        /// <summary>
        /// Toca el siguiente sonido del preset. Con un Audio Random Container en modo manual, cada
        /// <c>Play</c> elige una variante según su modo (aleatorio, sin repetir, en orden).
        /// </summary>
        private static void Play(AudioSource source, float volume = 1f)
        {
            if (source.resource == null) return;
            source.volume = volume;
            source.Play();
        }
    }
}

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

        [Header("Aspiradora y Herramientas")]
        [SerializeField] private AudioResource vacuumFull;
        [Tooltip("Sonido al disparar un objeto con la aspiradora (SoundReality - Gunshot).")]
        [SerializeField] private AudioResource launch;
        [Tooltip("Sonido al cambiar de modo de herramienta (freesound_Community - break_robot).")]
        [SerializeField] private AudioResource toolSwitch;
        [Tooltip("Sonido al usar el gancho (freesound_Community - grappling hook).")]
        [SerializeField] private AudioResource grappleFire;

        [Header("Nivel y Jugador")]
        [Tooltip("Sonido al recoger una estrella (KoiRoylers - Get Coin).")]
        [SerializeField] private AudioResource starCollect;
        [Tooltip("Sonido al saltar en una burbuja trampolín (freesound_community - Boing).")]
        [SerializeField] private AudioResource bubbleBounce;
        [Tooltip("Sonido al caer sobre el agua (freesound_community - Splash).")]
        [SerializeField] private AudioResource waterSplash;
        [Tooltip("Sonido al activar un checkpoint (freesound_Community - futuristic_city_terminal).")]
        [SerializeField] private AudioResource checkpointActivate;
        [Tooltip("Sonido cuando el jugador muere (freesound_community - Videogame Death Sound).")]
        [SerializeField] private AudioResource playerDeath;

        [Header("Enemigos")]
        [Tooltip("Sonido cuando el email dispara (Universfield - Arcade Shot).")]
        [SerializeField] private AudioResource emailShoot;
        [Tooltip("Sonido cuando el WinRar muerde (Anon - Mimi Chomp).")]
        [SerializeField] private AudioResource winrarBite;
        [Tooltip("Sonido cuando ataca una araña (DavidDumaisAudio - Small Monster Attack).")]
        [SerializeField] private AudioResource spiderAttack;
        [Tooltip("Sonido cada vez que sale una araña (Yodguard - small insect monster 6).")]
        [SerializeField] private AudioResource spiderSpawn;
        [Tooltip("Sonido al morir el caballo/troyano (AleXZavesa - Breaking Glass 3).")]
        [SerializeField] private AudioResource trojanDeath;
        [Tooltip("Sonido al morir el WinRar (AleXZavesa - Boards Fall 1).")]
        [SerializeField] private AudioResource winrarDeath;
        [Tooltip("Sonido al morir el email (OxidVideos - Crumpling paper wrapping).")]
        [SerializeField] private AudioResource emailDeath;
        [Tooltip("Sonido al morir una araña (minecraft - Spider death).")]
        [SerializeField] private AudioResource spiderDeath;

        private const float ContinuousWalkStart = 1.90f;
        private const float ContinuousWalkEnd = 9.65f;

        private AudioSource footstepSource, landSource, slideSource, fullSource;
        private AudioSource launchSource, toolSwitchSource, grappleSource;
        private AudioSource starSource, bubbleSource, waterSplashSource, checkpointSource, deathSource;
        private AudioSource emailShootSource, winrarBiteSource, spiderAttackSource, spiderSpawnSource;
        private AudioSource trojanDeathSource, winrarDeathSource, emailDeathSource, spiderDeathSource;

        private AudioClip continuousWalkClip;
        private bool grounded;
        private bool initialToolReceived;
        private float lastWaterSplashTime = float.NegativeInfinity;
        private float walkStopTimer;
        private Vector3 lastPosition;
        private float walked;

        /// <summary>Crea una fuente 2D por efecto, todas en el grupo de efectos.</summary>
        private void Awake()
        {
            footstepSource = CreateSource(footstep);
            if (footstep is AudioClip clip && clip.length > 2f)
            {
                continuousWalkClip = clip;
                footstepSource.clip = clip;
                footstepSource.loop = true;
            }
            landSource = CreateSource(land);
            slideSource = CreateSource(slide);
            fullSource = CreateSource(vacuumFull);
            launchSource = CreateSource(launch);
            toolSwitchSource = CreateSource(toolSwitch);
            grappleSource = CreateSource(grappleFire);
            starSource = CreateSource(starCollect);
            bubbleSource = CreateSource(bubbleBounce);
            waterSplashSource = CreateSource(waterSplash);
            checkpointSource = CreateSource(checkpointActivate);
            deathSource = CreateSource(playerDeath);
            emailShootSource = CreateSource(emailShoot);
            winrarBiteSource = CreateSource(winrarBite);
            spiderAttackSource = CreateSource(spiderAttack);
            spiderSpawnSource = CreateSource(spiderSpawn);
            trojanDeathSource = CreateSource(trojanDeath);
            winrarDeathSource = CreateSource(winrarDeath);
            emailDeathSource = CreateSource(emailDeath);
            spiderDeathSource = CreateSource(spiderDeath);
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
            initialToolReceived = false;
            EventBus<LocomotionStateChanged>.Subscribe(OnLocomotion);
            EventBus<PlayerLanded>.Subscribe(OnLanded);
            EventBus<SlideStarted>.Subscribe(OnSlide);
            EventBus<VacuumFull>.Subscribe(OnVacuumFull);
            EventBus<ObjectLaunched>.Subscribe(OnObjectLaunched);
            EventBus<ToolSwitched>.Subscribe(OnToolSwitched);
            EventBus<GrappleFired>.Subscribe(OnGrappleFired);
            EventBus<StarCollected>.Subscribe(OnStarCollected);
            EventBus<BubbleBounced>.Subscribe(OnBubbleBounced);
            EventBus<ShallowWaterEntered>.Subscribe(OnWaterEntered);
            EventBus<CheckpointActivated>.Subscribe(OnCheckpointActivated);
            EventBus<PlayerDied>.Subscribe(OnPlayerDied);
            EventBus<EnemyAttacked>.Subscribe(OnEnemyAttacked);
            EventBus<EnemySpawned>.Subscribe(OnEnemySpawned);
            EventBus<EnemyKilled>.Subscribe(OnEnemyKilled);
        }

        private void OnDisable()
        {
            EventBus<LocomotionStateChanged>.Unsubscribe(OnLocomotion);
            EventBus<PlayerLanded>.Unsubscribe(OnLanded);
            EventBus<SlideStarted>.Unsubscribe(OnSlide);
            EventBus<VacuumFull>.Unsubscribe(OnVacuumFull);
            EventBus<ObjectLaunched>.Unsubscribe(OnObjectLaunched);
            EventBus<ToolSwitched>.Unsubscribe(OnToolSwitched);
            EventBus<GrappleFired>.Unsubscribe(OnGrappleFired);
            EventBus<StarCollected>.Unsubscribe(OnStarCollected);
            EventBus<BubbleBounced>.Unsubscribe(OnBubbleBounced);
            EventBus<ShallowWaterEntered>.Unsubscribe(OnWaterEntered);
            EventBus<CheckpointActivated>.Unsubscribe(OnCheckpointActivated);
            EventBus<PlayerDied>.Unsubscribe(OnPlayerDied);
            EventBus<EnemyAttacked>.Unsubscribe(OnEnemyAttacked);
            EventBus<EnemySpawned>.Unsubscribe(OnEnemySpawned);
            EventBus<EnemyKilled>.Unsubscribe(OnEnemyKilled);
            if (footstepSource != null) footstepSource.Stop();
        }

        // ------------------------------------------------------------------ pasos
        /// <summary>Suma la distancia caminada en el suelo y toca los pasos (o bucle continuo del robot).</summary>
        private void Update()
        {
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;

            if (continuousWalkClip != null)
            {
                float dt = Time.deltaTime;
                bool isWalkingNow = grounded && dt > 0f && Time.timeScale > 0f && distance <= 2f && (distance / dt) > 0.4f;
                if (isWalkingNow)
                {
                    walkStopTimer = 0.08f;
                    if (!footstepSource.isPlaying)
                    {
                        if (footstepSource.time < ContinuousWalkStart || footstepSource.time >= ContinuousWalkEnd)
                        {
                            footstepSource.time = ContinuousWalkStart;
                        }
                        footstepSource.volume = 1f;
                        footstepSource.Play();
                    }
                    else if (footstepSource.time >= ContinuousWalkEnd || footstepSource.time < ContinuousWalkStart)
                    {
                        footstepSource.time = ContinuousWalkStart;
                    }
                }
                else
                {
                    walkStopTimer -= Time.unscaledDeltaTime;
                    if (walkStopTimer <= 0f && footstepSource.isPlaying)
                    {
                        footstepSource.Pause();
                    }
                }
                return;
            }

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
            if (!grounded)
            {
                walked = stepDistance * 0.5f;
                if (continuousWalkClip != null && footstepSource != null && footstepSource.isPlaying)
                {
                    footstepSource.Pause();
                }
            }
        }

        // ------------------------------------------------------------------ eventos
        /// <summary>El aterrizaje suena más fuerte cuanto más rápido caía (los aterrizajes suaves o en agua no suenan a concreto).</summary>
        private void OnLanded(in PlayerLanded e)
        {
            if (e.ImpactSpeed < 3f || Time.time - lastWaterSplashTime < 0.6f) return;
            Play(landSource, Mathf.InverseLerp(3f, 15f, e.ImpactSpeed) * 0.7f + 0.3f);
        }

        private void OnSlide(in SlideStarted e) => Play(slideSource);
        private void OnVacuumFull(in VacuumFull e) => Play(fullSource);
        private void OnObjectLaunched(in ObjectLaunched e) => Play(launchSource);

        private void OnToolSwitched(in ToolSwitched e)
        {
            // Se ignora la selección automática inicial al arrancar la escena.
            if (!initialToolReceived)
            {
                initialToolReceived = true;
                return;
            }
            Play(toolSwitchSource);
        }

        private void OnGrappleFired(in GrappleFired e) => Play(grappleSource);
        private void OnStarCollected(in StarCollected e) => Play(starSource);
        private void OnBubbleBounced(in BubbleBounced e) => Play(bubbleSource);

        private void OnWaterEntered(in ShallowWaterEntered e)
        {
            lastWaterSplashTime = Time.time;
            Play(waterSplashSource);
        }

        private void OnCheckpointActivated(in CheckpointActivated e) => Play(checkpointSource);
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

        private void OnEnemySpawned(in EnemySpawned e)
        {
            if (e.PlayedLocally) return;
            if (e.Kind == EnemyKind.Bug) Play(spiderSpawnSource);
        }

        private void OnEnemyKilled(in EnemyKilled e)
        {
            switch (e.Kind)
            {
                case EnemyKind.Trojan:
                    Play(trojanDeathSource);
                    break;
                case EnemyKind.Antivirus:
                    Play(winrarDeathSource);
                    break;
                case EnemyKind.SpamMail:
                    Play(emailDeathSource);
                    break;
                case EnemyKind.Bug:
                    Play(spiderDeathSource);
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

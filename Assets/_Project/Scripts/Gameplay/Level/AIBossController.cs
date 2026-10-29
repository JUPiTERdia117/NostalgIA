// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: AIBossController.cs
// Propósito: controlador del jefe final (IA) en el centro del laberinto circular.
// Coordina las 3 oleadas de enemigos y la activación secuencial de los 3 monitores
// de pared hasta vencer a la IA y ganar el juego.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Enemies;
using NostalgIA.Gameplay.Player;
using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Máquina de estados del combate final contra la IA en el centro del laberinto (Estación 4).
    /// <list type="number">
    /// <item>Espera a que el jugador entre a la cámara central.</item>
    /// <item>Despliega la Oleada 1; al derrotarla todos sus enemigos, activa el Monitor 1 en la pared.</item>
    /// <item>Cuando el jugador dispara un objeto absorbido y destruye el Monitor 1, daña a la IA (1/3) y lanza la Oleada 2.</item>
    /// <item>Al derrotar la Oleada 2 se activa el Monitor 2; al destruirlo daña a la IA (2/3) y lanza la Oleada 3.</item>
    /// <item>Al derrotar la Oleada 3 se activa el Monitor 3; con el tercer golpe vence a la IA.</item>
    /// <item>La IA se destruye con el mismo efecto de muerte de los enemigos y en su lugar queda
    /// flotando el portal a la estación final, con una esfera de gancho arriba para alcanzarlo.</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AIBossController : MonoBehaviour
    {
        private enum BossPhase
        {
            WaitingForPlayer,
            Wave1Combat,
            WaitingForMonitor1Hit,
            Wave2Transition,
            Wave2Combat,
            WaitingForMonitor2Hit,
            Wave3Transition,
            Wave3Combat,
            WaitingForMonitor3Hit,
            Defeated
        }

        [Header("Activación de la arena")]
        [Tooltip("Radio en metros desde el centro de la cámara a partir del cual inicia el combate contra la IA.")]
        [SerializeField, Min(5f)] private float activationRadius = 21.5f;
        [Tooltip("Pausa breve entre la destrucción de un monitor y la aparición de la siguiente oleada, en segundos.")]
        [SerializeField, Min(0.2f)] private float waveTransitionDelay = 1.6f;

        [Header("Contenedores de las 3 oleadas")]
        [Tooltip("Raíz cuyos hijos son los enemigos de la primera oleada.")]
        [SerializeField] private Transform wave1Container;
        [Tooltip("Raíz cuyos hijos son los enemigos de la segunda oleada.")]
        [SerializeField] private Transform wave2Container;
        [Tooltip("Raíz cuyos hijos son los enemigos de la tercera oleada.")]
        [SerializeField] private Transform wave3Container;

        [Header("Monitores de pared (3 secciones)")]
        [Tooltip("Los 3 monitores en la pared que se activan uno tras cada oleada derrotada.")]
        [SerializeField] private AIMonitorTarget[] wallMonitors = new AIMonitorTarget[3];

        [Header("Núcleo central de la IA (estilo Portal)")]
        [Tooltip("Transform del chasis colgante de la IA en el centro de la sala.")]
        [SerializeField] private Transform bossCoreTransform;
        [Tooltip("Renderer del ojo o pantalla central de la IA para reflejar su nivel de daño.")]
        [SerializeField] private Renderer bossEyeRenderer;
        [Tooltip("Objetos que se activan al vencer a la IA (efectos o iluminación de victoria).")]
        [SerializeField] private GameObject[] activateOnVictory = new GameObject[0];

        [Header("Destrucción de la IA y portal final")]
        [Tooltip("Fragmentos del efecto de muerte de enemigos que suelta el núcleo al destruirse.")]
        [SerializeField, Min(1)] private int coreGibCount = 16;
        [Tooltip("Escala del efecto de muerte para un objeto tan grande como el núcleo (1 = enemigo normal).")]
        [SerializeField, Min(0.1f)] private float coreGibScale = 2.5f;
        [Tooltip("Portal a la estación final; queda apagado hasta vencer a la IA y aparece donde estaba el núcleo.")]
        [SerializeField] private GameObject victoryPortal;
        [Tooltip("Esfera de gancho sobre el portal para poder alcanzarlo; también aparece al vencer a la IA.")]
        [SerializeField] private GameObject victoryGrappleAnchor;
        [Tooltip("Distancia bajo el centro de la arena a partir de la cual un enemigo caído cuenta como derrotado, en m.")]
        [SerializeField, Min(5f)] private float fallenEnemyDepth = 20f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private const float PollInterval = 0.12f;

        private BossPhase phase = BossPhase.WaitingForPlayer;
        private Transform playerTransform;
        private MaterialPropertyBlock propBlock;
        private Vector3 coreBasePosition;
        private Quaternion coreBaseRotation;
        private float pollTimer;
        private float transitionTimer;
        private float hurtShakeTimer;
        private int monitorsDestroyedCount;

        /// <summary>Configura todas las referencias del encuentro desde el constructor de escena.</summary>
        public void Configure(
            float triggerRadius,
            Transform w1,
            Transform w2,
            Transform w3,
            AIMonitorTarget[] monitors,
            Transform coreTransform,
            Renderer eyeRenderer,
            GameObject[] victoryObjects)
        {
            activationRadius = Mathf.Max(5f, triggerRadius);
            wave1Container = w1;
            wave2Container = w2;
            wave3Container = w3;
            wallMonitors = monitors ?? new AIMonitorTarget[0];
            bossCoreTransform = coreTransform;
            bossEyeRenderer = eyeRenderer;
            activateOnVictory = victoryObjects ?? new GameObject[0];
        }

        /// <summary>Asigna el portal de victoria y su esfera de gancho desde una herramienta de editor.</summary>
        public void ConfigureVictoryPortal(GameObject portal, GameObject grappleAnchor)
        {
            victoryPortal = portal;
            victoryGrappleAnchor = grappleAnchor;
        }

        /// <summary>
        /// Atajo de prueba (menú contextual del componente en modo juego): da por vencida a la IA
        /// para revisar la destrucción y el portal sin jugar las tres oleadas.
        /// </summary>
        [ContextMenu("Prueba: vencer a la IA")]
        private void DebugDefeat()
        {
            if (!Application.isPlaying || phase == BossPhase.Defeated) return;
            CompleteBossFight();
        }

        private void Awake()
        {
            propBlock = new MaterialPropertyBlock();
            if (bossCoreTransform != null)
            {
                coreBasePosition = bossCoreTransform.localPosition;
                coreBaseRotation = bossCoreTransform.localRotation;
            }

            // Mantengo las 3 oleadas apagadas hasta que el jugador entre al centro del laberinto.
            if (wave1Container != null) wave1Container.gameObject.SetActive(false);
            if (wave2Container != null) wave2Container.gameObject.SetActive(false);
            if (wave3Container != null) wave3Container.gameObject.SetActive(false);

            for (int i = 0; i < wallMonitors.Length; i++)
            {
                if (wallMonitors[i] != null) wallMonitors[i].SetVulnerable(false);
            }

            for (int i = 0; i < activateOnVictory.Length; i++)
            {
                if (activateOnVictory[i] != null) activateOnVictory[i].SetActive(false);
            }

            // El portal y su esfera de gancho no existen para el jugador hasta que cae la IA.
            if (victoryPortal != null) victoryPortal.SetActive(false);
            if (victoryGrappleAnchor != null) victoryGrappleAnchor.SetActive(false);

            ApplyBossEyeColor(new Color(0.0f, 0.85f, 1.0f, 1f));
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            AnimateBossCore(dt);

            pollTimer -= dt;
            switch (phase)
            {
                case BossPhase.WaitingForPlayer:
                    if (pollTimer <= 0f)
                    {
                        pollTimer = PollInterval;
                        if (IsPlayerInsideArena())
                        {
                            StartWave1();
                        }
                    }
                    break;

                case BossPhase.Wave1Combat:
                    if (pollTimer <= 0f)
                    {
                        pollTimer = PollInterval;
                        if (IsWaveCleared(wave1Container))
                        {
                            ExposeMonitor(0, "» ¡Oleada 1 derrotada! Se activó el Monitor 1 en la pared: dispárale un objeto.");
                            phase = BossPhase.WaitingForMonitor1Hit;
                        }
                    }
                    break;

                case BossPhase.WaitingForMonitor1Hit:
                    if (IsMonitorDestroyed(0))
                    {
                        OnMonitorHit(1, new Color(1.0f, 0.58f, 0.1f, 1f),
                            "» ¡Monitor 1/3 destruido! La IA sufrió daño — ¡Prepárate para la Oleada 2!");
                        transitionTimer = waveTransitionDelay;
                        phase = BossPhase.Wave2Transition;
                    }
                    break;

                case BossPhase.Wave2Transition:
                    transitionTimer -= dt;
                    if (transitionTimer <= 0f)
                    {
                        StartWave2();
                    }
                    break;

                case BossPhase.Wave2Combat:
                    if (pollTimer <= 0f)
                    {
                        pollTimer = PollInterval;
                        if (IsWaveCleared(wave2Container))
                        {
                            ExposeMonitor(1, "» ¡Oleada 2 derrotada! Se activó el Monitor 2 en la pared: dispárale un objeto.");
                            phase = BossPhase.WaitingForMonitor2Hit;
                        }
                    }
                    break;

                case BossPhase.WaitingForMonitor2Hit:
                    if (IsMonitorDestroyed(1))
                    {
                        OnMonitorHit(2, new Color(1.0f, 0.12f, 0.12f, 1f),
                            "» ¡Monitor 2/3 destruido! Núcleo de la IA crítico — ¡Última oleada en camino!");
                        transitionTimer = waveTransitionDelay;
                        phase = BossPhase.Wave3Transition;
                    }
                    break;

                case BossPhase.Wave3Transition:
                    transitionTimer -= dt;
                    if (transitionTimer <= 0f)
                    {
                        StartWave3();
                    }
                    break;

                case BossPhase.Wave3Combat:
                    if (pollTimer <= 0f)
                    {
                        pollTimer = PollInterval;
                        if (IsWaveCleared(wave3Container))
                        {
                            ExposeMonitor(2, "» ¡Última oleada derrotada! ¡Dispara al Monitor 3 para destruir a la IA!");
                            phase = BossPhase.WaitingForMonitor3Hit;
                        }
                    }
                    break;

                case BossPhase.WaitingForMonitor3Hit:
                    if (IsMonitorDestroyed(2))
                    {
                        CompleteBossFight();
                    }
                    break;
            }
        }

        private bool IsPlayerInsideArena()
        {
            if (playerTransform == null)
            {
                PlayerMotor motor = FindAnyObjectByType<PlayerMotor>();
                if (motor != null) playerTransform = motor.transform;
            }
            if (playerTransform == null) return false;

            Vector3 diff = playerTransform.position - transform.position;
            diff.y = 0f;
            return diff.sqrMagnitude <= activationRadius * activationRadius;
        }

        private void StartWave1()
        {
            phase = BossPhase.Wave1Combat;
            if (wave1Container != null) wave1Container.gameObject.SetActive(true);
            ApplyBossEyeColor(new Color(1.0f, 0.78f, 0.15f, 1f));
            EventBus<HudMessageRequested>.Raise(
                new HudMessageRequested("» [IA CENTRAL] ¡Intruso en el núcleo! Oleada 1/3 activada: usa los objetos del suelo."));
        }

        private void StartWave2()
        {
            phase = BossPhase.Wave2Combat;
            if (wave2Container != null) wave2Container.gameObject.SetActive(true);
            EventBus<HudMessageRequested>.Raise(
                new HudMessageRequested("» [IA CENTRAL] ¡Protocolo de defensa 2/3! Derrota a la segunda oleada."));
        }

        private void StartWave3()
        {
            phase = BossPhase.Wave3Combat;
            if (wave3Container != null) wave3Container.gameObject.SetActive(true);
            EventBus<HudMessageRequested>.Raise(
                new HudMessageRequested("» [IA CENTRAL] ¡Sobrecarga final! Derrota a la Oleada 3/3."));
        }

        /// <summary>
        /// Una oleada termina cuando ya no le queda ningún enemigo activo. Si un enemigo cayó fuera
        /// de la arena (por ejemplo, una araña expulsada por el nido que salió volando), nunca
        /// podría morir y el combate se quedaría atorado: a los que están muy por debajo del piso
        /// los cuento como derrotados y los elimino.
        /// </summary>
        private bool IsWaveCleared(Transform waveContainer)
        {
            if (waveContainer == null) return true;
            float floorLimit = transform.position.y - fallenEnemyDepth;
            bool cleared = true;
            for (int i = waveContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = waveContainer.GetChild(i);
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                if (child.position.y < floorLimit)
                {
                    Destroy(child.gameObject);
                    continue;
                }
                cleared = false;
            }
            return cleared;
        }

        private void ExposeMonitor(int index, string hudMessage)
        {
            if (index >= 0 && index < wallMonitors.Length && wallMonitors[index] != null)
            {
                wallMonitors[index].SetVulnerable(true);
            }
            EventBus<HudMessageRequested>.Raise(new HudMessageRequested(hudMessage));
        }

        private bool IsMonitorDestroyed(int index)
        {
            if (index < 0 || index >= wallMonitors.Length || wallMonitors[index] == null) return true;
            return wallMonitors[index].IsDestroyed;
        }

        private void OnMonitorHit(int destroyedSoFar, Color nextEyeColor, string message)
        {
            monitorsDestroyedCount = destroyedSoFar;
            hurtShakeTimer = 1.1f;
            ApplyBossEyeColor(nextEyeColor);
            EventBus<HudMessageRequested>.Raise(new HudMessageRequested(message));
        }

        private void CompleteBossFight()
        {
            phase = BossPhase.Defeated;
            monitorsDestroyedCount = 3;
            hurtShakeTimer = 1.6f;

            ApplyBossEyeColor(new Color(0.08f, 0.1f, 0.12f, 1f));

            // La IA se destruye con el mismo efecto que los enemigos (cubos con su material que
            // salen disparados), solo que más grandes porque el núcleo mide varios metros.
            if (bossCoreTransform != null)
            {
                Renderer coreRenderer = bossCoreTransform.GetComponentInChildren<Renderer>();
                Material material = coreRenderer != null ? coreRenderer.sharedMaterial : null;
                EnemyDeathEffect.Spawn(bossCoreTransform.position, material, coreGibCount, coreGibScale);
                Destroy(bossCoreTransform.gameObject);
                bossCoreTransform = null;
                bossEyeRenderer = null;
            }

            for (int i = 0; i < activateOnVictory.Length; i++)
            {
                if (activateOnVictory[i] != null) activateOnVictory[i].SetActive(true);
            }

            // En el lugar del núcleo queda el portal flotando, con la esfera de gancho encima.
            if (victoryPortal != null) victoryPortal.SetActive(true);
            if (victoryGrappleAnchor != null) victoryGrappleAnchor.SetActive(true);

            EventBus<StationCompleted>.Raise(new StationCompleted(3, 3, Time.timeSinceLevelLoad));
            EventBus<HudMessageRequested>.Raise(
                new HudMessageRequested("» ¡NÚCLEO DE LA IA DESTRUIDO (3/3)! Engánchate a la esfera y déjate caer en el portal."));
        }

        private void AnimateBossCore(float dt)
        {
            // Tras la derrota el núcleo ya no existe (se destruyó en CompleteBossFight).
            if (bossCoreTransform == null || phase == BossPhase.Defeated) return;

            // Giro vigilante mirando hacia el jugador como el núcleo central de Portal.
            if (playerTransform != null)
            {
                Vector3 lookDir = playerTransform.position - bossCoreTransform.position;
                lookDir.y *= 0.25f;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                    float turnSpeed = 2.2f + monitorsDestroyedCount * 1.4f;
                    bossCoreTransform.rotation = Quaternion.Slerp(bossCoreTransform.rotation, targetRot, turnSpeed * dt);
                }
            }

            float hoverFreq = 1.6f + monitorsDestroyedCount * 0.9f;
            Vector3 offset = new Vector3(0f, Mathf.Sin(Time.time * hoverFreq) * 0.28f, 0f);
            if (hurtShakeTimer > 0f)
            {
                hurtShakeTimer -= dt;
                float shake = hurtShakeTimer * 0.45f;
                offset += new Vector3(
                    Mathf.Sin(Time.time * 42f) * shake,
                    Mathf.Cos(Time.time * 37f) * shake,
                    Mathf.Sin(Time.time * 51f) * shake);
            }
            bossCoreTransform.localPosition = coreBasePosition + offset;
        }

        private void ApplyBossEyeColor(Color color)
        {
            if (bossEyeRenderer == null) return;
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            bossEyeRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorId, color);
            propBlock.SetColor(ColorId, color);
            bossEyeRenderer.SetPropertyBlock(propBlock);
        }
    }
}

// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: AIBossController.cs
// Propósito: controlador del jefe final (IA) en el centro del laberinto circular.
// Coordina las 3 oleadas de enemigos y la activación secuencial de los 3 monitores
// de pared hasta vencer a la IA y ganar el juego.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
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
    /// <item>Al derrotar la Oleada 3 se activa el Monitor 3; con el tercer golpe vence a la IA y gana el juego.</item>
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

        private static bool IsWaveCleared(Transform waveContainer)
        {
            if (waveContainer == null) return true;
            int childCount = waveContainer.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = waveContainer.GetChild(i);
                if (child != null && child.gameObject.activeInHierarchy)
                {
                    return false;
                }
            }
            return true;
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

            if (bossCoreTransform != null)
            {
                Material mat = bossEyeRenderer != null ? bossEyeRenderer.sharedMaterial : null;
                EventBus<BreakableDestroyed>.Raise(
                    new BreakableDestroyed(bossCoreTransform.position, 18, mat, 3.5f));
            }

            for (int i = 0; i < activateOnVictory.Length; i++)
            {
                if (activateOnVictory[i] != null) activateOnVictory[i].SetActive(true);
            }

            EventBus<StationCompleted>.Raise(new StationCompleted(3, 3, Time.timeSinceLevelLoad));
            EventBus<HudMessageRequested>.Raise(
                new HudMessageRequested("» ¡NÚCLEO DE LA IA DESTRUIDO (3/3)! ¡HAS VENCIDO A LA IA Y GANADO EL JUEGO!"));
        }

        private void AnimateBossCore(float dt)
        {
            if (bossCoreTransform == null) return;

            if (phase == BossPhase.Defeated)
            {
                // Al ser derrotada la IA, el chasis colgante pierde energía y queda inclinado hacia abajo.
                Quaternion defeatedRot = coreBaseRotation * Quaternion.Euler(28f, 0f, 14f);
                bossCoreTransform.localRotation = Quaternion.Slerp(bossCoreTransform.localRotation, defeatedRot, 2.5f * dt);
                Vector3 defeatedPos = coreBasePosition + new Vector3(0f, -1.2f, 0f);
                bossCoreTransform.localPosition = Vector3.Lerp(bossCoreTransform.localPosition, defeatedPos, 2.5f * dt);
                return;
            }

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

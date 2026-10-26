// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: RespawnService.cs
// Propósito: reaparición en el último checkpoint sin recargar la escena
// (TDD §5.3, tarea F1-A12).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Core.Game;
using NostalgIA.Core.Lifecycle;
using NostalgIA.Core.Services;
using NostalgIA.Gameplay.Tools;
using UnityEngine;

namespace NostalgIA.Gameplay.Player
{
    /// <summary>
    /// Servicio de reaparición del jugador.
    /// </summary>
    /// <remarks>
    /// <para>Secuencia (TDD §5.3):</para>
    /// <list type="number">
    /// <item>Recibe <see cref="PlayerDied"/> y cambia el estado global a <see cref="GameState.Respawning"/>
    /// (sin entrada) durante <c>respawnDelay</c> segundos (≈ 0,8 s, tiempo de la transición visual).</item>
    /// <item>Reubica al jugador en el último checkpoint activo, anula velocidades, restablece corazones
    /// y herramientas (gancho suelto, succión detenida).</item>
    /// <item>Notifica a los <see cref="IResettable"/> registrados para que vuelvan a su estado inicial.</item>
    /// <item>Publica <see cref="PlayerRespawned"/> y regresa a <see cref="GameState.Playing"/>.</item>
    /// </list>
    /// <para>
    /// Sustituye la recarga completa de escena del prototipo, que tardaba varios segundos en
    /// escenas grandes, generaba basura y obligaba a guardar el progreso en campos estáticos.
    /// La espera se cuenta en <c>Update</c> con un temporizador en lugar de una corrutina, para
    /// no asignar el enumerador en cada muerte.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerHealth))]
    public sealed class RespawnService : MonoBehaviour
    {
        [Tooltip("Controlador de herramientas a restablecer (opcional; se busca en los hijos).")]
        [SerializeField] private ToolController tools;

        private PlayerMotor motor;
        private PlayerHealth health;

        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private int spawnCheckpointIndex = -1;

        private bool respawnPending;
        private float respawnTimer;

        /// <summary>Posición de reaparición actual.</summary>
        public Vector3 SpawnPosition => spawnPosition;

        /// <summary>Índice del checkpoint actual (−1 = inicio de la escena).</summary>
        public int SpawnCheckpointIndex => spawnCheckpointIndex;

        /// <summary>Indica si hay una reaparición en curso.</summary>
        public bool IsRespawning => respawnPending;

        /// <summary>Toma la posición inicial de la escena como primer punto de reaparición.</summary>
        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            health = GetComponent<PlayerHealth>();
            if (tools == null) tools = GetComponentInChildren<ToolController>(true);
            spawnPosition = transform.position;
            spawnRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        /// <summary>Suscripciones y registro del servicio.</summary>
        private void OnEnable()
        {
            ServiceLocator.Register(this);
            EventBus<PlayerDied>.Subscribe(OnPlayerDied);
            EventBus<CheckpointActivated>.Subscribe(OnCheckpointActivated);
        }

        /// <summary>Cancela suscripciones.</summary>
        private void OnDisable()
        {
            EventBus<PlayerDied>.Unsubscribe(OnPlayerDied);
            EventBus<CheckpointActivated>.Unsubscribe(OnCheckpointActivated);
            ServiceLocator.Unregister(this);
        }

        /// <summary>Actualiza el punto de reaparición.</summary>
        private void OnCheckpointActivated(in CheckpointActivated evt)
        {
            spawnPosition = evt.SpawnPosition;
            spawnRotation = evt.SpawnRotation;
            spawnCheckpointIndex = evt.Index;
        }

        /// <summary>Inicia la secuencia de reaparición.</summary>
        private void OnPlayerDied(in PlayerDied evt)
        {
            if (respawnPending) return;
            respawnPending = true;
            respawnTimer = motor.Settings.respawnDelay;
            if (ServiceLocator.TryGet(out GameStateMachine machine)) machine.ChangeState(GameState.Respawning);
            tools?.ResetTools();
        }

        /// <summary>Cuenta la espera de la transición visual.</summary>
        private void Update()
        {
            if (!respawnPending) return;
            respawnTimer -= Time.unscaledDeltaTime;
            if (respawnTimer <= 0f) CompleteRespawn();
        }

        /// <summary>Reubica y restablece al jugador y a la estación.</summary>
        public void CompleteRespawn()
        {
            respawnPending = false;
            motor.ResetForRespawn(spawnPosition, spawnRotation);
            health.Revive();
            tools?.ResetTools();
            ResettableRegistry.ResetAll();

            EventBus<PlayerRespawned>.Raise(new PlayerRespawned(spawnCheckpointIndex, spawnPosition, health.CurrentHearts));
            if (ServiceLocator.TryGet(out GameStateMachine machine)) machine.ChangeState(GameState.Playing);
        }
    }
}

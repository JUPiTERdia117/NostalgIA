// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: RespawnService.cs
// Propósito: reaparición en el último checkpoint (o reinicio del Tutorial) cuando
// el jugador pulsa el botón de la pantalla de muerte.
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Core.Events;
using NostalgIA.Core.Game;
using NostalgIA.Core.Lifecycle;
using NostalgIA.Core.Services;
using NostalgIA.Gameplay.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.Gameplay.Player
{
    /// <summary>
    /// Servicio de reaparición del jugador.
    /// </summary>
    /// <remarks>
    /// <para>Secuencia:</para>
    /// <list type="number">
    /// <item>Recibe <see cref="PlayerDied"/> y cambia el estado global a <see cref="GameState.Respawning"/>
    /// (sin entrada de juego). La pantalla de muerte muestra el robot desarmado y un botón.</item>
    /// <item>Al pulsar el botón llega <see cref="RespawnRequested"/>. En el Tutorial se vuelve a
    /// cargar la escena completa (el tutorial empieza de nuevo).</item>
    /// <item>En los demás niveles reubica al jugador en el último checkpoint activo, anula
    /// velocidades, restablece corazones y herramientas y notifica a los <see cref="IResettable"/>.</item>
    /// <item>Publica <see cref="PlayerRespawned"/> y regresa a <see cref="GameState.Playing"/>.</item>
    /// </list>
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

        /// <summary>Posición de reaparición actual.</summary>
        public Vector3 SpawnPosition => spawnPosition;

        /// <summary>Índice del checkpoint actual (−1 = inicio de la escena).</summary>
        public int SpawnCheckpointIndex => spawnCheckpointIndex;

        /// <summary>Indica si el jugador está muerto esperando el botón.</summary>
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
            EventBus<RespawnRequested>.Subscribe(OnRespawnRequested);
            EventBus<CheckpointActivated>.Subscribe(OnCheckpointActivated);
            EventBus<LevelGateCrossed>.Subscribe(OnLevelGateCrossed);
        }

        /// <summary>Cancela suscripciones.</summary>
        private void OnDisable()
        {
            EventBus<PlayerDied>.Unsubscribe(OnPlayerDied);
            EventBus<RespawnRequested>.Unsubscribe(OnRespawnRequested);
            EventBus<CheckpointActivated>.Unsubscribe(OnCheckpointActivated);
            EventBus<LevelGateCrossed>.Unsubscribe(OnLevelGateCrossed);
            ServiceLocator.Unregister(this);
        }

        /// <summary>
        /// Al entrar a un nivel nuevo por una puerta, su entrada pasa a ser el punto de reaparición
        /// hasta que se active el primer checkpoint del nivel. El índice −1 indica que no es un
        /// checkpoint numerado.
        /// </summary>
        private void OnLevelGateCrossed(in LevelGateCrossed evt)
        {
            spawnPosition = evt.SpawnPosition;
            spawnRotation = evt.SpawnRotation;
            spawnCheckpointIndex = -1;
        }

        /// <summary>Actualiza el punto de reaparición.</summary>
        private void OnCheckpointActivated(in CheckpointActivated evt)
        {
            spawnPosition = evt.SpawnPosition;
            spawnRotation = evt.SpawnRotation;
            spawnCheckpointIndex = evt.Index;
        }

        /// <summary>El jugador murió: se espera al botón de la pantalla de muerte.</summary>
        private void OnPlayerDied(in PlayerDied evt)
        {
            if (respawnPending) return;
            respawnPending = true;
            if (ServiceLocator.TryGet(out GameStateMachine machine)) machine.ChangeState(GameState.Respawning);
            tools?.ResetTools();
        }

        /// <summary>
        /// Se pulsó el botón. En el Tutorial se reinicia la escena completa; en los demás niveles
        /// se reaparece en el último checkpoint.
        /// </summary>
        private void OnRespawnRequested(in RespawnRequested evt)
        {
            if (!respawnPending) return;
            if (gameObject.scene.name == SceneNames.Tutorial)
            {
                respawnPending = false;
                // Aviso de reaparición antes de recargar: los efectos persistentes (posprocesado de
                // GameRoot, pantalla de muerte) vuelven a la normalidad.
                EventBus<PlayerRespawned>.Raise(new PlayerRespawned(-1, spawnPosition, health.MaxHearts));
                SceneManager.LoadScene(SceneNames.Tutorial);
                return;
            }
            CompleteRespawn();
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

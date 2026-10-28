// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: DefeatUnlockController.cs
// Propósito: vigila uno o varios enemigos clave y, al ser derrotados, desplaza un
// muro bloqueador y activa objetos de recompensa (como la Estrella central).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Desbloquea una barrera física y revela recompensas cuando todos los objetivos asignados han
    /// sido destruidos. No acopla <c>NostalgIA.Gameplay</c> con scripts heredados: comprueba la
    /// vida del <see cref="GameObject"/> objetivo (que desaparece al morir) cada intervalo corto.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DefeatUnlockController : MonoBehaviour
    {
        [Header("Objetivos requeridos")]
        [Tooltip("Enemigos que deben ser eliminados para abrir el paso.")]
        [SerializeField] private GameObject[] targetsToDefeat = new GameObject[0];

        [Header("Muro o barrera a desplazar")]
        [Tooltip("Transform del muro que impide avanzar.")]
        [SerializeField] private Transform barrierToMove;
        [Tooltip("Desplazamiento en el mundo que recorre el muro al abrirse (ej. hacia abajo para ocultarse en el edificio).")]
        [SerializeField] private Vector3 openWorldOffset = new Vector3(0f, -8.5f, 0f);
        [Tooltip("Duración de la apertura suave del muro, en segundos.")]
        [SerializeField, Min(0.1f)] private float openDuration = 1.25f;

        [Header("Recompensas y señales")]
        [Tooltip("Objetos que se activan al derrotar a los objetivos (por ejemplo, la Estrella coleccionable).")]
        [SerializeField] private GameObject[] activateOnUnlock = new GameObject[0];
        [Tooltip("Si es verdadero, suelta la recompensa en la última posición donde murió el enemigo para que el jugador vaya a recogerla.")]
        [SerializeField] private bool dropRewardsAtTargetPosition = true;
        [Tooltip("Altura sobre la superficie donde aparece flotando la recompensa soltada, en m.")]
        [SerializeField] private float dropHeightOffset = 1.25f;
        [Tooltip("Objetos que se apagan al desbloquear (por ejemplo, una cápsula protectora o sello visual).")]
        [SerializeField] private GameObject[] deactivateOnUnlock = new GameObject[0];
        [Tooltip("Mensaje opcional que se muestra en el HUD al abrirse la barrera.")]
        [SerializeField] private string unlockToastMessage = string.Empty;

        private const float PollInterval = 0.12f;

        private float pollTimer;
        private bool unlocked;
        private bool finishMoveComplete;
        private Vector3 barrierClosedPosition;
        private Vector3 barrierOpenPosition;
        private Vector3 lastTargetPosition;
        private float floorY;
        private bool hasTargetPosition;
        private float openElapsed;

        /// <summary>Indica si los objetivos ya fueron derrotados y la barrera se desbloqueó.</summary>
        public bool IsUnlocked => unlocked;

        /// <summary>Configura referencias y parámetros desde el constructor del nivel.</summary>
        public void Configure(GameObject[] targets, Transform barrier, Vector3 worldOffset, float duration,
                              GameObject[] toActivate, GameObject[] toDeactivate, string toastMessage,
                              bool dropAtEnemy = true, float dropOffset = 1.25f)
        {
            targetsToDefeat = targets ?? new GameObject[0];
            barrierToMove = barrier;
            openWorldOffset = worldOffset;
            openDuration = Mathf.Max(0.1f, duration);
            activateOnUnlock = toActivate ?? new GameObject[0];
            deactivateOnUnlock = toDeactivate ?? new GameObject[0];
            unlockToastMessage = toastMessage ?? string.Empty;
            dropRewardsAtTargetPosition = dropAtEnemy;
            dropHeightOffset = dropOffset;
        }

        private void Awake()
        {
            if (barrierToMove != null)
            {
                barrierClosedPosition = barrierToMove.position;
                barrierOpenPosition = barrierClosedPosition + openWorldOffset;
            }

            CacheAliveTargetPosition(true);

            // Me aseguro de que las recompensas bloqueadas empiecen apagadas hasta cumplir la condición.
            for (int i = 0; i < activateOnUnlock.Length; i++)
            {
                if (activateOnUnlock[i] != null) activateOnUnlock[i].SetActive(false);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!unlocked)
            {
                CacheAliveTargetPosition(false);
                pollTimer -= dt;
                if (pollTimer <= 0f)
                {
                    pollTimer = PollInterval;
                    if (AllTargetsDefeated()) TriggerUnlock();
                }
                return;
            }

            if (!finishMoveComplete && barrierToMove != null)
            {
                openElapsed += dt;
                float u = Mathf.Clamp01(openElapsed / Mathf.Max(0.1f, openDuration));
                // Uso smoothstep u²·(3 − 2u) para que el muro arranque y frene con inercia mecánica.
                float smooth = u * u * (3f - 2f * u);
                barrierToMove.position = Vector3.Lerp(barrierClosedPosition, barrierOpenPosition, smooth);
                Physics.SyncTransforms();

                if (u >= 1f)
                {
                    finishMoveComplete = true;
                    barrierToMove.gameObject.SetActive(false);
                }
            }
        }

        private void CacheAliveTargetPosition(bool captureFloor)
        {
            if (targetsToDefeat == null) return;
            for (int i = 0; i < targetsToDefeat.Length; i++)
            {
                if (targetsToDefeat[i] != null)
                {
                    lastTargetPosition = targetsToDefeat[i].transform.position;
                    if (captureFloor || !hasTargetPosition) floorY = lastTargetPosition.y;
                    hasTargetPosition = true;
                    return;
                }
            }
        }

        private bool AllTargetsDefeated()
        {
            if (targetsToDefeat == null || targetsToDefeat.Length == 0) return false;
            for (int i = 0; i < targetsToDefeat.Length; i++)
            {
                // En Unity, cuando EnemyHealth llama a Destroy(gameObject) la referencia compara igual a null.
                if (targetsToDefeat[i] != null) return false;
            }
            return true;
        }

        private void TriggerUnlock()
        {
            unlocked = true;
            openElapsed = 0f;

            for (int i = 0; i < deactivateOnUnlock.Length; i++)
            {
                if (deactivateOnUnlock[i] != null) deactivateOnUnlock[i].SetActive(false);
            }

            for (int i = 0; i < activateOnUnlock.Length; i++)
            {
                GameObject reward = activateOnUnlock[i];
                if (reward == null) continue;
                if (dropRewardsAtTargetPosition && hasTargetPosition)
                {
                    // Coloco la estrella justo donde murió el enemigo para que el jugador tenga que ir a recogerla.
                    reward.transform.position = new Vector3(lastTargetPosition.x, floorY + dropHeightOffset, lastTargetPosition.z);
                }
                reward.SetActive(true);
            }

            if (!string.IsNullOrEmpty(unlockToastMessage))
            {
                EventBus<HudMessageRequested>.Raise(new HudMessageRequested(unlockToastMessage));
            }

            Vector3 eventPos = barrierToMove != null ? barrierToMove.position : transform.position;
            EventBus<ArenaGateOpened>.Raise(new ArenaGateOpened(transform, eventPos));
        }
    }
}

// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: GameStateMachine.cs
// Propósito: estados globales, escala de tiempo y bloqueo del cursor.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using UnityEngine;

namespace NostalgIA.Core.Game
{
    /// <summary>
    /// Máquina de estados global de la partida.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cada transición aplica su escala de tiempo y cursor y publica <see cref="GameStateChanged"/>. El
    /// mapa de acciones activo y la visibilidad de los controles táctiles los decide cada
    /// consumidor al recibir el evento (<c>InputReader</c>, <c>TouchControlsVisibility</c>), de modo
    /// que este servicio de <c>Core</c> no depende del ensamblado de entrada.
    /// </para>
    /// <para>
    /// | Estado | <c>Time.timeScale</c> | Cursor |
    /// | Playing / Respawning | 1 | bloqueado (PC) |
    /// | Paused | 0 | libre |
    /// | Loading | 1 | oculto |
    /// | Menús y resultados | 1 | libre |
    /// </para>
    /// </remarks>
    public sealed class GameStateMachine
    {
        /// <summary>Estado actual.</summary>
        public GameState State { get; private set; } = GameState.Boot;

        /// <summary>Indica si el estado actual corresponde a juego activo (incluye la reaparición).</summary>
        public bool IsInGameplay => State == GameState.Playing || State == GameState.Respawning;

        /// <summary>Cambia de estado, aplica sus efectos y publica el evento.</summary>
        /// <param name="next">Estado de destino.</param>
        /// <returns><c>true</c> si hubo transición.</returns>
        public bool ChangeState(GameState next)
        {
            if (next == State) return false;
            GameState previous = State;
            State = next;
            ApplyEffects();
            EventBus<GameStateChanged>.Raise(new GameStateChanged(previous, next));
            return true;
        }

        /// <summary>Alterna entre <see cref="GameState.Playing"/> y <see cref="GameState.Paused"/>.</summary>
        /// <returns><c>true</c> si hubo transición.</returns>
        public bool TogglePause()
        {
            if (State == GameState.Playing) return ChangeState(GameState.Paused);
            if (State == GameState.Paused) return ChangeState(GameState.Playing);
            return false;
        }

        /// <summary>
        /// Vuelve a aplicar la escala de tiempo y el cursor del estado actual. Se llama al
        /// recuperar el foco de la aplicación, porque el sistema operativo libera el cursor.
        /// </summary>
        public void ApplyEffects()
        {
            Time.timeScale = State == GameState.Paused ? 0f : 1f;

            bool lockCursor = IsInGameplay && !Application.isMobilePlatform;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor && State != GameState.Loading;
        }
    }
}

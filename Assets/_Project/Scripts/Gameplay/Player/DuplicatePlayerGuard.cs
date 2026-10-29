// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: DuplicatePlayerGuard.cs
// Propósito: evitar un segundo jugador cuando un nivel con su propio Player se
// carga en segundo plano desde una puerta (LevelGate).
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Gameplay.Player
{
    /// <summary>
    /// Apaga este jugador si ya existe otro jugador vivo en una escena distinta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La puerta entre niveles precarga el nivel siguiente de forma aditiva. Si esa escena trae
    /// su propio <c>Player</c> fuera de un <see cref="Level.StandaloneLevelContent"/>, el jugador
    /// nuevo alcanzaba a ejecutar su <c>OnEnable</c>: se registraba en el ServiceLocator encima del
    /// jugador real, se suscribía a la entrada y a la muerte, y sus controles táctiles quedaban
    /// encima de los nuestros. Por eso, después de morir, las herramientas y los botones se
    /// desincronizaban y aparecía un jugador duplicado.
    /// </para>
    /// <para>
    /// Le doy el orden de ejecución más bajo para que su <c>Awake</c> corra antes que el de
    /// cualquier otro componente del jugador. Si encuentro otro jugador en otra escena, apago el
    /// objeto completo en ese momento y ninguno de sus componentes llega a despertar. Cuando el
    /// nivel se abre solo (sin otro jugador cargado), no hace nada.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class DuplicatePlayerGuard : MonoBehaviour
    {
        private void Awake()
        {
            PlayerMotor[] motors = FindObjectsByType<PlayerMotor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < motors.Length; i++)
            {
                PlayerMotor other = motors[i];
                // Me salto mi propio jugador y cualquiera de mi misma escena: solo cuenta el que viene de otro nivel.
                if (other.transform.root == transform.root || other.gameObject.scene == gameObject.scene) continue;
                gameObject.SetActive(false);
                return;
            }
        }
    }
}

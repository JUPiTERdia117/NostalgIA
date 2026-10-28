// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: WaterFallCushion.cs
// Propósito: amortiguar caídas desde cualquier altura al entrar al océano poco
// profundo de Verano, garantizando que el agua nunca haga daño por caída.
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Player;
using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Volumen disparador en la lámina del agua que frena suavemente la velocidad vertical del
    /// jugador antes de que pise el lecho marino poco profundo.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    [DisallowMultipleComponent]
    public sealed class WaterFallCushion : MonoBehaviour
    {
        [Tooltip("Velocidad vertical máxima hacia abajo permitida tras cruzar el espejo de agua, en m/s (menor que lethalFallSpeed).")]
        [SerializeField, Min(1f)] private float maxDownwardSpeedInWater = 6.5f;

        [Tooltip("Factor de conservación de la velocidad horizontal al chapotear (0 a 1).")]
        [SerializeField, Range(0.2f, 1f)] private float horizontalRetention = 0.85f;

        [Tooltip("Rapidez vertical mínima de entrada para emitir el evento de chapoteo sonoro, en m/s.")]
        [SerializeField, Min(0.5f)] private float minSplashSpeed = 3.5f;

        private float lastSplashTime = float.NegativeInfinity;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            gameObject.layer = Layers.Trigger;
        }

        private void OnTriggerEnter(Collider other) => CushionPlayer(other, true);

        private void OnTriggerStay(Collider other) => CushionPlayer(other, false);

        private void CushionPlayer(Collider other, bool entered)
        {
            if (!other.TryGetComponent(out PlayerMotor motor)) return;

            Vector3 raw = motor.RawVelocity;
            float downwardSpeed = -raw.y;
            if (downwardSpeed <= 0f) return;

            if (entered && downwardSpeed >= minSplashSpeed && Time.time - lastSplashTime > 0.35f)
            {
                lastSplashTime = Time.time;
                EventBus<ShallowWaterEntered>.Raise(new ShallowWaterEntered(other.transform.position, downwardSpeed));
            }

            // Si venía cayendo desde una azotea alta, recorto la componente vertical a un valor seguro
            // mucho menor que los 24 m/s letales de PlayerSettings para que aterrizar en la arena sea inocuo.
            if (downwardSpeed > maxDownwardSpeedInWater)
            {
                float factor = entered ? horizontalRetention : 1f;
                motor.SetVelocity(new Vector3(raw.x * factor, -maxDownwardSpeedInWater, raw.z * factor));
            }
        }
    }
}

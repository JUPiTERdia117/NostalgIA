// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: BounceBubble.cs
// Propósito: burbuja trampolín de la Estación Frutiger Aero (rama experimental):
// al tocarla, lanza al jugador hacia arriba.
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Player;
using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Volumen disparador esférico que impulsa al jugador con <see cref="PlayerMotor.Launch"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La velocidad necesaria para subir una altura h con gravedad g es v = √(2·g·h); en este
    /// módulo se decidió configurar la <b>altura</b> deseada en el inspector (más intuitivo para
    /// diseño de niveles) y calcular la velocidad con la gravedad de <c>PlayerSettings</c>.
    /// </para>
    /// <para>
    /// El collider está en la capa <c>Trigger</c>, que solo interactúa con <c>Player</c>.
    /// Un enfriamiento corto evita rebotes múltiples en el mismo contacto.
    /// La parte visual (deformación y partículas) escucha <see cref="BubbleBounced"/>.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class BounceBubble : MonoBehaviour
    {
        [Tooltip("Altura que alcanza el jugador sobre el punto de rebote, en metros.")]
        [SerializeField, Min(0.5f)] private float launchHeight = 9f;

        [Tooltip("Empuje horizontal en la dirección local +Z de la burbuja, en m/s.")]
        [SerializeField] private float forwardBoost = 0f;

        [Tooltip("Tiempo mínimo entre rebotes, en segundos.")]
        [SerializeField, Min(0f)] private float cooldown = 0.35f;

        private float nextAllowedTime;

        /// <summary>Configura el collider como disparador en la capa de disparadores.</summary>
        private void Awake()
        {
            GetComponent<SphereCollider>().isTrigger = true;
            gameObject.layer = Layers.Trigger;
        }

        /// <summary>Ajusta los parámetros desde el constructor del nivel.</summary>
        public void Configure(float height, float boost)
        {
            launchHeight = Mathf.Max(0.5f, height);
            forwardBoost = boost;
        }

        /// <summary>Impulsa al jugador al entrar en la burbuja.</summary>
        private void OnTriggerEnter(Collider other)
        {
            if (Time.time < nextAllowedTime) return;
            if (!other.TryGetComponent(out PlayerMotor motor)) return;

            float gravity = motor.Settings != null ? motor.Settings.gravity : 20f;
            float speed = Mathf.Sqrt(2f * gravity * launchHeight);   // v = √(2·g·h)
            Vector3 boost = transform.forward * forwardBoost;
            motor.Launch(speed, new Vector3(boost.x, 0f, boost.z));

            nextAllowedTime = Time.time + cooldown;
            EventBus<BubbleBounced>.Raise(new BubbleBounced(transform, other.transform.position, speed));
        }
    }
}

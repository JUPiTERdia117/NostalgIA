// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: Checkpoint.cs
// Propósito: punto de reaparición colocado en la escena.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using UnityEngine;
using UnityEngine.Serialization;

namespace NostalgIA.Gameplay.Player
{
    /// <summary>
    /// Volumen que, al cruzarlo el jugador, se convierte en su punto de reaparición.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La posición del objeto en la escena es la fuente de verdad</b>. El prototipo
    /// reubicaba los checkpoints por código con una tabla de coordenadas fijas; después esa
    /// tabla se aplicó una única vez en el editor y se eliminó del código.
    /// </para>
    /// <para>
    /// El componente reutiliza el GUID del antiguo <c>RespawnController</c>, de modo que las
    /// instancias de la escena se conservaron con sus colliders y luces; el campo
    /// <c>checkpointName</c> se migra a <see cref="label"/> con <see cref="FormerlySerializedAsAttribute"/>.
    /// </para>
    /// <para>
    /// Publica <see cref="CheckpointActivated"/>; <c>RespawnService</c> y el HUD lo escuchan, por
    /// lo que el checkpoint no conoce a ningún otro sistema.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public sealed class Checkpoint : MonoBehaviour
    {
        [Tooltip("Índice del checkpoint dentro de la estación (orden de recorrido).")]
        [SerializeField] private int index = -1;

        [Tooltip("Texto de la notificación al activarse.")]
        [FormerlySerializedAs("checkpointName")]
        [SerializeField] private string label = "¡Punto de control activado!";

        [Tooltip("Elevación sobre el pivote al reaparecer, en metros, para no quedar dentro del piso.")]
        [SerializeField, Min(0f)] private float spawnHeightOffset = 0.2f;

        [Tooltip("Color de la luz del checkpoint al activarse (si tiene una luz hija).")]
        [SerializeField] private Color activatedColor = new Color(0.2f, 1f, 0.4f, 1f);

        private Light indicatorLight;

        /// <summary>Indica si el jugador ya lo activó en esta estación.</summary>
        public bool IsActivated { get; private set; }

        /// <summary>Índice del checkpoint.</summary>
        public int Index => index;

        /// <summary>Posición de reaparición en el mundo.</summary>
        public Vector3 SpawnPosition => transform.position + Vector3.up * spawnHeightOffset;

        /// <summary>Orientación de reaparición (solo el giro horizontal del objeto).</summary>
        public Quaternion SpawnRotation => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        /// <summary>Cachea la luz indicadora.</summary>
        private void Awake() => indicatorLight = GetComponentInChildren<Light>();

        /// <summary>Activa el checkpoint al entrar el jugador (una sola vez por estación).</summary>
        private void OnTriggerEnter(Collider other)
        {
            if (IsActivated || !other.TryGetComponent(out PlayerHealth health) || health.IsDead) return;
            Activate();
        }

        /// <summary>Marca el checkpoint como activo y publica el evento.</summary>
        public void Activate()
        {
            IsActivated = true;
            if (indicatorLight != null) indicatorLight.color = activatedColor;
            EventBus<CheckpointActivated>.Raise(new CheckpointActivated(index, SpawnPosition, SpawnRotation, label));
        }

        /// <summary>Configura el checkpoint desde el constructor de escena.</summary>
        public void Configure(int newIndex, string newLabel)
        {
            index = newIndex;
            label = newLabel;
        }

        /// <summary>Muestra el punto y la dirección de reaparición en la vista de escena.</summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = IsActivated ? activatedColor : new Color(1f, 0.85f, 0.2f, 1f);
            Vector3 spawn = SpawnPosition;
            Gizmos.DrawWireSphere(spawn, 0.3f);
            Gizmos.DrawLine(spawn, spawn + SpawnRotation * Vector3.forward);
        }
    }
}

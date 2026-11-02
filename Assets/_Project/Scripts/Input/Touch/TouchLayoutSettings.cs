// -----------------------------------------------------------------------------
// NostalgIA — Entrada (NostalgIA.Input)
// Archivo: TouchLayoutSettings.cs
// Propósito: parámetros de los controles táctiles dinámicos.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Input.TouchControls
{
    /// <summary>Parámetros de disposición y umbrales de los controles táctiles.</summary>
    [CreateAssetMenu(menuName = "NostalgIA/Entrada/Touch Layout Settings", fileName = "TouchLayoutSettings")]
    public sealed class TouchLayoutSettings : ScriptableObject
    {
        [Tooltip("Radio del pad de movimiento, en unidades del lienzo de referencia (1920 × 1080).")]
        [SerializeField, Min(20f)] private float stickRadius = 110f;

        [Tooltip("Desplazamiento vertical normalizado del pad (0–1) a partir del cual se corre (llevar el pad hasta arriba).")]
        [SerializeField, Range(0.5f, 1f)] private float sprintThreshold = 0.9f;

        [Tooltip("Segundos de pulsación del pad de herramienta que distinguen mantener (absorber) de tocar (disparar).")]
        [SerializeField, Range(0.05f, 1f)] private float holdThreshold = 0.2f;

        [Tooltip("Opacidad de los controles para no ocultar la vista central (45 %).")]
        [SerializeField, Range(0.1f, 1f)] private float opacity = 0.45f;

        /// <summary>Radio del pad de movimiento.</summary>
        public float StickRadius => stickRadius;

        /// <summary>Umbral vertical para correr.</summary>
        public float SprintThreshold => sprintThreshold;

        /// <summary>Umbral de mantener del pad de herramienta.</summary>
        public float HoldThreshold => holdThreshold;

        /// <summary>Opacidad de los controles.</summary>
        public float Opacity => opacity;
    }
}

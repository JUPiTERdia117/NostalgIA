// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: FishSchool.cs
// Propósito: cardumen de peces voladores que gira alrededor de un punto
// (rama experimental Frutiger Aero).
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Presentation.Environment
{
    /// <summary>
    /// Gira el cardumen completo alrededor de su eje vertical y lo hace subir y bajar.
    /// </summary>
    /// <remarks>
    /// Los peces son hijos colocados en anillo por el constructor del nivel, mirando en la
    /// dirección tangente. En este módulo se decidió mover solo la raíz: una escritura de
    /// Transform por frame para todo el cardumen; el aleteo de cada pez lo hace el vértice de
    /// NOS/AeroFish. La altura sigue y(t) = y₀ + A·sin(ω·t + φ).
    /// </remarks>
    public sealed class FishSchool : MonoBehaviour
    {
        [Tooltip("Velocidad angular del cardumen, en grados por segundo (negativa = sentido horario).")]
        [SerializeField] private float angularSpeed = 18f;

        [Tooltip("Amplitud de la ondulación vertical, en metros.")]
        [SerializeField] private float bobAmplitude = 1.2f;

        [Tooltip("Frecuencia angular de la ondulación vertical (rad/s).")]
        [SerializeField] private float bobFrequency = 0.6f;

        private Vector3 basePosition;
        private float phase;

        /// <summary>Guarda la posición base y una fase propia.</summary>
        private void Awake()
        {
            basePosition = transform.position;
            phase = (basePosition.x * 0.37f + basePosition.z * 0.11f) % 6.2831f;
        }

        /// <summary>Gira y ondula.</summary>
        private void Update()
        {
            float t = Time.time;
            transform.SetPositionAndRotation(
                basePosition + Vector3.up * (Mathf.Sin(t * bobFrequency + phase) * bobAmplitude),
                Quaternion.Euler(0f, t * angularSpeed + phase * Mathf.Rad2Deg, 0f));
        }
    }
}

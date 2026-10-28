// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: HazardMotion.cs
// Propósito: movimiento cinemático de los obstáculos de nivel (barredoras
// giratorias, pistones y paredes aplastadoras), sin animaciones ni corrutinas.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Mueve un obstáculo de forma periódica y determinista a partir del tiempo de juego.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Aquí calculé la pose como función pura del tiempo, p(t), en vez de integrarla frame a frame:
    /// así evito que se acumule deriva numérica y mantengo sincronizados todos los aplastadores de un
    /// pasillo según su desfase <see cref="phase"/>. Muevo un <see cref="Rigidbody"/> cinemático con
    /// <c>MovePosition</c>/<c>MoveRotation</c> en <c>FixedUpdate</c> para que los triggers del
    /// obstáculo detecten al jugador aunque se quede quieto esperando.
    /// </para>
    /// <para>No asigna memoria en el bucle de juego: solo aritmética sobre valores.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HazardMotion : MonoBehaviour
    {
        /// <summary>Tipo de movimiento.</summary>
        public enum MotionKind
        {
            /// <summary>Giro continuo alrededor de un eje local.</summary>
            Rotate,
            /// <summary>Vaivén senoidal entre la posición inicial y un desplazamiento.</summary>
            Oscillate,
            /// <summary>Aplastador: espera arriba, cae rápido, espera abajo y sube despacio.</summary>
            Crusher
        }

        [Tooltip("Tipo de movimiento del obstáculo.")]
        [SerializeField] private MotionKind kind = MotionKind.Rotate;

        [Tooltip("Eje de giro en espacio local (solo Rotate).")]
        [SerializeField] private Vector3 rotationAxis = Vector3.up;

        [Tooltip("Velocidad angular en grados por segundo (solo Rotate). Negativa invierte el sentido.")]
        [SerializeField] private float degreesPerSecond = 60f;

        [Tooltip("Desplazamiento máximo respecto a la posición inicial, en metros y en espacio del padre (Oscillate y Crusher).")]
        [SerializeField] private Vector3 travel = new Vector3(3f, 0f, 0f);

        [Tooltip("Duración de un ciclo completo, en segundos (Oscillate y Crusher).")]
        [SerializeField, Min(0.2f)] private float period = 2.5f;

        [Tooltip("Desfase del ciclo, como fracción del periodo (0-1).")]
        [SerializeField, Range(0f, 1f)] private float phase;

        [Tooltip("Aplastador: fracciones del ciclo para espera arriba, caída y espera abajo; el resto es la subida.")]
        [SerializeField] private Vector3 crusherTiming = new Vector3(0.45f, 0.08f, 0.17f);

        private Rigidbody body;
        private Vector3 startLocalPosition;
        private Quaternion startLocalRotation;
        private Vector3 lastWorldPosition;
        private Vector3 linearVelocity;

        /// <summary>Velocidad lineal actual del obstáculo (m/s).</summary>
        public Vector3 LinearVelocity => linearVelocity;

        /// <summary>Indica si el aplastador está en su fase de caída o abajo (peligroso).</summary>
        public bool IsCrushing { get; private set; }

        /// <summary>
        /// Velocidad del punto indicado del obstáculo: v = v_lineal + ω × r, donde ω es la velocidad
        /// angular en rad/s sobre el eje de giro en el mundo y r el vector del pivote al punto.
        /// </summary>
        /// <param name="worldPoint">Punto en el mundo.</param>
        /// <returns>Velocidad del punto en m/s.</returns>
        public Vector3 PointVelocity(Vector3 worldPoint)
        {
            if (kind != MotionKind.Rotate) return linearVelocity;
            Vector3 omega = transform.TransformDirection(rotationAxis.normalized) * (degreesPerSecond * Mathf.Deg2Rad);
            return linearVelocity + Vector3.Cross(omega, worldPoint - transform.position);
        }

        /// <summary>Configura el movimiento desde herramientas de editor.</summary>
        public void Configure(MotionKind newKind, Vector3 axisOrTravel, float speedOrPeriod, float newPhase)
        {
            kind = newKind;
            if (kind == MotionKind.Rotate) { rotationAxis = axisOrTravel; degreesPerSecond = speedOrPeriod; }
            else { travel = axisOrTravel; period = Mathf.Max(0.2f, speedOrPeriod); }
            phase = Mathf.Repeat(newPhase, 1f);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            startLocalPosition = transform.localPosition;
            startLocalRotation = transform.localRotation;
            lastWorldPosition = transform.position;
        }

        private void FixedUpdate()
        {
            float t = Time.time;
            Transform parent = transform.parent;

            if (kind == MotionKind.Rotate)
            {
                // θ(t) = ω·t: calculo el ángulo absoluto para no acumular error de redondeo.
                Quaternion localRotation = startLocalRotation * Quaternion.AngleAxis(degreesPerSecond * t + phase * 360f, rotationAxis);
                body.MoveRotation(parent != null ? parent.rotation * localRotation : localRotation);
                linearVelocity = Vector3.zero;
                return;
            }

            float u = Mathf.Repeat(t / period + phase, 1f);
            float s = kind == MotionKind.Oscillate ? OscillateProfile(u) : CrusherProfile(u);
            Vector3 local = startLocalPosition + travel * s;
            Vector3 world = parent != null ? parent.TransformPoint(local) : local;
            body.MovePosition(world);
            linearVelocity = (world - lastWorldPosition) / Mathf.Max(Time.fixedDeltaTime, 1e-4f);
            lastWorldPosition = world;
        }

        /// <summary>
        /// Vaivén senoidal: s(u) = (1 − cos 2πu) / 2, que va de 0 a 1 y vuelve con velocidad nula en
        /// los extremos (el pistón avisa frenando antes de cambiar de sentido).
        /// </summary>
        private static float OscillateProfile(float u) => 0.5f - 0.5f * Mathf.Cos(u * 2f * Mathf.PI);

        /// <summary>
        /// Perfil del aplastador (s = 0 retraído, 1 cerrado): espera, golpe con aceleración cuadrática
        /// s = x², pausa abajo y retroceso suave s = 1 − smoothstep(x). Dejé la caída corta y el
        /// retroceso más lento para que el jugador lea con claridad la ventana de paso.
        /// </summary>
        private float CrusherProfile(float u)
        {
            float hold = crusherTiming.x, fall = crusherTiming.y, bottom = crusherTiming.z;
            if (u < hold) { IsCrushing = false; return 0f; }
            u -= hold;
            if (u < fall) { IsCrushing = true; float x = u / fall; return x * x; }
            u -= fall;
            if (u < bottom) { IsCrushing = true; return 1f; }
            u -= bottom;
            IsCrushing = false;
            float rise = Mathf.Max(0.001f, 1f - hold - fall - bottom);
            return 1f - Mathf.SmoothStep(0f, 1f, u / rise);
        }
    }
}

// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: MovingPlatform.cs
// Propósito: plataforma móvil con vaivén cosenoidal que transporta al jugador y
// a los objetos absorbibles apoyados encima sin generar asignaciones por frame.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using NostalgIA.Core;
using NostalgIA.Core.Lifecycle;
using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Plataforma que oscila suavemente desde su posición colocada en escena hasta un desplazamiento
    /// relativo <c>travelOffset</c>, exponiendo su avance por frame para <c>PlayerMotor</c>.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class MovingPlatform : MonoBehaviour, IResettable
    {
        [Header("Trayectoria (relativa a su posición inicial)")]
        [Tooltip("Desplazamiento total en metros desde el punto inicial hasta el extremo opuesto del vaivén.")]
        [SerializeField] private Vector3 travelOffset = new Vector3(0f, 0f, 9f);
        [Tooltip("Segundos que tarda en recorrer de ida (medio ciclo).")]
        [SerializeField, Min(0.25f)] private float travelDuration = 3.2f;
        [Tooltip("Desfase inicial del ciclo entre 0 y 1 para alternar dos plataformas vecinas.")]
        [SerializeField, Range(0f, 1f)] private float phaseOffset;

        [Header("Carga superior")]
        [Tooltip("Si está activo, arrastra también objetos con Rigidbody apoyados encima (como orbes absorbibles).")]
        [SerializeField] private bool carryRigidbodies = true;
        [Tooltip("Semidimensiones de la caja de detección sobre la cara superior, en metros.")]
        [SerializeField] private Vector3 cargoHalfExtents = new Vector3(2.4f, 0.7f, 3.2f);
        [Tooltip("Altura del centro de la caja de carga respecto al pivote de la plataforma, en metros.")]
        [SerializeField] private float cargoCenterY = 0.8f;

        // Búfer fijo y conjunto reutilizado para no arrastrar dos veces un mismo Rigidbody con varios colliders.
        private readonly Collider[] cargoHits = new Collider[12];
        private readonly HashSet<int> movedBodies = new HashSet<int>();

        private Vector3 originWorldPosition;
        private float elapsed;
        private Vector3 frameDelta;
        private Vector3 currentVelocity;

        /// <summary>Desplazamiento exacto aplicado en el frame actual, en metros.</summary>
        public Vector3 FrameDelta => frameDelta;

        /// <summary>Velocidad instantánea en el frame actual, en m/s.</summary>
        public Vector3 CurrentVelocity => currentVelocity;

        /// <summary>Configura el desplazamiento relativo y el ritmo desde el constructor del nivel.</summary>
        public void Configure(Vector3 offsetFromStart, float duration, float offset01, Vector3 halfExtents, float centerY)
        {
            travelOffset = offsetFromStart;
            travelDuration = Mathf.Max(0.25f, duration);
            phaseOffset = Mathf.Repeat(offset01, 1f);
            cargoHalfExtents = halfExtents;
            cargoCenterY = centerY;
        }

        private void Awake()
        {
            // Guardo la posición en la que quedó la plataforma en escena como origen A: así cambiar el
            // nivel de región o mover el grupo en el editor arrastra consigo todo el recorrido.
            originWorldPosition = transform.position;
            elapsed = 0f;
            transform.position = EvaluatePosition(0f);
        }

        private void OnEnable() => ResettableRegistry.Register(this);

        private void OnDisable()
        {
            ResettableRegistry.Unregister(this);
            frameDelta = Vector3.zero;
            currentVelocity = Vector3.zero;
        }

        /// <summary>Al reaparecer el jugador devuelvo la plataforma a su arranque para que el salto sea consistente.</summary>
        public void ResetState()
        {
            elapsed = 0f;
            frameDelta = Vector3.zero;
            currentVelocity = Vector3.zero;
            transform.position = EvaluatePosition(0f);
            Physics.SyncTransforms();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                frameDelta = Vector3.zero;
                currentVelocity = Vector3.zero;
                return;
            }

            elapsed += dt;
            Vector3 previous = transform.position;
            Vector3 next = EvaluatePosition(elapsed);

            frameDelta = next - previous;
            currentVelocity = frameDelta / dt;
            transform.position = next;

            // Sincronizo la transformación antes de que corra PlayerMotor (-20) para que la cápsula
            // detecte el collider ya en su posición nueva de este frame.
            Physics.SyncTransforms();

            if (carryRigidbodies && frameDelta.sqrMagnitude > 1e-8f)
            {
                CarryRestingBodies();
            }
        }

        /// <summary>
        /// Curva de vaivén suave: u = (t / T) + fase·2; factor s = 0,5 − 0,5·cos(π·u).
        /// La derivada de cos se anula en los extremos (u = 0, 1, 2...), así que la plataforma frena
        /// con suavidad al llegar a cada orilla en vez de dar un tirón seco.
        /// </summary>
        private Vector3 EvaluatePosition(float timeSeconds)
        {
            float u = (timeSeconds / Mathf.Max(0.25f, travelDuration)) + phaseOffset * 2f;
            float s = 0.5f - 0.5f * Mathf.Cos(u * Mathf.PI);
            return originWorldPosition + travelOffset * s;
        }

        /// <summary>
        /// Desplaza los objetos absorbibles que descansan sobre la plataforma usando OverlapBoxNonAlloc
        /// para mantener cero asignaciones en el bucle de juego.
        /// </summary>
        private void CarryRestingBodies()
        {
            Vector3 boxCenter = transform.TransformPoint(new Vector3(0f, cargoCenterY, 0f));
            int count = Physics.OverlapBoxNonAlloc(
                boxCenter,
                cargoHalfExtents,
                cargoHits,
                transform.rotation,
                Layers.Masks.Absorbable,
                QueryTriggerInteraction.Ignore);

            if (count <= 0) return;

            movedBodies.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider col = cargoHits[i];
                if (col == null) continue;
                Rigidbody body = col.attachedRigidbody;
                if (body == null || body.isKinematic) continue;

                int id = body.GetInstanceID();
                if (!movedBodies.Add(id)) continue;

                body.position += frameDelta;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 a = Application.isPlaying ? originWorldPosition : transform.position;
            Vector3 b = a + travelOffset;
            Gizmos.color = new Color(0.72f, 0.25f, 1f, 0.9f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.35f);
            Gizmos.DrawWireSphere(b, 0.35f);
        }
    }
}

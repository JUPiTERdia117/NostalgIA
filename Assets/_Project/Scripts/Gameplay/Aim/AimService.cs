// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: AimService.cs
// Propósito: muestra de puntería única por frame compartida por las herramientas
// y la retícula (TDD §6.2, tarea F1-A07).
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Core.Events;
using NostalgIA.Core.Services;
using UnityEngine;

namespace NostalgIA.Gameplay.Aim
{
    /// <summary>
    /// Resultado de la puntería de un frame.
    /// </summary>
    /// <remarks>
    /// Es un <c>readonly struct</c> que las herramientas reciben por referencia de solo lectura
    /// (<c>in AimSample</c>); copiarlo o pasarlo no asigna memoria.
    /// </remarks>
    public readonly struct AimSample
    {
        /// <summary>Rayo desde el centro de la cámara.</summary>
        public readonly Ray Ray;
        /// <summary>Indica si el rayo golpeó algo dentro de la distancia de sondeo.</summary>
        public readonly bool HasHit;
        /// <summary>Punto de impacto.</summary>
        public readonly Vector3 Point;
        /// <summary>Normal de la superficie golpeada.</summary>
        public readonly Vector3 Normal;
        /// <summary>Distancia al impacto, en metros.</summary>
        public readonly float Distance;
        /// <summary>Collider golpeado (o <c>null</c>).</summary>
        public readonly Collider Collider;
        /// <summary>Tipo de objetivo para la herramienta activa.</summary>
        public readonly AimTargetKind Kind;

        /// <summary>Crea la muestra.</summary>
        public AimSample(Ray ray, bool hasHit, Vector3 point, Vector3 normal, float distance, Collider collider, AimTargetKind kind)
        {
            Ray = ray;
            HasHit = hasHit;
            Point = point;
            Normal = normal;
            Distance = distance;
            Collider = collider;
            Kind = kind;
        }

        /// <summary>Indica si la muestra es utilizable (hay cámara).</summary>
        public bool IsValid => Ray.direction.sqrMagnitude > 0.5f;

        /// <summary>
        /// Punto de destino para un alcance dado: el impacto si está dentro del alcance; si no,
        /// el punto del rayo a esa distancia. Es el punto hacia el que convergen los disparos.
        /// </summary>
        /// <param name="range">Alcance, en metros.</param>
        public Vector3 EndPoint(float range) => HasHit && Distance <= range ? Point : Ray.GetPoint(range);
    }

    /// <summary>
    /// Calcula una vez por frame la muestra de puntería con un único <c>Physics.Raycast</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El prototipo llamaba a <c>Physics.RaycastAll</c> varias veces por frame (la aspiradora, el
    /// gancho y la retícula por separado), y cada llamada asignaba un arreglo nuevo. Ahora hay un
    /// solo rayo no asignador cuyo resultado consumen todas las herramientas y la retícula.
    /// </para>
    /// <para>
    /// La máscara excluye al jugador, los volúmenes lógicos, los fragmentos, los proyectiles del
    /// jugador e <c>Ignore Raycast</c>, y se ignoran los triggers; así el rayo nunca choca con la
    /// propia cápsula y no hace falta filtrar los resultados por jerarquía.
    /// </para>
    /// <para>
    /// Se ejecuta después de <c>PlayerMotor</c> (orden −20), que aplica la mirada del frame, y
    /// antes de las herramientas (orden 0).
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public sealed class AimService : MonoBehaviour
    {
        /// <summary>Capas que la puntería atraviesa.</summary>
        public const int IgnoredLayers = Layers.Masks.Player | (1 << Layers.IgnoreRaycast) | Layers.Masks.Trigger |
                                         Layers.Masks.Debris | Layers.Masks.PlayerProjectile;

        /// <summary>Máscara de la puntería.</summary>
        public const int AimMask = ~IgnoredLayers;

        [Tooltip("Cámara desde cuyo centro se apunta.")]
        [SerializeField] private Transform aimCamera;

        [Tooltip("Distancia máxima de sondeo, en metros. Debe superar el alcance de todas las herramientas para detectar «fuera de alcance».")]
        [SerializeField, Min(1f)] private float probeDistance = 120f;

        private AimSample current;
        private ToolId activeTool = ToolId.Vacuum;
        private float activeRange = 10f;

        /// <summary>Muestra del frame actual.</summary>
        public ref readonly AimSample Current => ref current;

        /// <summary>Cámara de puntería.</summary>
        public Transform AimCamera => aimCamera;

        /// <summary>Busca la cámara si no se asignó.</summary>
        private void Awake()
        {
            if (aimCamera == null)
            {
                Camera cam = GetComponentInChildren<Camera>(true);
                if (cam != null) aimCamera = cam.transform;
            }
        }

        /// <summary>Registro del servicio.</summary>
        private void OnEnable() => ServiceLocator.Register(this);

        /// <summary>Retiro del registro.</summary>
        private void OnDisable() => ServiceLocator.Unregister(this);

        /// <summary>
        /// Indica a la puntería qué herramienta está activa y su alcance, para clasificar el
        /// objetivo. Lo llama <c>ToolController</c> al cambiar de herramienta o de alcance.
        /// </summary>
        public void SetActiveTool(ToolId tool, float range)
        {
            activeTool = tool;
            activeRange = range;
        }

        /// <summary>Calcula la muestra del frame.</summary>
        private void Update() => Sample();

        /// <summary>Lanza el rayo, clasifica el objetivo y publica el cambio de tipo.</summary>
        public void Sample()
        {
            if (aimCamera == null) return;

            var ray = new Ray(aimCamera.position, aimCamera.forward);
            AimTargetKind previous = current.Kind;
            AimSample next;

            if (Physics.Raycast(ray, out RaycastHit hit, probeDistance, AimMask, QueryTriggerInteraction.Ignore))
            {
                AimTargetKind kind = Classify(hit.collider.gameObject.layer, hit.distance);
                next = new AimSample(ray, true, hit.point, hit.normal, hit.distance, hit.collider, kind);
            }
            else
            {
                next = new AimSample(ray, false, ray.GetPoint(probeDistance), -ray.direction, probeDistance, null, AimTargetKind.None);
            }

            current = next;
            if (next.Kind != previous)
                EventBus<AimTargetChanged>.Raise(new AimTargetChanged(previous, next.Kind));
        }

        /// <summary>
        /// Clasifica el objetivo según la herramienta activa: absorbible para la aspiradora,
        /// superficie de anclaje para el gancho; si está más lejos que el alcance, fuera de alcance.
        /// </summary>
        private AimTargetKind Classify(int layer, float distance)
        {
            bool relevant = activeTool == ToolId.Vacuum ? layer == Layers.Absorbable : layer == Layers.Grapple;
            if (!relevant) return AimTargetKind.None;
            if (distance > activeRange) return AimTargetKind.OutOfRange;
            return activeTool == ToolId.Vacuum ? AimTargetKind.Absorbable : AimTargetKind.Anchor;
        }
    }
}

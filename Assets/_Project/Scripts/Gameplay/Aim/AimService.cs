// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: AimService.cs
// Propósito: muestra de puntería única por frame compartida por las herramientas
// y la retícula.
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

        [Header("Asistencia del gancho")]
        [Tooltip("Ángulo máximo, en grados, entre el centro de la retícula y un ancla para que el gancho la tome igual. Cero la apaga.")]
        [SerializeField, Range(0f, 12f)] private float grappleAssistAngle = 5f;
        [Tooltip("Radio máximo de la búsqueda alrededor del rayo, en metros (limita la ayuda en anclas lejanas).")]
        [SerializeField, Min(0.1f)] private float grappleAssistMaxRadius = 1.6f;

        private const int GrappleMask = 1 << Layers.Grapple;

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

            if (activeTool == ToolId.Grapple && next.Kind != AimTargetKind.Anchor && next.Kind != AimTargetKind.OutOfRange &&
                TryAssistGrapple(ray, out AimSample assisted))
                next = assisted;

            current = next;
            if (next.Kind != previous)
                EventBus<AimTargetChanged>.Raise(new AimTargetChanged(previous, next.Kind));
        }

        /// <summary>
        /// Asistencia de puntería del gancho, parecida a la del Grappleshot de Halo Infinite: si el
        /// centro de la retícula no cae en un ancla pero hay una casi en la mira, el gancho la toma.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 1) Lanzo una esfera de radio <c>r</c> a lo largo del rayo solo contra la capa
        /// <c>Grapple</c>. 2) Del collider que toca tomo el punto más cercano a la línea de mira
        /// (con <c>ClosestPoint</c>, desde el punto del rayo a la misma profundidad). 3) Acepto si el
        /// ángulo entre el rayo y ese punto es ≤ <c>grappleAssistAngle</c>; así la ayuda es un cono
        /// (la misma sensación cerca y lejos) y la esfera solo sirve para encontrar candidatos.
        /// 4) Compruebo línea de vista con un rayo normal para no enganchar a través de paredes.
        /// </para>
        /// <para>
        /// Solo corre con el gancho equipado y cuando el centro no está ya sobre un ancla; usa
        /// consultas sin asignación (<c>SphereCast</c> y <c>Raycast</c> con <c>out</c>).
        /// </para>
        /// </remarks>
        private bool TryAssistGrapple(Ray ray, out AimSample assisted)
        {
            assisted = default;
            if (grappleAssistAngle <= 0f) return false;

            float radius = Mathf.Min(grappleAssistMaxRadius, Mathf.Tan(grappleAssistAngle * Mathf.Deg2Rad) * activeRange);
            if (!Physics.SphereCast(ray, radius, out RaycastHit sphereHit, activeRange, GrappleMask, QueryTriggerInteraction.Ignore))
                return false;

            Collider candidate = sphereHit.collider;
            Vector3 target = sphereHit.point;
            // ClosestPoint solo funciona con cajas, esferas, cápsulas y mallas convexas.
            bool supportsClosest = !(candidate is MeshCollider mesh) || mesh.convex;
            if (supportsClosest)
            {
                float depth = Mathf.Max(0f, Vector3.Dot(sphereHit.point - ray.origin, ray.direction));
                target = candidate.ClosestPoint(ray.GetPoint(depth));
            }

            Vector3 toTarget = target - ray.origin;
            float distance = toTarget.magnitude;
            if (distance < 0.01f || distance > activeRange) return false;
            if (Vector3.Angle(ray.direction, toTarget) > grappleAssistAngle) return false;

            // Línea de vista: lo primero que toca el rayo hacia el punto debe ser el propio ancla.
            Vector3 normal = sphereHit.normal;
            var sightRay = new Ray(ray.origin, toTarget / distance);
            if (Physics.Raycast(sightRay, out RaycastHit sight, distance + 0.05f, AimMask, QueryTriggerInteraction.Ignore))
            {
                if (sight.collider != candidate) return false;
                target = sight.point;
                normal = sight.normal;
                distance = sight.distance;
            }

            assisted = new AimSample(ray, true, target, normal, distance, candidate, AimTargetKind.Anchor);
            return true;
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

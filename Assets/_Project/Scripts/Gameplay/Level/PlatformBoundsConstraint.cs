// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: PlatformBoundsConstraint.cs
// Propósito: mantener a enemigos terrestres (como el WinRAR de la plataforma
// final o bichos en azoteas) dentro de los bordes horizontales de su superficie.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Restringe la posición horizontal (X, Z) de un enemigo al rectángulo útil alrededor de una
    /// superficie de referencia (<see cref="anchorSurface"/>) para evitar que camine en el aire.
    /// </summary>
    [DefaultExecutionOrder(120)]
    [DisallowMultipleComponent]
    public sealed class PlatformBoundsConstraint : MonoBehaviour
    {
        [Tooltip("Superficie cuyo centro sirve como referencia del recinto permitido (si falta, usa el arranque del enemigo).")]
        [SerializeField] private Transform anchorSurface;

        [Tooltip("Desplazamiento horizontal opcional respecto al centro de la superficie.")]
        [SerializeField] private Vector2 centerOffsetXZ;

        [Tooltip("Semiancho (X) y semilargo (Z) permitidos desde el centro, en metros.")]
        [SerializeField] private Vector2 halfExtentsXZ = new Vector2(5f, 7f);

        [Tooltip("Altura mínima relativa al arranque para impedir que se hunda bajo la plataforma.")]
        [SerializeField] private float minRelativeY = -0.35f;

        private Transform initialParent;
        private Vector3 fallbackCenter;
        private float minWorldY;

        /// <summary>Configura el recinto permitido enlazado a su plataforma.</summary>
        public void Configure(Transform surfaceTransform, Vector2 extentsXZ, Vector2 offsetXZ = default, float relativeFloor = -0.35f)
        {
            anchorSurface = surfaceTransform;
            halfExtentsXZ = new Vector2(Mathf.Max(0.5f, extentsXZ.x), Mathf.Max(0.5f, extentsXZ.y));
            centerOffsetXZ = offsetXZ;
            minRelativeY = relativeFloor;
        }

        private void Awake()
        {
            initialParent = transform.parent;
            fallbackCenter = transform.position;
            minWorldY = transform.position.y + minRelativeY;
        }

        private void LateUpdate()
        {
            // Si una araña se emparentó temporalmente a la cámara del jugador, no la fuerzo al piso.
            if (transform.parent != initialParent) return;

            Vector3 refPos = anchorSurface != null ? anchorSurface.position : fallbackCenter;
            float cx = refPos.x + centerOffsetXZ.x;
            float cz = refPos.z + centerOffsetXZ.y;

            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, cx - halfExtentsXZ.x, cx + halfExtentsXZ.x);
            pos.z = Mathf.Clamp(pos.z, cz - halfExtentsXZ.y, cz + halfExtentsXZ.y);
            if (pos.y < minWorldY) pos.y = minWorldY;
            transform.position = pos;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 refPos = anchorSurface != null ? anchorSurface.position : transform.position;
            Gizmos.color = new Color(1f, 0.45f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(refPos.x + centerOffsetXZ.x, transform.position.y + 0.5f, refPos.z + centerOffsetXZ.y),
                                new Vector3(halfExtentsXZ.x * 2f, 1f, halfExtentsXZ.y * 2f));
        }
    }
}

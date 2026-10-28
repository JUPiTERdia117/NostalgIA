// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: PortalMath.cs
// Propósito: geometría compartida de las puertas entre niveles: transferencia de
// poses de un marco a otro y proyección con plano de recorte oblicuo.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Core.Levels
{
    /// <summary>
    /// Funciones puras para enlazar dos marcos de puerta (origen y destino) como si fueran la
    /// misma abertura. Las usan la jugabilidad (teletransporte del jugador) y la presentación
    /// (cámara del portal), por lo que viven en el núcleo y no dependen de ningún componente.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Convención de los marcos: el pivote está en el suelo, en el centro de la abertura, y el eje
    /// Z local (forward) apunta hacia el lado desde el que se mira la puerta. En la puerta de
    /// salida de un nivel ese lado es el del jugador que se acerca; en la entrada del nivel
    /// siguiente es el interior del nivel.
    /// </para>
    /// <para>
    /// Al cruzar, el frente de un marco debe corresponder a la espalda del otro. Por eso la
    /// transferencia incluye un giro de 180° alrededor del eje Y entre ambos espacios locales.
    /// </para>
    /// </remarks>
    public static class PortalMath
    {
        /// <summary>Giro de media vuelta alrededor de Y: R_y(180°) = diag(−1, 1, −1).</summary>
        public static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

        private static readonly Matrix4x4 HalfTurnMatrix = Matrix4x4.Rotate(HalfTurn);

        /// <summary>
        /// Matriz que lleva un punto del espacio del mundo cercano a <paramref name="from"/> al
        /// espacio del mundo cercano a <paramref name="to"/>: M = L_to · R_y(180°) · L_from⁻¹,
        /// con L la matriz local→mundo de cada marco.
        /// </summary>
        /// <param name="from">Marco por el que se entra.</param>
        /// <param name="to">Marco por el que se sale.</param>
        /// <returns>Matriz de transferencia mundo→mundo.</returns>
        public static Matrix4x4 Transfer(Transform from, Transform to)
        {
            return to.localToWorldMatrix * HalfTurnMatrix * from.worldToLocalMatrix;
        }

        /// <summary>
        /// Transfiere una orientación: q' = q_to · R_y(180°) · q_from⁻¹ · q. Se calcula con
        /// cuaterniones (y no extrayendo la rotación de la matriz) para que una escala no unitaria
        /// en la jerarquía no deforme el resultado.
        /// </summary>
        /// <param name="from">Marco de entrada.</param>
        /// <param name="to">Marco de salida.</param>
        /// <param name="rotation">Orientación en el mundo de entrada.</param>
        /// <returns>Orientación equivalente en el mundo de salida.</returns>
        public static Quaternion TransferRotation(Transform from, Transform to, Quaternion rotation)
        {
            return to.rotation * HalfTurn * Quaternion.Inverse(from.rotation) * rotation;
        }

        /// <summary>
        /// Distancia con signo de un punto al plano de la puerta: d = n̂ · (p − o), con n̂ el eje
        /// forward del marco y o su pivote. Positiva en el lado frontal.
        /// </summary>
        /// <param name="frame">Marco de la puerta.</param>
        /// <param name="point">Punto en el mundo.</param>
        /// <returns>Distancia con signo, en metros.</returns>
        public static float SignedDistance(Transform frame, Vector3 point)
        {
            return Vector3.Dot(frame.forward, point - frame.position);
        }

        /// <summary>
        /// Indica si un punto, expresado en el espacio local del marco, cae dentro de la abertura
        /// (|x| ≤ ancho/2 y 0 ≤ y ≤ alto) con un margen opcional.
        /// </summary>
        /// <param name="localPoint">Punto en el espacio local del marco.</param>
        /// <param name="aperture">Ancho y alto de la abertura, en metros.</param>
        /// <param name="margin">Holgura en cada borde, en metros.</param>
        /// <returns><c>true</c> si está dentro de la abertura.</returns>
        public static bool InsideAperture(Vector3 localPoint, Vector2 aperture, float margin)
        {
            return Mathf.Abs(localPoint.x) <= aperture.x * 0.5f + margin &&
                   localPoint.y >= -margin &&
                   localPoint.y <= aperture.y + margin;
        }

        /// <summary>
        /// Ajusta la proyección de <paramref name="camera"/> para que su plano cercano coincida con
        /// el plano del marco de salida (técnica de Lengyel, "oblique near-plane clipping").
        /// </summary>
        /// <remarks>
        /// <para>
        /// Sin este recorte, la cámara del portal dibujaría lo que hay detrás del marco de salida
        /// (entre la cámara virtual y la puerta) y taparía la vista. Se expresa el plano en espacio
        /// de cámara, C = (n, −n·p + ε), con n orientada lejos de la cámara, y se sustituye la fila
        /// del plano cercano de la matriz de proyección.
        /// </para>
        /// <para>
        /// Cuando la cámara está casi sobre el plano (|C.w| pequeño) la matriz oblicua degenera y
        /// produce parpadeos; en ese caso se conserva la proyección normal, porque la cámara ya está
        /// prácticamente en la abertura y no hay nada que recortar.
        /// </para>
        /// </remarks>
        /// <param name="camera">Cámara del portal, ya colocada y con su proyección base.</param>
        /// <param name="planeFrame">Marco cuyo plano actúa como plano cercano.</param>
        /// <param name="offset">Separación del plano, en metros, para evitar recortar el borde.</param>
        public static void ApplyObliqueClip(Camera camera, Transform planeFrame, float offset)
        {
            Matrix4x4 worldToCamera = camera.worldToCameraMatrix;
            Vector3 normal = planeFrame.forward;
            // La normal debe apuntar hacia la zona visible, es decir, lejos de la cámara.
            if (Vector3.Dot(normal, planeFrame.position - camera.transform.position) < 0f) normal = -normal;

            Vector3 cameraSpacePoint = worldToCamera.MultiplyPoint(planeFrame.position);
            Vector3 cameraSpaceNormal = worldToCamera.MultiplyVector(normal).normalized;
            float distance = -Vector3.Dot(cameraSpacePoint, cameraSpaceNormal) + offset;

            if (Mathf.Abs(distance) < 0.05f) return;
            var plane = new Vector4(cameraSpaceNormal.x, cameraSpaceNormal.y, cameraSpaceNormal.z, distance);
            camera.projectionMatrix = camera.CalculateObliqueMatrix(plane);
        }
    }
}

// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: BubbleFieldRenderer.cs
// Propósito: cientos de burbujas flotantes dibujadas con GPU Instancing, sin un
// GameObject por burbuja (rama experimental Frutiger Aero).
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Rendering;

namespace NostalgIA.Presentation.Environment
{
    /// <summary>
    /// Campo de burbujas decorativas: genera una vez las matrices de las instancias dentro de
    /// una caja y las dibuja cada frame con <c>Graphics.RenderMeshInstanced</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rendimiento.</b> Un GameObject por burbuja costaría un Transform, un MeshRenderer y su
    /// culling cada uno. En este módulo se decidió dibujar todas las burbujas en una sola
    /// llamada instanciada: la CPU solo envía el arreglo de matrices (calculado una vez en
    /// <c>OnEnable</c>) y el movimiento (deriva y ondulación) ocurre por completo en el vértice
    /// de NOS/AeroBubble. Cero asignaciones por frame.
    /// </para>
    /// <para>
    /// <b>Distribución.</b> Los radios siguen r = r_min + (r_max − r_min)·u³ con u uniforme: el
    /// cubo concentra la mayoría en burbujas pequeñas y deja pocas grandes, como en una nube
    /// real de burbujas. Las posiciones salen de un generador con semilla fija, así el campo
    /// es idéntico en cada partida y en el editor.
    /// </para>
    /// <para>
    /// <b>Impostores.</b> Con un cuadro (<c>Quad</c>) y el material en modo impostor, cada burbuja
    /// cuesta 2 triángulos en lugar de los 768 de la esfera del motor: el shader orienta el cuadro
    /// hacia la cámara y reconstruye la esfera por píxel. 400 burbujas pasan de ~300 000 a 800
    /// triángulos con el mismo aspecto.
    /// </para>
    /// <para>En móvil el número de burbujas se multiplica por <see cref="mobileCountMultiplier"/>.</para>
    /// </remarks>
    [ExecuteAlways]
    public sealed class BubbleFieldRenderer : MonoBehaviour
    {
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material material;

        [Tooltip("Tamaño de la caja (centrada en este objeto) donde se reparten las burbujas.")]
        [SerializeField] private Vector3 size = new Vector3(120f, 20f, 120f);

        [SerializeField, Range(1, 1000)] private int count = 300;
        [SerializeField, Min(0.05f)] private float minRadius = 0.15f;
        [SerializeField, Min(0.05f)] private float maxRadius = 1.6f;
        [SerializeField] private int seed = 20261027;

        [Tooltip("Multiplicador de cantidad en móvil.")]
        [SerializeField, Range(0.1f, 1f)] private float mobileCountMultiplier = 0.45f;

        private Matrix4x4[] matrices;
        private int activeCount;
        private RenderParams renderParams;

        /// <summary>Genera las instancias y se suscribe al inicio del render de cada cámara.</summary>
        private void OnEnable()
        {
            Build();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        /// <summary>Cancela la suscripción.</summary>
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

        /// <summary>Regenera al cambiar valores en el inspector.</summary>
        private void OnValidate()
        {
            if (isActiveAndEnabled) Build();
        }

        /// <summary>Calcula las matrices de las instancias (única asignación del componente).</summary>
        private void Build()
        {
            if (mesh == null || material == null) return;
            activeCount = Application.isMobilePlatform ? Mathf.Max(1, Mathf.RoundToInt(count * mobileCountMultiplier)) : count;
            if (matrices == null || matrices.Length != activeCount) matrices = new Matrix4x4[activeCount];

            // Generador propio (no UnityEngine.Random) para no alterar el estado aleatorio global.
            var random = new System.Random(seed);
            Vector3 half = size * 0.5f;
            for (int i = 0; i < activeCount; i++)
            {
                var local = new Vector3(
                    Mathf.Lerp(-half.x, half.x, (float)random.NextDouble()),
                    Mathf.Lerp(-half.y, half.y, (float)random.NextDouble()),
                    Mathf.Lerp(-half.z, half.z, (float)random.NextDouble()));
                float u = (float)random.NextDouble();
                float radius = Mathf.Lerp(minRadius, maxRadius, u * u * u);
                matrices[i] = Matrix4x4.TRS(transform.TransformPoint(local), Quaternion.identity, Vector3.one * (radius * 2f));
            }

            renderParams = new RenderParams(material)
            {
                // Límites del campo más el margen de la deriva del shader, para el culling.
                worldBounds = new Bounds(transform.position, size + Vector3.one * (maxRadius * 2f + 4f)),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
            };
        }

        /// <summary>
        /// Envía el dibujo instanciado para la cámara que está por renderizar. Hacerlo aquí, y no
        /// en <c>Update</c>, garantiza que lo reciban todas las cámaras (juego, vista de escena y
        /// capturas con <c>Camera.Render</c>) exactamente una vez por render. El delegado se crea
        /// una sola vez al suscribirse: sin asignaciones por frame.
        /// </summary>
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (matrices == null || activeCount == 0 || material == null || mesh == null) return;
            if (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) return;
            RenderParams parameters = renderParams;
            parameters.camera = camera;
            Graphics.RenderMeshInstanced(parameters, mesh, 0, matrices, activeCount);
        }

        /// <summary>Muestra la caja del campo en la vista de escena.</summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.4f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}

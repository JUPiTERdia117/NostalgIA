// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: LevelGate.cs
// Propósito: puerta entre niveles: detecta cuándo el jugador la cruza e inicia el
// cambio de nivel con pantalla de carga (LevelTransitionRunner).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Core.Levels;
using NostalgIA.Gameplay.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Puerta de salida de un nivel. Cuando la cámara del jugador atraviesa la abertura, inicia el
    /// cambio de nivel con pantalla de carga (<see cref="LevelTransitionRunner"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Antes la puerta precargaba el nivel siguiente durante el Tutorial, lo despertaba por partes
    /// al acercarse y lo mostraba en vivo con una segunda cámara. Eso costaba memoria (dos niveles
    /// cargados), carga en segundo plano y doble renderizado mientras se jugaba. Ahora el nivel
    /// siguiente no existe hasta cruzar: se carga detrás de la pantalla de carga.
    /// </para>
    /// <para>
    /// Si el destino no es jugable en esta versión (no está en <see cref="LevelCatalog"/>) o no
    /// hay destino pero sí un punto de regreso, la puerta es una meta: al cruzarla se publica
    /// <see cref="StationCompleted"/> y, si hace falta, el jugador vuelve a un punto seguro.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class LevelGate : MonoBehaviour
    {
        [Header("Destino")]
        [Tooltip("Nombre de la escena del nivel siguiente (debe estar en la lista de escenas del build).")]
        [SerializeField] private string destinationSceneName = string.Empty;
        [Tooltip("Ruta de la escena; solo se usa en el editor si la escena aún no está en la lista del build.")]
        [SerializeField] private string destinationScenePath = string.Empty;
        [Tooltip("Identificador de la LevelEntrance de llegada. Vacío = primera entrada de la escena.")]
        [SerializeField] private string entranceId = string.Empty;

        [Header("Abertura")]
        [Tooltip("Ancho y alto de la abertura de la puerta, en m. El pivote está en el suelo, en el centro.")]
        [SerializeField] private Vector2 aperture = new Vector2(3.2f, 4.4f);

        [Header("Meta pendiente de enlazar al siguiente nivel")]
        [Tooltip("Índice de esta estación cuando el siguiente nivel aún no existe (1 = Verano).")]
        [SerializeField] private int pendingStationIndex = 1;
        [Tooltip("Punto seguro al que regresa el jugador tras caer en una puerta cuyo nivel destino aún no se construye.")]
        [SerializeField] private Transform pendingNextLevelReturnPoint;
        [Tooltip("Mensaje en el HUD al cruzar la meta mientras el siguiente nivel está pendiente.")]
        [SerializeField] private string pendingCompletionMessage = "» ¡Estación Verano superada! Puerta lista para el siguiente nivel.";

        private PlayerMotor motor;
        private Transform viewer;
        private PersistAcrossLevels[] persistent = new PersistAcrossLevels[0];
        private bool used;

        // Estado del detector de cruce.
        private float previousSide;
        private bool hasPreviousSide;

        /// <summary>
        /// <c>true</c> si la puerta lleva a un nivel que no existe en esta versión (no está en
        /// <see cref="LevelCatalog"/>): entonces es la meta final del juego.
        /// </summary>
        private bool finalGoal;

        /// <summary>Ancho y alto de la abertura, en metros.</summary>
        public Vector2 Aperture => aperture;

        /// <summary><c>true</c> cuando esta puerta enlaza hacia una escena de nivel siguiente y aún no se usó.</summary>
        public bool HasValidDestination =>
            !used && !finalGoal &&
            (!string.IsNullOrEmpty(destinationSceneName) || !string.IsNullOrEmpty(destinationScenePath));

        /// <summary>Nombre de la escena del nivel siguiente configurado en esta puerta.</summary>
        public string DestinationSceneName => destinationSceneName;

        private bool IsPendingGoalOnly =>
            finalGoal ||
            (string.IsNullOrEmpty(destinationSceneName) &&
             string.IsNullOrEmpty(destinationScenePath) &&
             pendingNextLevelReturnPoint != null);

        /// <summary>
        /// Salta de inmediato al siguiente nivel (botón de prueba del menú de pausa).
        /// </summary>
        public bool TriggerImmediateSkip()
        {
            if (!HasValidDestination) return false;
            return BeginTransition();
        }

        /// <summary>Configura la puerta desde un constructor de editor.</summary>
        public void Configure(string sceneName, string scenePath, Vector2 newAperture)
        {
            destinationSceneName = sceneName;
            destinationScenePath = scenePath;
            aperture = newAperture;
        }

        /// <summary>
        /// Deja lista la puerta de meta cuando el siguiente nivel todavía no se construye: detecta el
        /// cruce, avisa que se completó la estación y devuelve al jugador a un punto seguro.
        /// </summary>
        public void ConfigurePendingGoal(Vector2 newAperture, int stationIndex, Transform safeReturnPoint, string message)
        {
            destinationSceneName = string.Empty;
            destinationScenePath = string.Empty;
            aperture = newAperture;
            pendingStationIndex = stationIndex;
            pendingNextLevelReturnPoint = safeReturnPoint;
            if (!string.IsNullOrEmpty(message)) pendingCompletionMessage = message;
        }

        // ------------------------------------------------------------------ ciclo de vida
        /// <summary>Cachea referencias (búsquedas solo en la inicialización).</summary>
        private void Start()
        {
            Scene source = gameObject.scene;
            motor = FindAnyObjectByType<PlayerMotor>();
            Camera main = Camera.main;
            viewer = main != null ? main.transform : (motor != null ? motor.transform : null);

            // Se guardan solo los marcadores de la escena de esta puerta.
            PersistAcrossLevels[] all = FindObjectsByType<PersistAcrossLevels>(FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < all.Length; i++) if (all[i].gameObject.scene == source) count++;
            persistent = new PersistAcrossLevels[count];
            for (int i = 0, j = 0; i < all.Length; i++) if (all[i].gameObject.scene == source) persistent[j++] = all[i];

            finalGoal = !string.IsNullOrEmpty(destinationSceneName) && !LevelCatalog.IsPlayable(destinationSceneName);
        }

        /// <summary>
        /// Detección del cruce en <c>LateUpdate</c>, cuando la cámara ya tiene su posición final del frame.
        /// </summary>
        private void LateUpdate()
        {
            if (viewer == null || used) return;

            if (IsPendingGoalOnly)
            {
                EvaluatePendingGoalCrossing();
                return;
            }
            if (!HasValidDestination) return;

            Vector3 local = transform.InverseTransformPoint(viewer.position);
            // Puerta acostada (se cae dentro de ella o se cruza subiendo con el gancho): más margen en
            // los bordes y en profundidad para no perder el cruce por la velocidad vertical.
            bool upwardGate = Mathf.Abs(transform.forward.y) > 0.5f;
            float margin = upwardGate ? 0.55f : 0.05f;
            float depthRange = upwardGate ? 3.8f : 2.4f;
            bool inside = PortalMath.InsideAperture(local, aperture, margin) && Mathf.Abs(local.z) < depthRange;
            if (!inside)
            {
                hasPreviousSide = false;
                return;
            }

            // Cruce: la cámara pasa del lado frontal (z > 0) al trasero (z ≤ 0) dentro de la abertura.
            if (hasPreviousSide && previousSide > 0f && local.z <= 0f)
            {
                BeginTransition();
                return;
            }
            previousSide = local.z;
            hasPreviousSide = true;
        }

        /// <summary>Inicia el cambio de nivel con pantalla de carga.</summary>
        private bool BeginTransition()
        {
            if (used) return false;
            if (motor == null) motor = FindAnyObjectByType<PlayerMotor>();
            if (!LevelTransitionRunner.Begin(transform, destinationSceneName, destinationScenePath, entranceId,
                                             aperture, persistent, motor))
                return false;
            used = true;
            hasPreviousSide = false;
            return true;
        }

        /// <summary>
        /// Evalúa el cruce cuando la puerta actúa como meta de fin de sesión antes de crear el
        /// siguiente nivel. Amplío el margen en Z a 3,2 m para no perder cruces rápidos en caída libre.
        /// </summary>
        private void EvaluatePendingGoalCrossing()
        {
            Vector3 local = transform.InverseTransformPoint(viewer.position);
            bool inside = PortalMath.InsideAperture(local, aperture, 0.15f) && Mathf.Abs(local.z) < 3.2f;
            if (!inside)
            {
                hasPreviousSide = false;
                return;
            }

            if (hasPreviousSide && previousSide > 0f && local.z <= 0f)
            {
                hasPreviousSide = false;
                if (motor != null && pendingNextLevelReturnPoint != null)
                {
                    motor.Teleport(pendingNextLevelReturnPoint.position, pendingNextLevelReturnPoint.rotation);
                    motor.SetVelocity(Vector3.zero);
                }
                EventBus<StationCompleted>.Raise(new StationCompleted(pendingStationIndex, 1, Time.timeSinceLevelLoad));
                if (!finalGoal && !string.IsNullOrEmpty(pendingCompletionMessage))
                {
                    EventBus<HudMessageRequested>.Raise(new HudMessageRequested(pendingCompletionMessage));
                }
                return;
            }

            previousSide = local.z;
            hasPreviousSide = true;
        }

        /// <summary>Dibuja la abertura y el lado de aproximación.</summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 1f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, aperture.y * 0.5f, 0f), new Vector3(aperture.x, aperture.y, 0.05f));
            Gizmos.DrawLine(new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 2f));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}

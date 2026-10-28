// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: LevelGate.cs
// Propósito: puerta entre niveles sin pantalla de carga: carga el nivel siguiente
// en segundo plano, lo despierta por partes al acercarse, traslada al jugador al
// cruzar y descarga el nivel anterior cuando termina la disolución.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Diagnostics;
using NostalgIA.Core.Events;
using NostalgIA.Core.Levels;
using NostalgIA.Gameplay.Player;
using NostalgIA.Gameplay.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Puerta de salida de un nivel. Gestiona el ciclo de vida del nivel siguiente:
    /// <list type="number">
    /// <item><b>Precarga</b>: <see cref="SceneManager.LoadSceneAsync(string, LoadSceneMode)"/> aditivo
    /// con prioridad de carga baja, unos segundos después de empezar el nivel.</item>
    /// <item><b>Sueño</b>: en cuanto la escena llega, sus objetos se desactivan; no ejecutan
    /// <c>Update</c>, no se dibujan y no simulan física.</item>
    /// <item><b>Despertar por partes</b>: al acercarse el jugador, los objetos se reactivan con un
    /// presupuesto de milisegundos por frame.</item>
    /// <item><b>Cruce</b>: el jugador se traslada al marco de llegada conservando su posición
    /// relativa, su mirada y su velocidad; la escena nueva pasa a ser la activa.</item>
    /// <item><b>Cierre</b>: tras la disolución visual, el nivel anterior se descarga.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Niveles en regiones separadas.</b> Ningún nivel se mueve en tiempo de ejecución: el
    /// motor combina las mallas estáticas (static batching), la navegación y las sondas de luz en
    /// la posición en que se guardaron, y mover la escena cargada las dejaría atrás (ese era el
    /// origen del encimado de niveles). Cada nivel se construye en su propia región del mundo y la
    /// puerta enlaza dos marcos lejanos, como un portal. Así funciona con niveles de cualquier
    /// tamaño; la herramienta <c>NostalgIA › Niveles</c> comprueba la separación entre regiones.
    /// </para>
    /// <para>
    /// <b>Rendimiento.</b> <see cref="Application.backgroundLoadingPriority"/> en
    /// <see cref="ThreadPriority.Low"/> limita a unos 2 ms por frame la integración de los objetos
    /// cargados en el hilo principal, de modo que la carga se reparte en muchos frames sin afectar
    /// al nivel actual. Despertar el nivel por partes reparte también el costo de
    /// <c>OnEnable</c>/<c>Start</c>. Todo el bucle por frame trabaja sobre listas preasignadas y no
    /// genera asignaciones de memoria.
    /// </para>
    /// <para>
    /// La presentación (vista del portal, luz, disolución) escucha los eventos
    /// <see cref="LevelGatePrepared"/>, <see cref="LevelGateAwakeChanged"/>,
    /// <see cref="LevelGateCrossed"/> y <see cref="LevelGateClosed"/>; este componente no la conoce.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class LevelGate : MonoBehaviour
    {
        /// <summary>Fases del ciclo de vida del nivel siguiente.</summary>
        private enum Phase { Idle, Loading, Asleep, Waking, Awake, Sleeping, Crossed, Done }

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

        [Header("Carga en segundo plano")]
        [Tooltip("Espera desde el inicio del nivel antes de empezar la precarga, en s (deja terminar la carga del nivel actual).")]
        [SerializeField, Min(0f)] private float preloadDelay = 2f;
        [Tooltip("Distancia a la que el nivel siguiente despierta, en m.")]
        [SerializeField, Min(1f)] private float wakeDistance = 55f;
        [Tooltip("Distancia a la que vuelve a dormir si el jugador se aleja, en m (mayor que la de despertar: histéresis).")]
        [SerializeField, Min(2f)] private float sleepDistance = 80f;
        [Tooltip("Presupuesto de tiempo por frame para activar o desactivar objetos del nivel siguiente, en ms.")]
        [SerializeField, Range(0.25f, 8f)] private float activationBudgetMs = 1.5f;

        [Header("Transición")]
        [Tooltip("Duración de la disolución del nivel anterior tras cruzar, en s. Después se descarga.")]
        [SerializeField, Range(0.3f, 6f)] private float fadeDuration = 1.8f;

        private Phase phase = Phase.Idle;
        private Scene source;
        private Scene destination;
        private Transform exit;
        private AsyncOperation loadOperation;
        private ThreadPriority previousLoadingPriority;
        private bool priorityChanged;
        private float timer;

        private PlayerMotor motor;
        private VacuumTool vacuum;
        private Transform viewer;
        private PersistAcrossLevels[] persistent = new PersistAcrossLevels[0];

        // Objetos dormidos del nivel siguiente y avance del despertar/dormir por partes.
        private readonly List<GameObject> sleepers = new List<GameObject>(256);
        private readonly List<GameObject> rootBuffer = new List<GameObject>(64);
        private int sliceIndex;

        // Estado del detector de cruce.
        private float previousSide;
        private bool hasPreviousSide;

        /// <summary>Ancho y alto de la abertura, en metros.</summary>
        public Vector2 Aperture => aperture;

        /// <summary>Escena del nivel siguiente (válida tras la precarga).</summary>
        public Scene Destination => destination;

        /// <summary>Marco de llegada en el nivel siguiente (tras la precarga).</summary>
        public Transform Exit => exit;

        /// <summary><c>true</c> cuando el nivel siguiente está completamente despierto.</summary>
        public bool IsDestinationAwake => phase == Phase.Awake;

        /// <summary>Duración de la disolución tras cruzar, en segundos.</summary>
        public float FadeDuration => fadeDuration;

        /// <summary>Configura la puerta desde un constructor de editor.</summary>
        /// <param name="sceneName">Nombre de la escena destino.</param>
        /// <param name="scenePath">Ruta de la escena destino.</param>
        /// <param name="newAperture">Abertura en metros.</param>
        public void Configure(string sceneName, string scenePath, Vector2 newAperture)
        {
            destinationSceneName = sceneName;
            destinationScenePath = scenePath;
            aperture = newAperture;
        }

        // ------------------------------------------------------------------ ciclo de vida
        /// <summary>Cachea referencias (búsquedas solo en la inicialización) y programa la precarga.</summary>
        private void Start()
        {
            source = gameObject.scene;
            motor = FindAnyObjectByType<PlayerMotor>();
            if (motor != null) vacuum = motor.GetComponentInChildren<VacuumTool>(true);
            Camera main = Camera.main;
            viewer = main != null ? main.transform : (motor != null ? motor.transform : null);

            // Se guardan solo los marcadores de la escena de esta puerta.
            PersistAcrossLevels[] all = FindObjectsByType<PersistAcrossLevels>(FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < all.Length; i++) if (all[i].gameObject.scene == source) count++;
            persistent = new PersistAcrossLevels[count];
            for (int i = 0, j = 0; i < all.Length; i++) if (all[i].gameObject.scene == source) persistent[j++] = all[i];

            timer = preloadDelay;
        }

        /// <summary>Restaura la prioridad de carga y cancela la suscripción.</summary>
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RestoreLoadingPriority();
        }

        /// <summary>Máquina de estados del nivel siguiente.</summary>
        private void Update()
        {
            float dt = Time.deltaTime;
            switch (phase)
            {
                case Phase.Idle:
                    timer -= dt;
                    if (timer <= 0f) BeginPreload();
                    break;

                case Phase.Loading:
                    // Respaldo por si la escena llegó sin pasar por sceneLoaded (no debería ocurrir).
                    if (loadOperation != null && loadOperation.isDone && !destination.IsValid()) TryAdoptLoadedScene();
                    break;

                case Phase.Asleep:
                    if (PlayerDistance() < wakeDistance) StartSlicing(Phase.Waking);
                    break;

                case Phase.Waking:
                    if (StepSlicing(true)) SetPhase(Phase.Awake);
                    break;

                case Phase.Awake:
                    if (PlayerDistance() > sleepDistance) StartSlicing(Phase.Sleeping);
                    break;

                case Phase.Sleeping:
                    if (PlayerDistance() < wakeDistance) StartSlicing(Phase.Waking);
                    else if (StepSlicing(false)) SetPhase(Phase.Asleep);
                    break;

                case Phase.Crossed:
                    timer -= dt;
                    if (timer <= 0f) CloseAndUnload();
                    break;
            }
        }

        /// <summary>
        /// Detección del cruce en <c>LateUpdate</c>, cuando la cámara ya tiene su posición final del
        /// frame: así el cambio de nivel ocurre en el mismo frame en que la cámara atraviesa el plano.
        /// </summary>
        private void LateUpdate()
        {
            if (viewer == null || exit == null) return;
            if (phase != Phase.Awake && phase != Phase.Waking && phase != Phase.Sleeping) return;

            Vector3 local = transform.InverseTransformPoint(viewer.position);
            bool inside = PortalMath.InsideAperture(local, aperture, 0.05f) && Mathf.Abs(local.z) < 2f;
            if (!inside)
            {
                hasPreviousSide = false;
                return;
            }

            // Si el jugador llega antes de que termine el despertar por partes, se completa de golpe:
            // nunca se cruza hacia un nivel a medio activar.
            if (phase != Phase.Awake && local.z < 1.2f)
            {
                StartSlicing(Phase.Waking);
                FinishSlicing(true);
                SetPhase(Phase.Awake);
            }

            // Cruce: la cámara pasa del lado frontal (z > 0) al trasero (z ≤ 0) dentro de la abertura.
            if (hasPreviousSide && previousSide > 0f && local.z <= 0f && phase == Phase.Awake)
            {
                Cross();
                return;
            }
            previousSide = local.z;
            hasPreviousSide = true;
        }

        // ------------------------------------------------------------------ precarga
        /// <summary>Inicia la carga aditiva con prioridad baja.</summary>
        private void BeginPreload()
        {
            if (string.IsNullOrEmpty(destinationSceneName) && string.IsNullOrEmpty(destinationScenePath))
            {
                phase = Phase.Done;
                return;
            }

            Scene existing = SceneManager.GetSceneByName(destinationSceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                AdoptDestination(existing);
                return;
            }

            previousLoadingPriority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            priorityChanged = true;
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (Application.CanStreamedLevelBeLoaded(destinationSceneName))
            {
                loadOperation = SceneManager.LoadSceneAsync(destinationSceneName, LoadSceneMode.Additive);
            }
#if UNITY_EDITOR
            else if (!string.IsNullOrEmpty(destinationScenePath))
            {
                // En el editor se permite probar niveles que aún no están en la lista del build.
                loadOperation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    destinationScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            }
#endif
            if (loadOperation == null)
            {
                Debug.LogWarning($"[LevelGate] No se puede cargar la escena '{destinationSceneName}': agrégala a la lista de escenas del build.");
                SceneManager.sceneLoaded -= OnSceneLoaded;
                RestoreLoadingPriority();
                phase = Phase.Done;
                return;
            }
            phase = Phase.Loading;
        }

        /// <summary>
        /// Recibe la escena en el mismo frame en que se activa (después de sus <c>Awake</c> y
        /// <c>OnEnable</c>, antes de sus <c>Start</c>), para dormirla antes de que se dibuje o se
        /// actualice una sola vez.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Additive || scene == source) return;
            bool matches = scene.name == destinationSceneName ||
                           (!string.IsNullOrEmpty(destinationScenePath) && scene.path == destinationScenePath);
            if (!matches) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            AdoptDestination(scene);
        }

        /// <summary>Respaldo de <see cref="OnSceneLoaded"/>.</summary>
        private void TryAdoptLoadedScene()
        {
            Scene scene = SceneManager.GetSceneByName(destinationSceneName);
            if (!scene.IsValid() && !string.IsNullOrEmpty(destinationScenePath)) scene = SceneManager.GetSceneByPath(destinationScenePath);
            if (scene.IsValid() && scene.isLoaded) AdoptDestination(scene);
        }

        /// <summary>
        /// Duerme la escena recién cargada: se desactivan los hijos directos de cada raíz (o la raíz
        /// si no tiene hijos). Se eligió el primer nivel de la jerarquía como unidad de trabajo del
        /// despertar por partes: es lo bastante fino para repartir el costo y no obliga a recorrer
        /// toda la jerarquía.
        /// </summary>
        private void AdoptDestination(Scene scene)
        {
            destination = scene;
            RestoreLoadingPriority();

            sleepers.Clear();
            rootBuffer.Clear();
            scene.GetRootGameObjects(rootBuffer);
            for (int r = 0; r < rootBuffer.Count; r++)
            {
                GameObject root = rootBuffer[r];
                Transform rootTransform = root.transform;
                if (rootTransform.childCount == 0)
                {
                    if (!root.activeSelf) continue;
                    root.SetActive(false);
                    sleepers.Add(root);
                    continue;
                }
                for (int c = 0; c < rootTransform.childCount; c++)
                {
                    GameObject child = rootTransform.GetChild(c).gameObject;
                    if (!child.activeSelf) continue;   // Lo que ya estaba apagado (contenido independiente) se respeta.
                    child.SetActive(false);
                    sleepers.Add(child);
                }
            }

            exit = FindEntrance(scene);
            SetPhase(Phase.Asleep);
            EventBus<LevelGatePrepared>.Raise(new LevelGatePrepared(transform, exit, source, destination, aperture));
        }

        /// <summary>Busca la entrada de llegada (solo en la inicialización del nivel siguiente).</summary>
        private Transform FindEntrance(Scene scene)
        {
            Transform fallback = null;
            for (int r = 0; r < rootBuffer.Count; r++)
            {
                LevelEntrance[] entrances = rootBuffer[r].GetComponentsInChildren<LevelEntrance>(true);
                for (int i = 0; i < entrances.Length; i++)
                {
                    if (fallback == null) fallback = entrances[i].transform;
                    if (string.IsNullOrEmpty(entranceId) || entrances[i].Id == entranceId) return entrances[i].transform;
                }
            }
            if (fallback != null) return fallback;

            Debug.LogWarning($"[LevelGate] La escena '{scene.name}' no tiene LevelEntrance; se usa su primera raíz como llegada.");
            return rootBuffer.Count > 0 ? rootBuffer[0].transform : transform;
        }

        // ------------------------------------------------------------------ despertar por partes
        /// <summary>Cambia a la fase de activación o desactivación por partes desde el principio.</summary>
        private void StartSlicing(Phase target)
        {
            if (phase == target) return;
            bool wasAwake = phase == Phase.Awake;
            phase = target;
            sliceIndex = 0;
            // Al empezar a dormirse la presentación deja de dibujar la vista en ese mismo frame.
            if (wasAwake) EventBus<LevelGateAwakeChanged>.Raise(new LevelGateAwakeChanged(transform, false));
        }

        /// <summary>
        /// Activa o desactiva objetos hasta agotar el presupuesto del frame. El tiempo se mide con
        /// <see cref="Stopwatch.GetTimestamp"/>, que no asigna memoria. Siempre se procesa al menos
        /// un objeto por frame para garantizar el avance.
        /// </summary>
        /// <param name="active">Estado que se aplica.</param>
        /// <returns><c>true</c> al terminar la lista.</returns>
        private bool StepSlicing(bool active)
        {
            long budget = (long)(activationBudgetMs * 0.001 * Stopwatch.Frequency);
            long start = Stopwatch.GetTimestamp();
            while (sliceIndex < sleepers.Count)
            {
                GameObject item = sleepers[sliceIndex++];
                if (item != null && item.activeSelf != active) item.SetActive(active);
                if (Stopwatch.GetTimestamp() - start > budget) break;
            }
            return sliceIndex >= sleepers.Count;
        }

        /// <summary>Termina de golpe la activación o desactivación pendiente.</summary>
        private void FinishSlicing(bool active)
        {
            while (sliceIndex < sleepers.Count)
            {
                GameObject item = sleepers[sliceIndex++];
                if (item != null && item.activeSelf != active) item.SetActive(active);
            }
        }

        /// <summary>Cambia de fase y publica el cambio de vigilia cuando corresponde.</summary>
        private void SetPhase(Phase next)
        {
            bool wasAwake = phase == Phase.Awake;
            phase = next;
            bool isAwake = phase == Phase.Awake;
            if (wasAwake != isAwake || next == Phase.Asleep)
                EventBus<LevelGateAwakeChanged>.Raise(new LevelGateAwakeChanged(transform, isAwake));
        }

        // ------------------------------------------------------------------ cruce
        /// <summary>
        /// Traslada al jugador al marco de llegada. Con M = <see cref="PortalMath.Transfer"/>, la
        /// posición se transforma como punto (p' = M·p), la velocidad como vector (v' = M·v, sin
        /// traslación) y la orientación con <see cref="PortalMath.TransferRotation"/>. Como el
        /// jugador ve exactamente lo mismo antes y después, el cambio es imperceptible.
        /// </summary>
        private void Cross()
        {
            if (motor == null || exit == null) return;

            Matrix4x4 transfer = PortalMath.Transfer(transform, exit);
            Transform player = motor.transform;
            Vector3 newPosition = transfer.MultiplyPoint3x4(player.position);
            Quaternion newRotation = PortalMath.TransferRotation(transform, exit, player.rotation);
            Vector3 newVelocity = transfer.MultiplyVector(motor.RawVelocity);

            motor.Teleport(newPosition, newRotation);
            motor.SetVelocity(newVelocity);

            // El jugador, los objetos marcados y lo que lleva la aspiradora pasan a la escena nueva
            // antes de que la anterior se descargue.
            SceneManager.MoveGameObjectToScene(player.root.gameObject, destination);
            for (int i = 0; i < persistent.Length; i++)
            {
                if (persistent[i] == null) continue;
                GameObject root = persistent[i].transform.root.gameObject;
                if (root.scene == source) SceneManager.MoveGameObjectToScene(root, destination);
            }
            vacuum?.MoveStoredItemsToScene(destination);

            // La escena activa define el cielo, la luz ambiental, la niebla y los reflejos.
            SceneManager.SetActiveScene(destination);

            phase = Phase.Crossed;
            timer = fadeDuration;
            hasPreviousSide = false;

            Vector3 spawnPosition = exit.position + exit.forward * 2f + Vector3.up * 0.1f;
            Quaternion spawnRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(exit.forward, Vector3.up), Vector3.up);
            if (exit.TryGetComponent(out LevelEntrance entrance))
            {
                spawnPosition = entrance.SpawnPosition;
                spawnRotation = entrance.SpawnRotation;
            }

            EventBus<DimensionalPortalCrossed>.Raise(new DimensionalPortalCrossed(true));
            EventBus<LevelGateCrossed>.Raise(new LevelGateCrossed(transform, exit, source, destination,
                                                                  spawnPosition, spawnRotation, fadeDuration));
        }

        /// <summary>Avisa a la presentación y descarga el nivel anterior con prioridad baja.</summary>
        private void CloseAndUnload()
        {
            phase = Phase.Done;
            EventBus<LevelGateClosed>.Raise(new LevelGateClosed(transform));
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            // No se llama a Resources.UnloadUnusedAssets: recorre toda la memoria y produce un pico;
            // los recursos huérfanos se liberan en la siguiente limpieza natural.
            SceneManager.UnloadSceneAsync(source);
        }

        // ------------------------------------------------------------------ utilidades
        /// <summary>Distancia del jugador a la puerta, en metros.</summary>
        private float PlayerDistance()
        {
            Transform target = viewer != null ? viewer : (motor != null ? motor.transform : null);
            return target != null ? Vector3.Distance(target.position, transform.position) : float.MaxValue;
        }

        /// <summary>Devuelve la prioridad de carga a su valor anterior.</summary>
        private void RestoreLoadingPriority()
        {
            if (!priorityChanged) return;
            Application.backgroundLoadingPriority = previousLoadingPriority;
            priorityChanged = false;
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

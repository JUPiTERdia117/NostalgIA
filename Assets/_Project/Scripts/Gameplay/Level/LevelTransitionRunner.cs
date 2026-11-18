// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: LevelTransitionRunner.cs
// Propósito: cambio de nivel con pantalla de carga: oscurece, carga el nivel
// siguiente, lleva al jugador a su entrada, descarga el anterior y precalienta
// los shaders antes de devolver el control.
// -----------------------------------------------------------------------------

using System.Collections;
using NostalgIA.Core.Events;
using NostalgIA.Core.Game;
using NostalgIA.Core.Levels;
using NostalgIA.Core.Services;
using NostalgIA.Gameplay.Player;
using NostalgIA.Gameplay.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Conduce el cambio de nivel detrás de la pantalla de carga.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reemplaza la transición «sin costuras» anterior (precargar el nivel siguiente durante el
    /// Tutorial, despertarlo por partes y mostrarlo con una segunda cámara). Aquí nada del nivel
    /// siguiente existe hasta que el jugador cruza la puerta, así que mientras se juega solo hay
    /// un nivel en memoria y ninguna carga en segundo plano.
    /// </para>
    /// <para>
    /// Pasos, todos con la pantalla tapada:
    /// <list type="number">
    /// <item>Estado <see cref="GameState.Loading"/> (sin entrada) y tiempo detenido, para que nada
    /// del nivel actual dañe al jugador mientras se oscurece.</item>
    /// <item>Carga aditiva con prioridad alta: el jugador espera de todos modos, conviene que
    /// termine cuanto antes.</item>
    /// <item>El jugador, los objetos marcados con <see cref="PersistAcrossLevels"/> y lo que lleva
    /// la aspiradora se mudan a la escena nueva; el jugador aparece en su <see cref="LevelEntrance"/>.
    /// Si la escena trae su propio Player, <c>DuplicatePlayerGuard</c> lo apaga.</item>
    /// <item>Se descarga el nivel anterior y se liberan sus recursos (aquí sí se puede pagar
    /// <see cref="Resources.UnloadUnusedAssets"/>: nadie ve el pico).</item>
    /// <item>La presentación precalienta los shaders del nivel nuevo
    /// (<see cref="LevelTransitionArrived"/> → <see cref="LevelTransitionWarmupDone"/>).</item>
    /// <item>Se devuelve el control y la pantalla se aclara.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Se crea en un objeto propio con <c>DontDestroyOnLoad</c> porque la puerta que lo inicia
    /// vive en la escena que se descarga; al terminar se destruye solo.
    /// </para>
    /// </remarks>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class LevelTransitionRunner : MonoBehaviour
    {
        /// <summary>Segundos que tarda la pantalla en cubrir la imagen.</summary>
        public const float FadeOutDuration = 0.35f;
        /// <summary>Segundos que tarda la pantalla en desaparecer.</summary>
        public const float FadeInDuration = 0.5f;
        /// <summary>Espera máxima al precalentado de shaders, en segundos.</summary>
        private const float WarmupTimeout = 8f;
        /// <summary>Frames que se dibuja el nivel nuevo detrás de la pantalla antes de mostrarlo.</summary>
        private const int HiddenFrames = 3;
        /// <summary>Segundos que se ve «Listo» al 100 % antes de mostrar el nivel.</summary>
        private const float ReadySeconds = 0.6f;

        private static LevelTransitionRunner current;

        /// <summary><c>true</c> mientras hay un cambio de nivel en curso.</summary>
        public static bool IsRunning => current != null;

        private Transform gate;
        private string sceneName;
        private string scenePath;
        private string entranceId;
        private Vector2 aperture;
        private Scene source;
        private PersistAcrossLevels[] persistent;
        private PlayerMotor motor;
        private bool warmupDone;

        /// <summary>
        /// Inicia el cambio de nivel. Devuelve <c>false</c> si ya hay uno en curso.
        /// </summary>
        /// <param name="gate">Puerta que lo inicia (solo como referencia en los eventos).</param>
        /// <param name="sceneName">Escena del nivel siguiente.</param>
        /// <param name="scenePath">Ruta de la escena (solo editor, si no está en la lista del build).</param>
        /// <param name="entranceId">Entrada de llegada; vacío = la primera de la escena.</param>
        /// <param name="aperture">Abertura de la puerta, para la entrada automática de respaldo.</param>
        /// <param name="persistent">Objetos del nivel actual que deben mudarse al siguiente.</param>
        /// <param name="motor">Jugador.</param>
        public static bool Begin(Transform gate, string sceneName, string scenePath, string entranceId,
                                 Vector2 aperture, PersistAcrossLevels[] persistent, PlayerMotor motor)
        {
            if (current != null) return false;

            var host = new GameObject("LevelTransition");
            DontDestroyOnLoad(host);
            current = host.AddComponent<LevelTransitionRunner>();
            current.gate = gate;
            current.sceneName = sceneName;
            current.scenePath = scenePath;
            current.entranceId = entranceId;
            current.aperture = aperture;
            current.source = gate != null ? gate.gameObject.scene : SceneManager.GetActiveScene();
            current.persistent = persistent ?? new PersistAcrossLevels[0];
            current.motor = motor;
            current.StartCoroutine(current.Run());
            return true;
        }

        private void OnEnable() => EventBus<LevelTransitionWarmupDone>.Subscribe(OnWarmupDone);

        private void OnDisable() => EventBus<LevelTransitionWarmupDone>.Unsubscribe(OnWarmupDone);

        private void OnDestroy()
        {
            if (current == this) current = null;
        }

        private void OnWarmupDone(in LevelTransitionWarmupDone evt) => warmupDone = true;

        // ------------------------------------------------------------------ secuencia
        private IEnumerator Run()
        {
            ServiceLocator.TryGet(out GameStateMachine machine);
            machine?.ChangeState(GameState.Loading);
            if (motor != null) motor.SetVelocity(Vector3.zero);
            // El estado de carga deja la escala de tiempo en 1: la detengo para que el nivel actual
            // (enemigos, física, peligros) no siga mientras se oscurece.
            Time.timeScale = 0f;

            EventBus<LevelTransitionStarted>.Raise(new LevelTransitionStarted(sceneName, FadeOutDuration));
            EventBus<LevelTransitionProgress>.Raise(new LevelTransitionProgress(0f));
            yield return new WaitForSecondsRealtime(FadeOutDuration);
            // Un frame más con la pantalla ya opaca antes del primer trabajo pesado.
            yield return null;

            ThreadPriority previousPriority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.High;

            // 1) Carga del nivel siguiente.
            AsyncOperation load = StartLoad();
            if (load == null)
            {
                Debug.LogWarning($"[LevelTransition] No se puede cargar la escena '{sceneName}': agrégala a la lista de escenas del build.");
                Application.backgroundLoadingPriority = previousPriority;
                Finish(machine);
                yield break;
            }
            while (!load.isDone)
            {
                Report(Mathf.Clamp01(load.progress / 0.9f) * 0.6f);
                yield return null;
            }

            Scene destination = FindLoadedDestination();
            if (!destination.IsValid() || !destination.isLoaded)
            {
                Debug.LogWarning($"[LevelTransition] La escena '{sceneName}' se cargó pero no la encuentro.");
                Application.backgroundLoadingPriority = previousPriority;
                Finish(machine);
                yield break;
            }

            // 2) El jugador y lo que lo acompaña se mudan al nivel nuevo.
            Transform exit = FindEntrance(destination);
            Vector3 spawnPosition = exit.position + exit.forward * 2f + Vector3.up * 0.1f;
            Quaternion spawnRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(exit.forward, Vector3.up), Vector3.up);
            if (exit.TryGetComponent(out LevelEntrance entrance))
            {
                spawnPosition = entrance.SpawnPosition;
                spawnRotation = entrance.SpawnRotation;
            }
            MovePlayer(destination, spawnPosition, spawnRotation);
            SceneManager.SetActiveScene(destination);
            Report(0.65f);

            // Mismos avisos que la puerta anterior: progreso del nivel, punto de reaparición,
            // HUD y audio siguen funcionando igual.
            EventBus<DimensionalPortalCrossed>.Raise(new DimensionalPortalCrossed(true));
            EventBus<LevelGateCrossed>.Raise(new LevelGateCrossed(gate, exit, source, destination,
                                                                  spawnPosition, spawnRotation, 0f));

            // 3) Descarga del nivel anterior y de sus recursos.
            if (gate != null) EventBus<LevelGateClosed>.Raise(new LevelGateClosed(gate));
            if (source.IsValid() && source.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(source);
                while (unload != null && !unload.isDone)
                {
                    Report(0.65f + unload.progress * 0.1f);
                    yield return null;
                }
            }
            AsyncOperation cleanup = Resources.UnloadUnusedAssets();
            while (cleanup != null && !cleanup.isDone)
            {
                Report(0.78f);
                yield return null;
            }
            // La basura de la carga se recoge aquí, tapada, y no en medio del juego.
            System.GC.Collect();
            // Las sondas de luz del nivel nuevo se combinan sin las del anterior.
            LightProbes.Tetrahedralize();
            Application.backgroundLoadingPriority = previousPriority;
            Report(0.8f);

            // 4) Precalentado de shaders (lo hace la presentación) con la pantalla aún tapada.
            warmupDone = false;
            EventBus<LevelTransitionArrived>.Raise(new LevelTransitionArrived(destination));
            float waited = 0f;
            while (!warmupDone && waited < WarmupTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            // Unos frames dibujando el nivel real desde la entrada: compila lo que se ve al aparecer.
            for (int i = 0; i < HiddenFrames; i++) yield return null;
            Report(1f);
            // La pantalla muestra «Listo» un momento antes de devolver el control.
            yield return new WaitForSecondsRealtime(ReadySeconds);

            Finish(machine);
        }

        /// <summary>Devuelve el control al jugador y aclara la pantalla.</summary>
        private void Finish(GameStateMachine machine)
        {
            if (machine != null) machine.ChangeState(GameState.Playing);
            Time.timeScale = 1f;
            EventBus<LevelTransitionFinished>.Raise(new LevelTransitionFinished(FadeInDuration));
            Destroy(gameObject);
        }

        private static void Report(float progress) =>
            EventBus<LevelTransitionProgress>.Raise(new LevelTransitionProgress(progress));

        // ------------------------------------------------------------------ carga
        private AsyncOperation StartLoad()
        {
            if (!string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName))
                return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
#if UNITY_EDITOR
            // En el editor se permite probar niveles que aún no están en la lista del build.
            if (!string.IsNullOrEmpty(scenePath))
                return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            return null;
        }

        private Scene FindLoadedDestination()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene == source) continue;
                if (scene.name == sceneName || (!string.IsNullOrEmpty(scenePath) && scene.path == scenePath)) return scene;
            }
            return default;
        }

        /// <summary>Busca la entrada de llegada; si la escena no tiene ninguna, crea una de respaldo.</summary>
        private Transform FindEntrance(Scene scene)
        {
            Transform fallback = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                LevelEntrance[] entrances = roots[r].GetComponentsInChildren<LevelEntrance>(true);
                for (int i = 0; i < entrances.Length; i++)
                {
                    if (fallback == null) fallback = entrances[i].transform;
                    if (string.IsNullOrEmpty(entranceId) || entrances[i].Id == entranceId) return entrances[i].transform;
                }
            }
            if (fallback != null) return fallback;

            var autoEntranceGo = new GameObject("LevelEntrance_Auto");
            SceneManager.MoveGameObjectToScene(autoEntranceGo, scene);
            autoEntranceGo.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, 4.0f), Quaternion.identity);
            LevelEntrance autoEntrance = autoEntranceGo.AddComponent<LevelEntrance>();
            autoEntrance.Configure(string.Empty, aperture);
            return autoEntranceGo.transform;
        }

        /// <summary>
        /// Suelta el gancho, coloca al jugador en la entrada sin velocidad y muda a la escena nueva
        /// al jugador, los objetos persistentes y lo que guarda la aspiradora.
        /// </summary>
        private void MovePlayer(Scene destination, Vector3 position, Quaternion rotation)
        {
            if (motor == null) motor = FindAnyObjectByType<PlayerMotor>();
            if (motor == null) return;

            // El ancla del gancho se queda en el nivel anterior: hay que soltarlo antes de mudarse.
            GrappleTool grapple = motor.GetComponentInChildren<GrappleTool>(true);
            if (grapple != null) grapple.Release();

            motor.Teleport(position, rotation);
            motor.SetVelocity(Vector3.zero);

            SceneManager.MoveGameObjectToScene(motor.transform.root.gameObject, destination);
            for (int i = 0; i < persistent.Length; i++)
            {
                if (persistent[i] == null) continue;
                GameObject root = persistent[i].transform.root.gameObject;
                if (root.scene == source) SceneManager.MoveGameObjectToScene(root, destination);
            }
            VacuumTool vacuum = motor.GetComponentInChildren<VacuumTool>(true);
            vacuum?.MoveStoredItemsToScene(destination);
        }
    }
}

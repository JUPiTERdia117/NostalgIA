// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: LevelLoadingScreen.cs
// Propósito: pantalla de carga de los niveles: tapa la imagen mientras se carga el
// nivel y precarga todo lo posible (shaders, texturas, mallas y audio) antes de
// mostrarlo. Al terminar dice «Listo».
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using NostalgIA.Core.Events;
using NostalgIA.Core.Game;
using NostalgIA.Core.Levels;
using NostalgIA.Core.Services;
using NostalgIA.Presentation.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NostalgIA.Presentation.UI
{
    /// <summary>
    /// Pantalla de carga de todos los niveles: desde el menú, al reiniciar y al cruzar una puerta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Se crea sola al arrancar el juego (no hay que ponerla en ninguna escena) y vive en un objeto
    /// con <c>DontDestroyOnLoad</c>. Todo se arma por código con <see cref="UiKit"/>, con el estilo
    /// de la pausa, y se dibuja encima de cualquier otra interfaz.
    /// </para>
    /// <para>
    /// <b>Qué precarga.</b> Mientras la pantalla está opaca y el tiempo del juego detenido:
    /// <list type="number">
    /// <item>Los datos de audio de las fuentes del nivel (si alguno no se cargó con la escena).</item>
    /// <item>Cada material del nivel (y de los objetos persistentes: jugador, herramientas y pools)
    /// se dibuja una vez delante de la cámara con <see cref="LevelShaderWarmup"/>. Así la GPU
    /// compila sus shaders y sube mallas y texturas aquí, tapado, y no al empezar a jugar (en un
    /// teléfono cada variante nueva cuesta de 5 a 50 ms).</item>
    /// <item>Se liberan los recursos que ya no se usan y se recolecta la basura de la carga, para
    /// que el recolector no interrumpa el juego en los primeros minutos.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>El Tutorial carga más.</b> La primera vez que se entra al Tutorial en la sesión también
    /// se precalientan los shaders de los niveles siguientes, con las listas que guarda el editor
    /// (<see cref="WarmupManifest"/>). Esa carga es más larga, pero después la puerta a Verano
    /// encuentra todo compilado.
    /// </para>
    /// <para>
    /// <b>Barra de progreso.</b> Cada etapa ocupa un tramo y avanza con su trabajo real: el
    /// progreso de la carga de la escena, los audios cargados y los dibujos hechos. Al llegar al
    /// 100 % el título cambia a «Listo» un momento antes de mostrar el nivel.
    /// </para>
    /// <para>
    /// <b>Desde el menú</b> se ve la pantalla de carga del propio menú (SlimUI): el menú le pasa
    /// una <see cref="ILevelLoadingView"/> y esta clase hace la misma precarga, pero manda el
    /// progreso a esa pantalla en vez de mostrar la suya. Al reiniciar y entre niveles se usa la
    /// pantalla de este script.
    /// </para>
    /// <para>
    /// Usa tiempo real (<see cref="Time.unscaledDeltaTime"/>) porque durante la carga el tiempo del
    /// juego está detenido.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Otra pantalla que muestra la carga en lugar de la de <see cref="LevelLoadingScreen"/>
    /// (la usa el menú para que la entrada al Tutorial conserve su pantalla de carga).
    /// </summary>
    /// <remarks>Quien la implementa debe seguir viva al cambiar de escena (<c>DontDestroyOnLoad</c>).</remarks>
    public interface ILevelLoadingView
    {
        /// <summary>Progreso de 0 a 1; <paramref name="ready"/> es <c>true</c> al llegar al 100 %.</summary>
        void SetProgress(float progress, bool ready);

        /// <summary>La escena del nivel ya se activó (sigue tapada mientras se precarga).</summary>
        void OnLevelLoaded();

        /// <summary>Espera, si hace falta, a que el jugador confirme para empezar.</summary>
        IEnumerator WaitToContinue();

        /// <summary>Quita la pantalla: el nivel ya se ve y se puede jugar.</summary>
        void Close();
    }

    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class LevelLoadingScreen : MonoBehaviour
    {
        private const int SortingOrder = 32000;
        private const int DrawsPerFrame = 24;
        private const float ReadySeconds = 0.45f;
        private const float MenuFadeSeconds = 0.25f;
        private static readonly Color BackgroundColor = new Color(0.01f, 0.02f, 0.06f, 1f);

        private static LevelLoadingScreen instance;
        /// <summary>Se precalentaron ya los niveles siguientes en esta sesión.</summary>
        private static bool upcomingLevelsWarmed;

        private Canvas canvas;
        private CanvasGroup group;
        private Image barFill;
        private TextMeshProUGUI title;
        private TextMeshProUGUI percent;
        private string loadingTitle = "Cargando";

        private float alpha;
        private float targetAlpha;
        private float fadeSpeed = 4f;
        private float shownProgress;
        private float targetProgress;

        private readonly LevelShaderWarmup warmup = new LevelShaderWarmup();
        private bool transitionWarming;
        private bool menuLoading;
        private bool preparing;
        /// <summary>Pantalla externa de la carga actual (nula: se usa la propia).</summary>
        private ILevelLoadingView externalView;

        /// <summary><c>true</c> mientras la pantalla está cargando o preparando un nivel.</summary>
        public static bool IsBusy => instance != null && (instance.menuLoading || instance.preparing);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("LevelLoadingScreen");
            DontDestroyOnLoad(go);
            go.AddComponent<LevelLoadingScreen>();
        }

        /// <summary>
        /// Carga un nivel desde el menú y lo precarga todo. Devuelve <c>false</c> si la escena no es
        /// un nivel del catálogo o ya hay una carga en curso (entonces la carga la hace quien llamó).
        /// </summary>
        /// <param name="scene">Escena del nivel.</param>
        /// <param name="view">Pantalla que muestra el progreso; si es nula se usa la de este script.</param>
        public static bool LoadLevel(string scene, ILevelLoadingView view = null)
        {
            if (instance == null || IsBusy || !LevelCatalog.IsPlayable(scene)) return false;
            instance.StartCoroutine(instance.LoadFromMenu(scene, view));
            return true;
        }

        private void Awake()
        {
            instance = this;
            canvas = UiKit.NewCanvas("Canvas", transform, SortingOrder);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            RectTransform root = (RectTransform)canvas.transform;

            // Fondo opaco a pantalla completa; también bloquea los clics a la interfaz de abajo.
            Image background = UiKit.NewImage(root, "Background", Vector2.zero, Vector2.zero, BackgroundColor);
            UiKit.Stretch(background.rectTransform);

            title = UiKit.NewText(root, null, "Cargando", new Vector2(0f, 60f), new Vector2(1200f, 90f), 56f,
                                  TextAlignmentOptions.Center);

            Image track = UiKit.NewImage(root, "BarTrack", new Vector2(0f, -40f), new Vector2(720f, 14f), UiKit.TrackColor);
            barFill = UiKit.NewImage(track.rectTransform, "BarFill", Vector2.zero, Vector2.zero, UiKit.AccentColor);
            barFill.rectTransform.anchorMin = Vector2.zero;
            barFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            barFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            barFill.rectTransform.offsetMin = barFill.rectTransform.offsetMax = Vector2.zero;
            barFill.raycastTarget = false;

            percent = UiKit.NewText(root, null, "0 %", new Vector2(0f, -85f), new Vector2(400f, 50f), 26f,
                                    TextAlignmentOptions.Center);

            SetVisible(0f);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnEnable()
        {
            EventBus<LevelTransitionStarted>.Subscribe(OnStarted);
            EventBus<LevelTransitionProgress>.Subscribe(OnProgress);
            EventBus<LevelTransitionArrived>.Subscribe(OnArrived);
            EventBus<LevelTransitionFinished>.Subscribe(OnFinished);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            EventBus<LevelTransitionStarted>.Unsubscribe(OnStarted);
            EventBus<LevelTransitionProgress>.Unsubscribe(OnProgress);
            EventBus<LevelTransitionArrived>.Unsubscribe(OnArrived);
            EventBus<LevelTransitionFinished>.Unsubscribe(OnFinished);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // ------------------------------------------------------------------ desde el menú o al reiniciar
        /// <summary>Menú → nivel: la carga de la escena es el primer 35 % de la barra.</summary>
        private IEnumerator LoadFromMenu(string scene, ILevelLoadingView view)
        {
            menuLoading = true;
            externalView = view;
            if (view != null)
            {
                // La otra pantalla ya tapa el menú: aquí solo se reinicia la barra.
                shownProgress = targetProgress = 0f;
                UpdateBar();
            }
            else
            {
                Begin(scene, MenuFadeSeconds);
                // Se espera a que la pantalla tape el menú antes de empezar la carga.
                while (alpha < 1f) yield return null;
            }

            AsyncOperation load = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            while (load != null && !load.isDone)
            {
                Report(Mathf.Clamp01(load.progress / 0.9f) * 0.35f);
                yield return null;
            }
            menuLoading = false;
            externalView?.OnLevelLoaded();
            yield return Prepare(SceneManager.GetActiveScene(), 0.35f);
        }

        /// <summary>
        /// Un nivel cargado de golpe (reinicio, o el editor al dar Play) también pasa por la
        /// preparación. La pantalla aparece en este mismo frame para que no se vea el nivel a medias.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || menuLoading || preparing) return;
            if (!LevelCatalog.IsPlayable(scene.name)) return;
            Begin(scene.name, 0f);
            StartCoroutine(Prepare(scene, 0f));
        }

        /// <summary>Precarga el nivel con el tiempo detenido y después lo muestra.</summary>
        private IEnumerator Prepare(Scene scene, float from)
        {
            preparing = true;
            Time.timeScale = 0f;
            // Un frame para que GameRoot fije el estado de la escena nueva; después se pasa a carga.
            yield return null;
            ServiceLocator.TryGet(out GameStateMachine machine);
            machine?.ChangeState(GameState.Loading);
            Time.timeScale = 0f;

            // Lo que se va a precargar: el nivel, los objetos persistentes y, la primera vez que se
            // entra al primer nivel, las listas de los niveles siguientes.
            warmup.Clear();
            warmup.AddScene(scene);
            warmup.AddScene(gameObject.scene);
            var manifests = new List<WarmupManifest>();
            bool firstLevel = LevelCatalog.Levels.Length > 0 && LevelCatalog.Levels[0].Scene == scene.name;
            if (firstLevel && !upcomingLevelsWarmed)
            {
                for (int i = 1; i < LevelCatalog.Levels.Length; i++)
                {
                    WarmupManifest manifest = WarmupManifest.Load(LevelCatalog.Levels[i].Scene);
                    if (manifest == null) continue;
                    manifests.Add(manifest);
                    warmup.AddManifest(manifest);
                }
                upcomingLevelsWarmed = true;
            }

            // Tramos de la barra: audio 10 %, dibujos 75 %, limpieza 15 % (de lo que queda).
            float span = 1f - from;
            yield return PreloadAudio(scene, from, from + span * 0.1f);

            Camera viewer = Camera.main;
            while (viewer != null && !warmup.DrawNext(viewer, 0, viewer, DrawsPerFrame))
            {
                Report(from + span * (0.1f + 0.75f * warmup.Progress));
                yield return null;
            }

            // Las listas cargaron materiales y texturas de otros niveles: se sueltan y se liberan
            // (lo compilado se queda en la caché del driver).
            for (int i = 0; i < manifests.Count; i++) Resources.UnloadAsset(manifests[i]);
            manifests.Clear();
            AsyncOperation cleanup = Resources.UnloadUnusedAssets();
            while (cleanup != null && !cleanup.isDone)
            {
                Report(from + span * 0.9f);
                yield return null;
            }
            System.GC.Collect();

            // Unos frames dibujando el nivel real desde donde aparece el jugador.
            for (int i = 0; i < 3; i++) yield return null;
            Report(1f);
            yield return ShowReady();

            ILevelLoadingView view = externalView;
            if (view != null)
            {
                // La pantalla del menú pide confirmar antes de empezar (como antes).
                yield return view.WaitToContinue();
                externalView = null;
                view.Close();
                // Un frame más: el clic o la tecla que confirmó no debe llegar al juego.
                yield return null;
            }

            preparing = false;
            if (machine != null) machine.ChangeState(GameState.Playing);
            Time.timeScale = 1f;
            if (view == null) Hide(0.5f);
        }

        /// <summary>Carga los datos de audio de las fuentes de las escenas que no lo hayan hecho.</summary>
        private IEnumerator PreloadAudio(Scene scene, float from, float to)
        {
            var clips = new List<AudioClip>();
            CollectClips(scene, clips);
            CollectClips(gameObject.scene, clips);
            for (int i = 0; i < clips.Count; i++)
                if (clips[i].loadState == AudioDataLoadState.Unloaded) clips[i].LoadAudioData();

            int loaded = 0;
            while (loaded < clips.Count)
            {
                loaded = 0;
                for (int i = 0; i < clips.Count; i++)
                    if (clips[i].loadState != AudioDataLoadState.Loading) loaded++;
                Report(Mathf.Lerp(from, to, clips.Count > 0 ? (float)loaded / clips.Count : 1f));
                if (loaded < clips.Count) yield return null;
            }
        }

        private static void CollectClips(Scene scene, List<AudioClip> clips)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            var sources = new List<AudioSource>(64);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                root.GetComponentsInChildren(true, sources);
                for (int i = 0; i < sources.Count; i++)
                {
                    AudioClip clip = sources[i].clip;
                    if (clip != null && !clips.Contains(clip)) clips.Add(clip);
                }
            }
        }

        // ------------------------------------------------------------------ puerta entre niveles
        private void OnStarted(in LevelTransitionStarted evt) => Begin(evt.DestinationName, evt.FadeOutDuration);

        private void OnProgress(in LevelTransitionProgress evt) => Report(evt.Progress);

        /// <summary>El nivel nuevo ya está cargado tras la puerta: audio y shaders, del 80 al 98 %.</summary>
        private void OnArrived(in LevelTransitionArrived evt)
        {
            warmup.Clear();
            warmup.AddScene(evt.Destination);
            StartCoroutine(WarmTransition(evt.Destination));
        }

        private IEnumerator WarmTransition(Scene destination)
        {
            transitionWarming = true;
            yield return PreloadAudio(destination, 0.8f, 0.83f);
            Camera viewer = Camera.main;
            while (viewer != null && !warmup.DrawNext(viewer, 0, viewer, DrawsPerFrame))
            {
                Report(0.83f + 0.15f * warmup.Progress);
                yield return null;
            }
            transitionWarming = false;
            EventBus<LevelTransitionWarmupDone>.Raise(default);
        }

        private void OnFinished(in LevelTransitionFinished evt)
        {
            if (transitionWarming)
            {
                StopAllCoroutines();
                transitionWarming = false;
            }
            Hide(evt.FadeInDuration);
        }

        // ------------------------------------------------------------------ pantalla
        /// <summary>Muestra la pantalla («Cargando …») desde 0 %; con 0 s aparece en este mismo frame.</summary>
        private void Begin(string scene, float fadeSeconds)
        {
            loadingTitle = LevelCatalog.TryGet(scene, out LevelCatalog.Level level) ? "Cargando " + level.Title : "Cargando";
            title.text = loadingTitle;
            shownProgress = targetProgress = 0f;
            UpdateBar();
            targetAlpha = 1f;
            if (fadeSeconds <= 0f) SetVisible(1f);
            else fadeSpeed = 1f / fadeSeconds;
        }

        private void Hide(float fadeSeconds)
        {
            targetProgress = 1f;
            targetAlpha = 0f;
            fadeSpeed = 1f / Mathf.Max(0.01f, fadeSeconds);
        }

        /// <summary>Espera a que la barra llegue al 100 % y deja ver «Listo» un momento.</summary>
        private IEnumerator ShowReady()
        {
            while (shownProgress < 1f) yield return null;
            yield return new WaitForSecondsRealtime(ReadySeconds);
        }

        private void Report(float progress) => targetProgress = Mathf.Max(targetProgress, Mathf.Clamp01(progress));

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (!Mathf.Approximately(alpha, targetAlpha))
            {
                alpha = Mathf.MoveTowards(alpha, targetAlpha, fadeSpeed * dt);
                SetVisible(alpha);
            }

            if ((alpha > 0f || externalView != null) && shownProgress < targetProgress)
            {
                // La barra se acerca rápido al valor real para no dar saltos ni quedarse atrás.
                shownProgress = Mathf.MoveTowards(shownProgress, targetProgress, Mathf.Max(1.5f, (targetProgress - shownProgress) * 6f) * dt);
                UpdateBar();
            }
        }

        private void UpdateBar()
        {
            barFill.rectTransform.anchorMax = new Vector2(shownProgress, 1f);
            bool ready = shownProgress >= 0.999f;
            percent.text = ready ? "100 %" : Mathf.FloorToInt(shownProgress * 100f) + " %";
            title.text = ready ? "Listo" : loadingTitle;
            externalView?.SetProgress(shownProgress, ready);
        }

        private void SetVisible(float value)
        {
            alpha = value;
            group.alpha = value;
            group.blocksRaycasts = value > 0.01f;
            group.interactable = false;
            // Oculta no cuesta nada: el canvas apagado no se reconstruye ni se dibuja.
            canvas.enabled = value > 0f;
        }
    }
}

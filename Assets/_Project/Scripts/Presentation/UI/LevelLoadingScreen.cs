// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: LevelLoadingScreen.cs
// Propósito: pantalla de carga entre niveles: tapa la imagen mientras se carga el
// nivel siguiente y precalienta sus shaders antes de mostrarlo.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Core.Levels;
using NostalgIA.Presentation.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NostalgIA.Presentation.UI
{
    /// <summary>
    /// Pantalla de carga que acompaña a <c>LevelTransitionRunner</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Se crea sola al arrancar el juego (no hay que ponerla en ninguna escena) y vive en un objeto
    /// con <c>DontDestroyOnLoad</c>. Todo se arma por código con <see cref="UiKit"/>, con el estilo
    /// de la pausa, y se dibuja encima de cualquier otra interfaz.
    /// </para>
    /// <para>
    /// <b>Precalentado.</b> Cuando el nivel nuevo ya está cargado y la pantalla sigue opaca
    /// (<see cref="LevelTransitionArrived"/>), dibuja cada material del nivel una vez delante de la
    /// cámara del jugador con <see cref="LevelShaderWarmup"/>. La primera vez que la GPU usa un
    /// shader tiene que prepararlo (de 5 a 50 ms por variante en un teléfono); así ese costo se
    /// paga aquí, tapado, y no al empezar a jugar. Al terminar avisa con
    /// <see cref="LevelTransitionWarmupDone"/>.
    /// </para>
    /// <para>
    /// Usa tiempo real (<see cref="Time.unscaledDeltaTime"/>) porque la transición detiene el tiempo del juego.
    /// </para>
    /// </remarks>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class LevelLoadingScreen : MonoBehaviour
    {
        private const int SortingOrder = 32000;
        private const int DrawsPerFrame = 24;
        private static readonly Color BackgroundColor = new Color(0.01f, 0.02f, 0.06f, 1f);

        private Canvas canvas;
        private CanvasGroup group;
        private Image barFill;
        private TextMeshProUGUI title;
        private TextMeshProUGUI percent;

        private float alpha;
        private float targetAlpha;
        private float fadeSpeed = 4f;
        private float shownProgress;
        private float targetProgress;

        private LevelShaderWarmup warmup;
        private bool warming;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("LevelLoadingScreen");
            DontDestroyOnLoad(go);
            go.AddComponent<LevelLoadingScreen>();
        }

        private void Awake()
        {
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

        private void OnEnable()
        {
            EventBus<LevelTransitionStarted>.Subscribe(OnStarted);
            EventBus<LevelTransitionProgress>.Subscribe(OnProgress);
            EventBus<LevelTransitionArrived>.Subscribe(OnArrived);
            EventBus<LevelTransitionFinished>.Subscribe(OnFinished);
        }

        private void OnDisable()
        {
            EventBus<LevelTransitionStarted>.Unsubscribe(OnStarted);
            EventBus<LevelTransitionProgress>.Unsubscribe(OnProgress);
            EventBus<LevelTransitionArrived>.Unsubscribe(OnArrived);
            EventBus<LevelTransitionFinished>.Unsubscribe(OnFinished);
        }

        // ------------------------------------------------------------------ eventos
        private void OnStarted(in LevelTransitionStarted evt)
        {
            title.text = LevelCatalog.TryGet(evt.DestinationName, out LevelCatalog.Level level)
                ? "Cargando " + level.Title
                : "Cargando";
            shownProgress = targetProgress = 0f;
            UpdateBar();
            targetAlpha = 1f;
            fadeSpeed = 1f / Mathf.Max(0.01f, evt.FadeOutDuration);
        }

        private void OnProgress(in LevelTransitionProgress evt) => targetProgress = Mathf.Max(targetProgress, evt.Progress);

        private void OnArrived(in LevelTransitionArrived evt)
        {
            Camera viewer = Camera.main;
            warmup ??= new LevelShaderWarmup();
            warmup.Collect(evt.Destination);
            warmedDraws = 0;
            warming = viewer != null && warmup.Count > 0;
            if (!warming) EventBus<LevelTransitionWarmupDone>.Raise(default);
        }

        private void OnFinished(in LevelTransitionFinished evt)
        {
            warming = false;
            targetProgress = 1f;
            targetAlpha = 0f;
            fadeSpeed = 1f / Mathf.Max(0.01f, evt.FadeInDuration);
        }

        // ------------------------------------------------------------------ frame
        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (warming) StepWarmup();

            if (!Mathf.Approximately(alpha, targetAlpha))
            {
                alpha = Mathf.MoveTowards(alpha, targetAlpha, fadeSpeed * dt);
                SetVisible(alpha);
            }

            if (alpha > 0f && shownProgress < targetProgress)
            {
                // La barra se acerca suavemente al valor real para no dar saltos.
                shownProgress = Mathf.MoveTowards(shownProgress, targetProgress, 1.5f * dt);
                UpdateBar();
            }
        }

        /// <summary>Dibuja unos cuantos materiales por frame delante de la cámara, tapados por la pantalla.</summary>
        private void StepWarmup()
        {
            Camera viewer = Camera.main;
            if (viewer == null || warmup.DrawNext(viewer, 0, viewer, DrawsPerFrame))
            {
                warming = false;
                EventBus<LevelTransitionWarmupDone>.Raise(default);
                return;
            }
            // El precalentado ocupa el último tramo de la barra.
            OnProgress(new LevelTransitionProgress(0.8f + 0.18f * WarmupFraction()));
        }

        private float WarmupFraction()
        {
            // LevelShaderWarmup no expone el índice; aproximo con los dibujos hechos.
            warmedDraws += DrawsPerFrame;
            return warmup.Count > 0 ? Mathf.Clamp01((float)warmedDraws / warmup.Count) : 1f;
        }

        private int warmedDraws;

        private void UpdateBar()
        {
            barFill.rectTransform.anchorMax = new Vector2(shownProgress, 1f);
            percent.text = Mathf.RoundToInt(shownProgress * 100f) + " %";
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

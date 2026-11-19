// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: GameRoot.cs
// Propósito: raíz persistente de servicios: estado de juego, entrada y pools.
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Core.Events;
using NostalgIA.Core.Game;
using NostalgIA.Core.Pooling;
using NostalgIA.Core.Services;
using NostalgIA.Gameplay.Level;
using NostalgIA.Gameplay.Player;
using NostalgIA.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.Gameplay.Bootstrap
{
    /// <summary>
    /// Raíz de composición persistente (<c>DontDestroyOnLoad</c>) que crea y registra los servicios.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Instanciación.</b> El prefab vive en <c>Resources/GameRoot</c> y se instancia mediante
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/> con
    /// <see cref="RuntimeInitializeLoadType.BeforeSceneLoad"/>, es decir, antes de cargar
    /// <c>00_Boot</c> en el build. En este módulo se decidió no colocarlo como objeto de escena
    /// porque así también existe al probar una estación directamente desde el editor, y nunca se
    /// duplica al volver a cargar <c>00_Boot</c>. Si el prefab no existe, se crea una raíz mínima
    /// con un <see cref="InputReader"/> que usa las acciones de todo el proyecto.
    /// </para>
    /// <para>
    /// <b>Servicios registrados:</b> <see cref="GameStateMachine"/>, <see cref="PoolService"/> e
    /// <see cref="InputReader"/>. Los componentes de presentación hijos del prefab (audio, VFX
    /// globales) se registran por su cuenta en su <c>Awake</c>, sin que este ensamblado los conozca.
    /// </para>
    /// <para>
    /// <b>Estados.</b> Cargar <c>00_Boot</c> lleva al estado <see cref="GameState.Boot"/>, una escena con jugador al estado
    /// <see cref="GameState.Playing"/> y una escena sin jugador (un menú) al estado
    /// <see cref="GameState.MainMenu"/>. La pausa se alterna con la acción <c>Pause</c>.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour
    {
        /// <summary>Ruta del prefab dentro de una carpeta <c>Resources</c>.</summary>
        public const string ResourcePath = "GameRoot";

        [Tooltip("Lector de entrada compartido.")]
        [SerializeField] private InputReader inputReader;

        [Tooltip("Pools que se crean al arrancar para que su primer uso no instancie objetos.")]
        [SerializeField] private PoolDefinition[] prewarmPools = new PoolDefinition[0];

        private GameStateMachine stateMachine;
        private PoolService pools;

        /// <summary>Instancia activa (una por sesión).</summary>
        public static GameRoot Instance { get; private set; }

        /// <summary>Máquina de estados global.</summary>
        public GameStateMachine StateMachine => stateMachine;

        /// <summary>Lector de entrada.</summary>
        public InputReader Reader => inputReader;

        /// <summary>Crea la raíz antes de cargar la primera escena.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameRoot prefab = Resources.Load<GameRoot>(ResourcePath);
            if (prefab != null)
            {
                Instantiate(prefab).name = "GameRoot";
            }
            else
            {
                Debug.LogWarning("[GameRoot] No existe Resources/GameRoot; se crea una raíz mínima. Revise que el prefab GameRoot esté en Prefabs/Core/Resources.");
                new GameObject("GameRoot").AddComponent<GameRoot>();
            }
        }

        /// <summary>Crea y registra los servicios.</summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            stateMachine = new GameStateMachine();
            pools = new PoolService(transform);
            if (inputReader == null) inputReader = ScriptableObject.CreateInstance<InputReader>();
            inputReader.Initialize();

            ServiceLocator.Register(this);
            ServiceLocator.Register(stateMachine);
            ServiceLocator.Register(pools);
            ServiceLocator.Register(inputReader);

            for (int i = 0; i < prewarmPools.Length; i++)
            {
                if (prewarmPools[i] != null) pools.Prewarm(prewarmPools[i]);
            }

            inputReader.PausePressed += OnPausePressed;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        /// <summary>Libera servicios al cerrar la sesión de juego.</summary>
        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (inputReader != null)
            {
                inputReader.PausePressed -= OnPausePressed;
                inputReader.Shutdown();
            }
            ServiceLocator.Unregister(this);
            ServiceLocator.Unregister(stateMachine);
            ServiceLocator.Unregister(pools);
            ServiceLocator.Unregister(inputReader);
            Time.timeScale = 1f;
            Instance = null;
        }

        /// <summary>
        /// Asigna el estado según la escena cargada (solo cargas únicas, no aditivas):
        /// <c>00_Boot</c> → Boot; una escena con jugador (<see cref="PlayerMotor"/>) → Playing;
        /// cualquier otra, como un menú, → MainMenu.
        /// </summary>
        /// <remarks>
        /// Antes toda escena que no fuera Boot pasaba a Playing, y Playing bloquea y esconde el cursor
        /// en PC, así que en una escena de menú no se podía hacer clic en ningún botón. Ahora reviso si
        /// la escena tiene jugador (incluido el que espera desactivado en los niveles que se abren
        /// solos): si no lo tiene, es un menú y el cursor queda libre.
        /// </remarks>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;
            GameState next;
            if (scene.name == SceneNames.Boot) next = GameState.Boot;
            else if (FindAnyObjectByType<PlayerMotor>(FindObjectsInactive.Include) != null) next = GameState.Playing;
            else next = GameState.MainMenu;
            stateMachine.ChangeState(next);
        }

        /// <summary>Devuelve a sus pools los objetos en uso al descargar una escena.</summary>
        private void OnSceneUnloaded(Scene scene) => pools.ReleaseAll();

        /// <summary>Alterna la pausa.</summary>
        private void OnPausePressed() => stateMachine.TogglePause();

        /// <summary>El sistema operativo libera el cursor al perder el foco; se reaplica al volver.</summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (stateMachine == null) return;
            if (hasFocus) stateMachine.ApplyEffects();
            else inputReader?.ResetTransientState();
        }

        /// <summary>En móvil, enviar la aplicación a segundo plano pausa el juego.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused && stateMachine != null && stateMachine.State == GameState.Playing)
                stateMachine.ChangeState(GameState.Paused);
        }
    }
}

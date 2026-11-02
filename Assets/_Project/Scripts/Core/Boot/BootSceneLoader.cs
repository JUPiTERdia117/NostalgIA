// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: BootSceneLoader.cs
// Propósito: componente de la escena 00_Boot. Carga de forma asíncrona la primera
// escena jugable. Esta escena instancia además el prefab persistente
// GameRoot con los servicios del juego.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.Core.Boot
{
    /// <summary>
    /// Carga la primera escena jugable desde la escena de arranque <c>00_Boot</c>.
    /// </summary>
    /// <remarks>
    /// En este módulo se decidió que la escena de arranque sea la primera del build para tener
    /// un punto único donde inicializar servicios antes de cualquier nivel. La carga se hace con
    /// <see cref="SceneManager.LoadSceneAsync(string, LoadSceneMode)"/> en modo
    /// <see cref="LoadSceneMode.Single"/>: la escena de arranque se descarga al terminar, de modo
    /// que no queda ningún objeto suyo consumiendo memoria durante el juego.
    /// El componente no implementa <c>Update</c>; la consulta del progreso se hace bajo demanda,
    /// por lo que no tiene costo por frame.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("NostalgIA/Core/Boot Scene Loader")]
    public sealed class BootSceneLoader : MonoBehaviour
    {
        /// <summary>
        /// Fracción en la que Unity detiene el progreso de una carga asíncrona antes de activar
        /// la escena. El valor de <c>AsyncOperation.progress</c> avanza de 0 a 0,9 mientras se leen
        /// los datos y salta a 1 al activarse.
        /// </summary>
        private const float UnityLoadCompleteThreshold = 0.9f;

        [Tooltip("Nombre de la primera escena jugable que se cargará al iniciar. Debe estar incluida en Build Settings.")]
        [SerializeField] private string firstSceneName = SceneNames.Tutorial;

        [Tooltip("Si está activo, se carga la escena en Start de forma automática. Desactivarlo permite que otro sistema (por ejemplo, el menú principal) decida cuándo cargar.")]
        [SerializeField] private bool loadOnStart = true;

        private AsyncOperation loadOperation;

        /// <summary>Nombre de la escena que se cargará.</summary>
        public string FirstSceneName => firstSceneName;

        /// <summary>Indica si ya se inició la carga.</summary>
        public bool IsLoading => loadOperation != null;

        /// <summary>
        /// Progreso normalizado de la carga entre 0 y 1, pensado para una futura pantalla de carga.
        /// </summary>
        /// <remarks>
        /// Se normaliza el progreso de Unity dividiéndolo entre 0,9:
        /// <c>p = saturate(progress / 0,9)</c>. Así la barra llega a 1 cuando los datos ya están
        /// listos, sin esperar el salto final de activación.
        /// </remarks>
        public float Progress01 => loadOperation == null
            ? 0f
            : Mathf.Clamp01(loadOperation.progress / UnityLoadCompleteThreshold);

        /// <summary>Inicia la carga automática si está configurada.</summary>
        private void Start()
        {
            if (loadOnStart)
            {
                LoadFirstScene();
            }
        }

        /// <summary>
        /// Inicia la carga asíncrona de la primera escena jugable. Las llamadas repetidas se ignoran.
        /// </summary>
        /// <returns><c>true</c> si la carga se inició; <c>false</c> si ya estaba en curso o la escena no existe en el build.</returns>
        public bool LoadFirstScene()
        {
            if (loadOperation != null)
            {
                return false;
            }

            // Se verifica antes de cargar que la escena esté registrada en Build Settings.
            // Sin esta comprobación, LoadSceneAsync solo devolvería null y el juego quedaría
            // detenido en la escena de arranque sin un mensaje claro.
            if (string.IsNullOrEmpty(firstSceneName) || !Application.CanStreamedLevelBeLoaded(firstSceneName))
            {
                Debug.LogError($"[BootSceneLoader] La escena '{firstSceneName}' no está incluida en Build Settings.", this);
                return false;
            }

            loadOperation = SceneManager.LoadSceneAsync(firstSceneName, LoadSceneMode.Single);
            return loadOperation != null;
        }
    }
}

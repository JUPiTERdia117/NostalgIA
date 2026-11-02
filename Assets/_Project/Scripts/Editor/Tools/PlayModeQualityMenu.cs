// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: PlayModeQualityMenu.cs
// Propósito: que el modo de juego del editor use la misma calidad que estaba
// seleccionada al darle Play, con opción para previsualizar la de la plataforma.
// -----------------------------------------------------------------------------

using NostalgIA.Presentation.VFX;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NostalgIA.EditorTools
{
    /// <summary>
    /// Mantiene en el modo de juego la calidad que tenía el editor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Al darle Play, Unity cambia a la calidad por defecto de la plataforma activa. Con la
    /// plataforma en Android eso es «Mobile» (sin HDR y a 80 % de resolución), así que el
    /// postprocesado que se veía en la escena antes de jugar casi desaparecía: sin HDR la imagen se
    /// recorta a 1, el bloom ya no tiene brillo de sobra y el tonemapping no hace nada.
    /// </para>
    /// <para>
    /// Guardo el asset de URP del editor al salir del modo edición y lo pongo en cuanto Unity termina
    /// de entrar al modo de juego (intentarlo antes no sirve: Unity vuelve a poner el de la
    /// plataforma); al salir del modo de juego regreso el original. Los
    /// componentes LDR (<see cref="LdrVolumeProfileSwap"/>, <see cref="LdrEmissionFallback"/>)
    /// escuchan el cambio de asset de URP y regresan a sus versiones HDR; aun así les pido un
    /// <c>Refresh</c> por si acaso. En los builds no hay nada de esto: cada dispositivo usa su calidad.
    /// </para>
    /// <para>
    /// Desmarcando <c>NostalgIA ▸ Calidad ▸ Jugar con la calidad del editor</c> se juega con la
    /// calidad de la plataforma, útil para revisar cómo se verá en el teléfono.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    public static class PlayModeQualityMenu
    {
        private const string MenuPath = "NostalgIA/Calidad/Jugar con la calidad del editor";
        private const string SavedAssetKey = "NostalgIA.EditorPipelineAsset";
        private const string PlayOverrideKey = "NostalgIA.PlayModePipelineOverride";
        private const string KeepEditorQualityKey = "NostalgIA.KeepEditorQualityInPlayMode";

        static PlayModeQualityMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>
        /// Al salir del modo edición guardo qué asset de URP estaba usando el editor; al entrar a jugar
        /// lo pongo en la calidad activa (solo en esta sesión de juego) y al salir regreso el original.
        /// </summary>
        /// <remarks>
        /// No uso <c>SetQualityLevel</c> porque en la matriz de calidad «PC» está excluida de Android
        /// e iPhone: con esa plataforma activa, en modo de juego solo existe «Mobile» y el cambio se
        /// ignora. Cambiar <see cref="QualitySettings.renderPipeline"/> sí funciona y es lo que decide
        /// el HDR y la resolución de render. Las builds no se enteran de nada de esto.
        /// </remarks>
        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    SessionState.SetString(SavedAssetKey, AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline));
                    SessionState.EraseString(PlayOverrideKey);
                    break;

                case PlayModeStateChange.EnteredPlayMode:
                    if (!EditorPrefs.GetBool(KeepEditorQualityKey, true)) return;
                    var editorAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(SavedAssetKey, string.Empty));
                    if (editorAsset == null || editorAsset == GraphicsSettings.currentRenderPipeline) return;
                    SessionState.SetString(PlayOverrideKey, AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
                    QualitySettings.renderPipeline = editorAsset;
                    RefreshLdrComponents();
                    Debug.Log($"[Calidad] Jugando con {editorAsset.name} (el del editor) en lugar del de la plataforma. Para previsualizar móvil: NostalgIA ▸ Calidad.");
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    string original = SessionState.GetString(PlayOverrideKey, null);
                    if (original == null) return;
                    QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(original);
                    SessionState.EraseString(PlayOverrideKey);
                    break;
            }
        }

        /// <summary>Los componentes LDR ya escuchan el cambio de asset; esto es por si acaso.</summary>
        private static void RefreshLdrComponents()
        {
            foreach (var swap in Object.FindObjectsByType<LdrVolumeProfileSwap>(FindObjectsInactive.Include, FindObjectsSortMode.None)) swap.Refresh();
            foreach (var fallback in Object.FindObjectsByType<LdrEmissionFallback>(FindObjectsInactive.Include, FindObjectsSortMode.None)) fallback.Refresh();
        }

        [MenuItem(MenuPath, priority = 400)]
        private static void Toggle()
        {
            bool keep = !EditorPrefs.GetBool(KeepEditorQualityKey, true);
            EditorPrefs.SetBool(KeepEditorQualityKey, keep);
            Debug.Log(keep
                ? "[Calidad] El modo de juego usará la calidad seleccionada en el editor."
                : "[Calidad] El modo de juego usará la calidad de la plataforma activa (vista previa móvil en Android/iOS).");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(KeepEditorQualityKey, true));
            return true;
        }
    }
}

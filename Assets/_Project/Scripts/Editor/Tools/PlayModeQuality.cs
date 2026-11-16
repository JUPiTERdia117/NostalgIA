// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: PlayModeQuality.cs
// Propósito: dejar la calidad del proyecto como estaba al salir del modo de juego.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NostalgIA.EditorTools
{
    /// <summary>
    /// Guarda el asset de URP de la calidad activa al darle Play y lo regresa al salir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// En el modo de juego, <c>NostalgIA.Presentation.VFX.GameGraphics</c> pone en
    /// <see cref="QualitySettings.renderPipeline"/> una copia en memoria del preset elegido en el
    /// menú. En el editor ese es el preset de PC, aunque la plataforma activa sea Android, porque
    /// <c>Application.isMobilePlatform</c> es false. Antes este script forzaba el asset de PC; ya no
    /// hace falta.
    /// </para>
    /// <para>
    /// Los cambios a <see cref="QualitySettings"/> hechos durante el juego se quedan en el editor.
    /// Si no se regresara el asset original, la calidad apuntaría a esa copia, que deja de existir
    /// al salir.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    public static class PlayModeQuality
    {
        private const string SavedAssetKey = "NostalgIA.QualityPipelineBeforePlay";

        static PlayModeQuality()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    SessionState.SetString(SavedAssetKey, AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                case PlayModeStateChange.EnteredEditMode:
                    string original = SessionState.GetString(SavedAssetKey, null);
                    if (original == null) return;
                    var asset = string.IsNullOrEmpty(original) ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(original);
                    if (QualitySettings.renderPipeline != asset) QualitySettings.renderPipeline = asset;
                    if (change == PlayModeStateChange.EnteredEditMode) SessionState.EraseString(SavedAssetKey);
                    break;
            }
        }
    }
}

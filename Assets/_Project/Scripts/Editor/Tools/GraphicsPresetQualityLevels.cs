// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.EditorTools)
// Archivo: GraphicsPresetQualityLevels.cs
// Propósito: registra los presets de calidad (Resources/GraphicsPresets) como niveles
// de Quality para que el build no les quite las variantes de shader que necesitan.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NostalgIA.EditorTools
{
    /// <summary>
    /// Menú «NostalgIA → Registrar presets de calidad en Quality».
    /// </summary>
    /// <remarks>
    /// <para>
    /// URP decide qué variantes de shader compilar en el build mirando <b>solo</b> los assets de
    /// render que aparecen en Project Settings → Quality. Nuestros presets (Bajo, Medio, Alto y
    /// Muy alto) se cargan desde Resources en tiempo de juego, así que URP no los veía: como los
    /// niveles «Mobile» y «PC» tienen sombras, el build quitaba la variante <i>sin sombras</i> y en
    /// Bajo (que no tiene sombras) varios objetos no se dibujaban en el teléfono.
    /// </para>
    /// <para>
    /// Esta herramienta agrega un nivel de Quality por preset (copiando la configuración de
    /// «Mobile» o «PC») para que URP los tenga en cuenta. El juego no usa esos niveles para nada
    /// más: <c>GameGraphics</c> sigue eligiendo el asset por código.
    /// </para>
    /// </remarks>
    public static class GraphicsPresetQualityLevels
    {
        private const string Folder = "Assets/_Project/Rendering/URP/Presets/Resources/GraphicsPresets/";
        private static readonly string[] Presets = { "Low", "Medium", "High", "VeryHigh" };

        [MenuItem("NostalgIA/Registrar presets de calidad en Quality")]
        public static void Run()
        {
            Object quality = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var so = new SerializedObject(quality);
            SerializedProperty levels = so.FindProperty("m_QualitySettings");

            int added = 0;
            foreach (string platform in new[] { "Mobile", "PC" })
            {
                int template = FindLevel(levels, platform);
                if (template < 0) { Debug.LogError($"[Calidad] No encontré el nivel «{platform}»."); continue; }
                foreach (string preset in Presets)
                {
                    string name = $"{platform} {preset}";
                    var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"{Folder}{platform}_{preset}_RPAsset.asset");
                    if (asset == null) { Debug.LogError($"[Calidad] Falta {platform}_{preset}_RPAsset."); continue; }

                    int index = FindLevel(levels, name);
                    if (index < 0)
                    {
                        // Insertar en la posición del nivel base lo duplica (mismas plataformas excluidas
                        // y mismos ajustes); la copia se manda al final de la lista.
                        levels.InsertArrayElementAtIndex(template);
                        levels.MoveArrayElement(template + 1, levels.arraySize - 1);
                        index = levels.arraySize - 1;
                        levels.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue = name;
                        added++;
                    }
                    levels.GetArrayElementAtIndex(index).FindPropertyRelative("customRenderPipeline").objectReferenceValue = asset;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Calidad] Niveles de Quality listos ({added} nuevos).");
        }

        private static int FindLevel(SerializedProperty levels, string name)
        {
            for (int i = 0; i < levels.arraySize; i++)
                if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) return i;
            return -1;
        }

    }
}

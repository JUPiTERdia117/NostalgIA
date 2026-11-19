// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.EditorTools)
// Archivo: ProjectFontMigration.cs
// Propósito: crea la fuente TextMeshPro de JikiTourai ES y la pone en todos los
// textos del proyecto (escenas, prefabs y campos de fuente de los scripts).
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace NostalgIA.EditorTools
{
    /// <summary>
    /// Menú «NostalgIA → Aplicar fuente JikiTourai a todo el proyecto».
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>Crea (si aún no existe) <c>JikiTourai_ES SDF.asset</c> a partir de
    /// <c>Resources/Fonts/JikiTourai_ES.ttf</c>, la versión de JikiTourai con acentos, ñ y signos
    /// del español, con todos los caracteres ya puestos en el atlas.</item>
    /// <item>La hace fuente predeterminada de TextMeshPro: los textos creados por código la usan solos.</item>
    /// <item>Recorre las escenas y prefabs de <c>Assets/_Project</c> y el menú de SlimUI: cambia la
    /// fuente de cada texto y de cada campo de tipo fuente de los scripts.</item>
    /// <item>Los textos de interfaz se ajustan solos (auto tamaño entre su tamaño actual y el 55 %):
    /// JikiTourai es más ancha que la fuente anterior y así ningún texto se sale de su caja ni se
    /// encima con otro.</item>
    /// </list>
    /// </remarks>
    public static class ProjectFontMigration
    {
        private const string FontPath = "Assets/_Project/Resources/Fonts/JikiTourai_ES.ttf";
        private const string AssetPath = "Assets/_Project/Fonts/JikiTourai_ES SDF.asset";
        private static readonly string[] Roots = { "Assets/_Project", "Assets/ThirdParty/SlimUI" };

        // ASCII, español y los símbolos que usa el juego.
        private const string Characters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "áéíóúÁÉÍÓÚñÑüÜ¿¡«»…—–•♦♥●›ª√·‘’“”";

        [MenuItem("NostalgIA/Aplicar fuente JikiTourai a todo el proyecto")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            TMP_FontAsset font = CreateFontAsset();
            if (font == null) return;
            SetAsDefault(font);

            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", Roots))
                changed += ConvertPrefab(AssetDatabase.GUIDToAssetPath(guid), font);

            string active = SceneManager.GetActiveScene().path;
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", Roots))
                changed += ConvertScene(AssetDatabase.GUIDToAssetPath(guid), font);
            if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Fuente] JikiTourai aplicada: {changed} textos y campos cambiados.");
        }

        // ------------------------------------------------------------------ fuente
        private static TMP_FontAsset CreateFontAsset()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (source == null)
            {
                Debug.LogError($"[Fuente] No encontré {FontPath}.");
                return null;
            }

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            // Si ya existe se reutiliza (así no cambia su GUID y no se rompen las referencias) y solo se
            // le agregan los caracteres que le falten, por ejemplo si la fuente ganó glifos nuevos.
            if (existing != null)
            {
                AddCharacters(existing);
                return existing;
            }

            // SDF de 1024 × 1024 con 64 pt por glyph: nítido de 12 a 200 pt.
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024,
                                                               AtlasPopulationMode.Dynamic, false);
            font.name = "JikiTourai_ES SDF";
            AssetDatabase.CreateAsset(font, AssetPath);
            font.atlasTexture.name = font.name + " Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            font.material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);

            AddCharacters(font);
            return font;
        }

        /// <summary>Pone en el atlas los caracteres de <see cref="Characters"/> y deja la fuente estática.</summary>
        private static void AddCharacters(TMP_FontAsset font)
        {
            // Para agregar glifos la fuente debe ser dinámica un momento.
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.TryAddCharacters(Characters, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"[Fuente] Caracteres que no tiene: {missing}");
            // Estática: el atlas ya trae todo y no se modifica mientras se juega.
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
        }

        private static void SetAsDefault(TMP_FontAsset font)
        {
            TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings == null) return;
            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        // ------------------------------------------------------------------ escenas y prefabs
        private static int ConvertPrefab(string path, TMP_FontAsset font)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int changed = ConvertHierarchy(root, font);
            if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return changed;
        }

        private static int ConvertScene(string path, TMP_FontAsset font)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int changed = 0;
            foreach (GameObject root in scene.GetRootGameObjects()) changed += ConvertHierarchy(root, font);
            if (changed > 0) EditorSceneManager.SaveScene(scene);
            return changed;
        }

        private static int ConvertHierarchy(GameObject root, TMP_FontAsset font)
        {
            int changed = 0;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                // Los prefabs se convierten antes que las escenas, así que sus instancias ya heredan la
                // fuente nueva y aquí no se crea ningún cambio de más.
                if (text.font != font)
                {
                    text.font = font;
                    text.fontSharedMaterial = font.material;
                    changed++;
                }
                // Vale para textos de interfaz (TextMeshProUGUI) y para los textos 3D del menú (TextMeshPro).
                if (!text.enableAutoSizing)
                {
                    FitToBox(text, text.fontSize);
                    changed++;
                }
                else if (InstanceFontSize(text, out float size) && !Mathf.Approximately(size, text.fontSizeMax))
                {
                    // Instancia con su propio tamaño: el auto tamaño heredado del prefab debe usar ese.
                    FitToBox(text, size);
                    changed++;
                }
                EditorUtility.SetDirty(text);
            }

            // Campos de tipo fuente en los scripts (por ejemplo, la fuente de la pausa o de los resultados).
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour is TMP_Text) continue;
                var so = new SerializedObject(behaviour);
                SerializedProperty property = so.GetIterator();
                bool any = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (property.objectReferenceValue is TMP_FontAsset current && current != font)
                    {
                        property.objectReferenceValue = font;
                        any = true;
                        changed++;
                    }
                }
                if (any) so.ApplyModifiedPropertiesWithoutUndo();
            }
            return changed;
        }

        /// <summary>Auto tamaño: el tamaño de diseño como máximo y el 55 % como mínimo.</summary>
        private static void FitToBox(TMP_Text text, float size)
        {
            text.fontSizeMax = size;
            text.fontSizeMin = Mathf.Max(10f, Mathf.Round(size * 0.55f));
            text.enableAutoSizing = true;

            // Etiquetas de una sola línea (caja con menos de dos renglones de alto a su tamaño de diseño): sin salto de línea,
            // para que el auto tamaño las encoja a lo ancho en vez de partirlas y encimarlas con la
            // fila de abajo.
            float height = text.rectTransform.rect.height;
            float line = text.GetPreferredValues("Ag", 0f, 0f).y;
            if (height > 0f && line > 0f && height < line * 2f && !text.text.Contains("\n"))
                text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>Tamaño de letra que una instancia de prefab cambió respecto a su prefab, si lo hay.</summary>
        private static bool InstanceFontSize(TMP_Text text, out float size)
        {
            size = 0f;
            if (!PrefabUtility.IsPartOfPrefabInstance(text)) return false;
            PropertyModification[] mods = PrefabUtility.GetPropertyModifications(text);
            if (mods == null) return false;
            Object source = PrefabUtility.GetCorrespondingObjectFromSource(text);
            foreach (PropertyModification mod in mods)
            {
                if (mod.target != source || mod.propertyPath != "m_fontSize") continue;
                return float.TryParse(mod.value, System.Globalization.NumberStyles.Float,
                                      System.Globalization.CultureInfo.InvariantCulture, out size);
            }
            return false;
        }
    }
}

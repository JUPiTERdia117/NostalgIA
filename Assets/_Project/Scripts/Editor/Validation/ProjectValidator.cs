// -----------------------------------------------------------------------------
// NostalgIA — Herramientas de editor (NostalgIA.Editor)
// Archivo: ProjectValidator.cs
// Propósito: validación automática de las reglas obligatorias del curso y de la
// arquitectura (TDD §15). Se ejecuta desde el menú NostalgIA › Validar proyecto y
// antes de cada build (ver ValidationBuildGate).
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NostalgIA.Core;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using CompilationAssembly = UnityEditor.Compilation.Assembly;

namespace NostalgIA.EditorTools.Validation
{
    /// <summary>
    /// Valida el proyecto contra las restricciones de la asignatura y las reglas de arquitectura.
    /// </summary>
    /// <remarks>
    /// <para>Reglas evaluadas:</para>
    /// <list type="number">
    /// <item><description><b>Sin Terrain</b> (restricción 1 del curso): ningún componente <c>Terrain</c>/<c>TerrainCollider</c> ni asset <c>TerrainData</c>.</description></item>
    /// <item><description><b>Sin scripts faltantes</b> en escenas y prefabs.</description></item>
    /// <item><description><b>Sin prefabs faltantes</b>: toda instancia de prefab apunta a un asset existente.</description></item>
    /// <item><description><b>Material por defecto de ProBuilder</b> en escenas de Estación (advertencia hasta que la Pista B lo reemplace).</description></item>
    /// <item><description><b>Build Settings</b>: escenas existentes, <c>00_Boot</c> primero y sin escenas de prueba.</description></item>
    /// <item><description><b>Capas</b> con los nombres del TDD (§13).</description></item>
    /// <item><description><b>Matriz de colisiones</b> según el TDD (§13).</description></item>
    /// <item><description><b>Dependencias entre ensamblados</b>: la jugabilidad nunca referencia a la presentación (principio P1).</description></item>
    /// </list>
    /// <para>
    /// En este módulo se decidió inspeccionar escenas y prefabs leyendo su texto YAML en lugar de
    /// abrirlos con el editor. El proyecto usa serialización de texto forzada; así la validación no
    /// modifica la escena abierta, puede ejecutarse dentro del proceso de build y revisa cientos de
    /// archivos en segundos. Cada componente serializado comienza con una cabecera
    /// <c>--- !u!&lt;idDeClase&gt; &amp;&lt;idLocal&gt;</c>, donde el identificador de clase de Unity es
    /// 218 para <c>Terrain</c>, 154 para <c>TerrainCollider</c> y 156 para <c>TerrainData</c>.
    /// </para>
    /// </remarks>
    public static class ProjectValidator
    {
        /// <summary>Ruta del menú del editor.</summary>
        public const string MenuPath = "NostalgIA/Validar proyecto";

        /// <summary>Carpeta del reporte, relativa a la raíz del proyecto.</summary>
        public const string ReportFolder = "Docs/Bitacora_Tecnica";

        /// <summary>Nombre del archivo de reporte; se sobrescribe en cada ejecución.</summary>
        public const string ReportFileName = "Reporte_Validacion_Proyecto.md";

        // Nombres de las reglas, tal como aparecen en el reporte.
        private const string RuleTerrain = "Sin Terrain";
        private const string RuleMissingScripts = "Sin scripts faltantes";
        private const string RuleMissingPrefabs = "Sin prefabs faltantes";
        private const string RuleProBuilderMaterial = "Sin material por defecto de ProBuilder en Estaciones";
        private const string RuleBuildScenes = "Build Settings";
        private const string RuleLayers = "Capas físicas (TDD §13)";
        private const string RuleCollisionMatrix = "Matriz de colisiones (TDD §13)";
        private const string RuleAssemblies = "Dependencias entre ensamblados (TDD §2.1)";
        private const string RuleSerialization = "Serialización de texto";

        /// <summary>
        /// Mientras la Pista B reemplaza el material por defecto de ProBuilder (tarea F1-B02), su
        /// presencia se reporta como advertencia. Al cerrar la Fase 2 se cambiará a <c>true</c>
        /// para que bloquee el build.
        /// </summary>
        /// <remarks>Se declara <c>static readonly</c> y no <c>const</c> para que el compilador no
        /// marque como inalcanzable la rama de error mientras el valor sea <c>false</c>.</remarks>
        private static readonly bool ProBuilderDefaultMaterialIsError = false;

        // Cabeceras YAML de los tipos de Terrain. La opción Multiline hace que «^» coincida
        // con el inicio de cada línea del archivo.
        private static readonly Regex TerrainHeaderRegex =
            new Regex(@"^--- !u!(218|154|156) ", RegexOptions.Multiline | RegexOptions.Compiled);

        // Referencia serializada a un script: m_Script: {fileID: 11500000, guid: <32 hex>, type: 3}.
        private static readonly Regex ScriptReferenceRegex =
            new Regex(@"m_Script: \{fileID: -?\d+, guid: ([0-9a-f]{32}), type: 3\}", RegexOptions.Compiled);

        // Origen de una instancia de prefab: m_SourcePrefab: {fileID: 100100000, guid: <32 hex>, type: 3}.
        private static readonly Regex SourcePrefabRegex =
            new Regex(@"m_SourcePrefab: \{fileID: -?\d+, guid: ([0-9a-f]{32}), type: 3\}", RegexOptions.Compiled);

        /// <summary>
        /// Pares de capas que no deben colisionar entre sí, según el TDD (§13).
        /// La capa <c>Trigger</c> se trata aparte: solo debe detectar al jugador.
        /// </summary>
        private static readonly int[,] IgnoredCollisionPairs =
        {
            { Layers.Player, Layers.Debris },
            { Layers.Player, Layers.PlayerProjectile },
            { Layers.Enemy, Layers.EnemyProjectile },
            { Layers.Debris, Layers.Enemy },
            { Layers.Debris, Layers.PlayerProjectile },
            { Layers.Debris, Layers.EnemyProjectile },
            { Layers.Debris, Layers.Debris },
        };

        /// <summary>
        /// Reglas de dependencia: cada ensamblado y los ensamblados que tiene prohibido referenciar.
        /// </summary>
        private static readonly (string assembly, string[] forbidden)[] AssemblyRules =
        {
            ("NostalgIA.Core", new[] { "NostalgIA.Input", "NostalgIA.Gameplay", "NostalgIA.Presentation" }),
            ("NostalgIA.Input", new[] { "NostalgIA.Gameplay", "NostalgIA.Presentation" }),
            ("NostalgIA.Gameplay", new[] { "NostalgIA.Presentation" }),
            ("NostalgIA.Presentation", new[] { "NostalgIA.Gameplay" }),
        };

        /// <summary>
        /// Ejecuta la validación desde el menú, guarda el reporte y muestra el resultado.
        /// </summary>
        [MenuItem(MenuPath, priority = 0)]
        private static void ValidateFromMenu()
        {
            ValidationReport report = Run();
            string reportPath = WriteReport(report);
            LogSummary(report, reportPath);

            string title = report.ErrorCount == 0 ? "Validación aprobada" : "Validación rechazada";
            EditorUtility.DisplayDialog(
                title,
                $"{report.ErrorCount} error(es) y {report.WarningCount} advertencia(s).\n\nReporte guardado en:\n{reportPath}",
                "Aceptar");
        }

        /// <summary>
        /// Ejecuta todas las reglas y devuelve el reporte. No modifica ningún activo.
        /// </summary>
        public static ValidationReport Run()
        {
            var report = new ValidationReport();
            try
            {
                CheckSerializationMode(report);

                // Se leen una sola vez todas las escenas y prefabs del proyecto y se reutiliza
                // el texto en las reglas que lo necesitan.
                List<string> assetPaths = CollectSceneAndPrefabPaths();
                var buildScenePaths = new HashSet<string>(StringComparer.Ordinal);
                foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                {
                    if (scene.enabled) buildScenePaths.Add(scene.path);
                }

                CheckTerrainAndMissingScripts(report, assetPaths, buildScenePaths);
                CheckProBuilderDefaultMaterial(report, buildScenePaths);
                CheckBuildScenes(report);
                CheckLayers(report);
                CheckCollisionMatrix(report);
                CheckAssemblyDependencies(report);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return report;
        }

        /// <summary>
        /// Escribe el reporte en <c>Docs/Bitacora_Tecnica/</c> y devuelve la ruta absoluta.
        /// </summary>
        public static string WriteReport(ValidationReport report)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string folder = Path.Combine(projectRoot, ReportFolder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, ReportFileName);
            File.WriteAllText(path, report.ToMarkdown(Application.unityVersion), new UTF8Encoding(false));
            return path;
        }

        /// <summary>Escribe en la consola un resumen y cada error o advertencia.</summary>
        public static void LogSummary(ValidationReport report, string reportPath)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                ValidationIssue issue = report.Issues[i];
                string line = $"[Validación] {issue.Rule}: {issue.Message} {issue.AssetPath}";
                if (issue.Severity == ValidationSeverity.Error) Debug.LogError(line);
                else if (issue.Severity == ValidationSeverity.Warning) Debug.LogWarning(line);
            }
            Debug.Log($"[Validación] {report.ErrorCount} error(es), {report.WarningCount} advertencia(s). Reporte: {reportPath}");
        }

        // ---------------------------------------------------------------------
        // Reglas
        // ---------------------------------------------------------------------

        /// <summary>
        /// Comprueba que el proyecto use serialización de texto, requisito para que el resto de
        /// reglas pueda leer escenas y prefabs.
        /// </summary>
        private static void CheckSerializationMode(ValidationReport report)
        {
            if (EditorSettings.serializationMode == SerializationMode.ForceText)
            {
                report.Pass(RuleSerialization);
                return;
            }
            report.Add(ValidationSeverity.Error, RuleSerialization, string.Empty,
                "El proyecto debe usar Asset Serialization = Force Text (Project Settings › Editor).");
        }

        /// <summary>Reúne las rutas de todas las escenas y prefabs bajo <c>Assets/</c>.</summary>
        private static List<string> CollectSceneAndPrefabPaths()
        {
            var paths = new List<string>(1024);
            string[] folders = { "Assets" };
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", folders))
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", folders))
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            return paths;
        }

        /// <summary>
        /// Busca componentes de Terrain y referencias a scripts inexistentes en escenas y prefabs,
        /// y assets <c>TerrainData</c> en todo el proyecto.
        /// </summary>
        /// <remarks>
        /// Un script se considera faltante cuando su GUID no corresponde a ningún activo del
        /// proyecto ni de los paquetes. Los resultados de cada GUID se guardan en un diccionario para
        /// no consultar la base de datos de activos más de una vez por GUID.
        /// </remarks>
        private static void CheckTerrainAndMissingScripts(ValidationReport report, List<string> assetPaths, HashSet<string> buildScenePaths)
        {
            int terrainFindings = 0;
            int missingScriptFindings = 0;
            int missingPrefabFindings = 0;
            var guidExists = new Dictionary<string, bool>(512, StringComparer.Ordinal);

            for (int i = 0; i < assetPaths.Count; i++)
            {
                string path = assetPaths[i];
                if (i % 20 == 0)
                {
                    EditorUtility.DisplayProgressBar("NostalgIA · Validación", path, (float)i / Mathf.Max(1, assetPaths.Count));
                }

                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (IOException exception)
                {
                    report.Add(ValidationSeverity.Warning, RuleTerrain, path, $"No se pudo leer el archivo: {exception.Message}");
                    continue;
                }

                if (!text.StartsWith("%YAML", StringComparison.Ordinal))
                {
                    report.Add(ValidationSeverity.Warning, RuleTerrain, path, "El archivo no está serializado como texto; no se pudo inspeccionar.");
                    continue;
                }

                // Regla 1: Terrain.
                int terrainCount = TerrainHeaderRegex.Matches(text).Count;
                if (terrainCount > 0)
                {
                    terrainFindings++;
                    report.Add(ValidationSeverity.Error, RuleTerrain, path,
                        $"Contiene {terrainCount} componente(s) de Terrain. Está prohibido por la asignatura: sustituir por mallas 3D.");
                }

                // Regla 2: scripts faltantes. Se marcan como error en escenas del build y en
                // activos propios; en contenido de terceros, como advertencia.
                bool critical = buildScenePaths.Contains(path) || path.StartsWith("Assets/_Project/", StringComparison.Ordinal);
                int missingInFile = 0;
                foreach (Match match in ScriptReferenceRegex.Matches(text))
                {
                    string guid = match.Groups[1].Value;
                    if (!guidExists.TryGetValue(guid, out bool exists))
                    {
                        exists = !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid));
                        guidExists[guid] = exists;
                    }
                    if (!exists) missingInFile++;
                }
                if (missingInFile > 0)
                {
                    missingScriptFindings++;
                    report.Add(critical ? ValidationSeverity.Error : ValidationSeverity.Warning, RuleMissingScripts, path,
                        $"{missingInFile} referencia(s) a scripts que no existen en el proyecto.");
                }

                // Regla 2b: instancias de prefab cuyo asset de origen ya no existe. Unity las
                // muestra como «Missing Prefab» y su contenido no se puede editar ni actualizar.
                int missingPrefabsInFile = 0;
                foreach (Match match in SourcePrefabRegex.Matches(text))
                {
                    string guid = match.Groups[1].Value;
                    if (!guidExists.TryGetValue(guid, out bool exists))
                    {
                        exists = !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid));
                        guidExists[guid] = exists;
                    }
                    if (!exists) missingPrefabsInFile++;
                }
                if (missingPrefabsInFile > 0)
                {
                    missingPrefabFindings++;
                    report.Add(critical ? ValidationSeverity.Error : ValidationSeverity.Warning, RuleMissingPrefabs, path,
                        $"{missingPrefabsInFile} instancia(s) de un prefab que ya no existe (Missing Prefab). Restaurar el prefab o eliminar las instancias.");
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:TerrainData"))
            {
                terrainFindings++;
                report.Add(ValidationSeverity.Error, RuleTerrain, AssetDatabase.GUIDToAssetPath(guid),
                    "Asset TerrainData presente en el proyecto. Debe eliminarse.");
            }

            if (terrainFindings == 0)
            {
                report.Pass($"{RuleTerrain}: 0 componentes Terrain/TerrainCollider y 0 assets TerrainData en {assetPaths.Count} escenas y prefabs.");
            }
            if (missingScriptFindings == 0)
            {
                report.Pass($"{RuleMissingScripts}: {assetPaths.Count} escenas y prefabs revisados.");
            }
            if (missingPrefabFindings == 0)
            {
                report.Pass($"{RuleMissingPrefabs}: {assetPaths.Count} escenas y prefabs revisados.");
            }
        }

        /// <summary>
        /// Cuenta las referencias al material por defecto de ProBuilder en las escenas de Estación del build.
        /// </summary>
        /// <remarks>
        /// El GUID del material se obtiene del propio paquete de ProBuilder en lugar de escribirlo en
        /// el código, para que la regla siga funcionando si el paquete se actualiza.
        /// </remarks>
        private static void CheckProBuilderDefaultMaterial(ValidationReport report, HashSet<string> buildScenePaths)
        {
            string materialGuid = FindProBuilderDefaultMaterialGuid();
            if (string.IsNullOrEmpty(materialGuid))
            {
                report.Add(ValidationSeverity.Info, RuleProBuilderMaterial, string.Empty,
                    "No se encontró el material por defecto de ProBuilder; la regla no se evaluó.");
                return;
            }

            int offendingScenes = 0;
            foreach (string scenePath in buildScenePaths)
            {
                if (!scenePath.StartsWith(SceneNames.Paths.StationsFolder, StringComparison.Ordinal)) continue;
                if (!File.Exists(scenePath)) continue;

                int count = CountOccurrences(File.ReadAllText(scenePath), materialGuid);
                if (count == 0) continue;

                offendingScenes++;
                report.Add(ProBuilderDefaultMaterialIsError ? ValidationSeverity.Error : ValidationSeverity.Warning,
                    RuleProBuilderMaterial, scenePath,
                    $"{count} referencia(s) al material gris de prototipo de ProBuilder. Reemplazar por materiales de la Estación (tarea F1-B02).");
            }

            if (offendingScenes == 0)
            {
                report.Pass(RuleProBuilderMaterial);
            }
        }

        /// <summary>
        /// Verifica la lista de escenas del build: que existan, que <c>00_Boot</c> sea la primera
        /// habilitada y que no se incluyan escenas de prueba.
        /// </summary>
        private static void CheckBuildScenes(ValidationReport report)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            int findings = 0;
            string firstEnabled = null;

            for (int i = 0; i < scenes.Length; i++)
            {
                EditorBuildSettingsScene scene = scenes[i];
                bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) != null;

                if (!exists)
                {
                    findings++;
                    report.Add(scene.enabled ? ValidationSeverity.Error : ValidationSeverity.Warning, RuleBuildScenes, scene.path,
                        "La escena registrada en Build Settings no existe.");
                    continue;
                }

                if (!scene.enabled) continue;
                if (firstEnabled == null) firstEnabled = scene.path;

                if (scene.path.StartsWith(SceneNames.Paths.SandboxFolder, StringComparison.Ordinal))
                {
                    findings++;
                    report.Add(ValidationSeverity.Error, RuleBuildScenes, scene.path,
                        "Las escenas de prueba (Sandbox) no deben incluirse en el build.");
                }
            }

            if (firstEnabled != SceneNames.Paths.Boot)
            {
                findings++;
                report.Add(ValidationSeverity.Error, RuleBuildScenes, firstEnabled ?? string.Empty,
                    $"La primera escena habilitada debe ser {SceneNames.Paths.Boot}.");
            }

            if (findings == 0)
            {
                report.Pass($"{RuleBuildScenes}: {scenes.Length} escena(s), {SceneNames.Boot} primero, sin escenas de prueba.");
            }
        }

        /// <summary>Compara los nombres de capa configurados con los del TDD (§13).</summary>
        private static void CheckLayers(ValidationReport report)
        {
            int findings = 0;
            for (int layer = 0; layer < Layers.ExpectedNames.Length; layer++)
            {
                string expected = Layers.ExpectedNames[layer];
                if (expected == null) continue;

                string actual = LayerMask.LayerToName(layer);
                if (actual == expected) continue;

                findings++;
                report.Add(ValidationSeverity.Error, RuleLayers, "ProjectSettings/TagManager.asset",
                    $"La capa {layer} se llama «{actual}» y debe llamarse «{expected}».");
            }

            if (findings == 0)
            {
                report.Pass(RuleLayers);
            }
        }

        /// <summary>
        /// Comprueba que la matriz de colisiones ignore los pares definidos en el TDD y que la capa
        /// <c>Trigger</c> solo interactúe con el jugador.
        /// </summary>
        /// <remarks>
        /// Ignorar colisiones entre capas que nunca deben tocarse reduce los pares que el motor de
        /// física evalúa en la fase amplia (<i>broad phase</i>), lo que ahorra tiempo de CPU en móvil.
        /// </remarks>
        private static void CheckCollisionMatrix(ValidationReport report)
        {
            int findings = 0;
            for (int i = 0; i < IgnoredCollisionPairs.GetLength(0); i++)
            {
                int a = IgnoredCollisionPairs[i, 0];
                int b = IgnoredCollisionPairs[i, 1];
                if (Physics.GetIgnoreLayerCollision(a, b)) continue;

                findings++;
                report.Add(ValidationSeverity.Warning, RuleCollisionMatrix, "ProjectSettings/DynamicsManager.asset",
                    $"Las capas {LayerMask.LayerToName(a)} y {LayerMask.LayerToName(b)} deberían ignorar sus colisiones.");
            }

            for (int other = 0; other < 32; other++)
            {
                bool shouldCollide = other == Layers.Player;
                bool collides = !Physics.GetIgnoreLayerCollision(Layers.Trigger, other);
                if (collides == shouldCollide) continue;

                findings++;
                report.Add(ValidationSeverity.Warning, RuleCollisionMatrix, "ProjectSettings/DynamicsManager.asset",
                    shouldCollide
                        ? "La capa Trigger debe interactuar con Player."
                        : $"La capa Trigger solo debe interactuar con Player, pero también interactúa con la capa {other} ({LayerMask.LayerToName(other)}).");
            }

            if (findings == 0)
            {
                report.Pass(RuleCollisionMatrix);
            }
        }

        /// <summary>
        /// Verifica que los ensamblados del proyecto existan y respeten las reglas de dependencia.
        /// </summary>
        /// <remarks>
        /// Se consulta el grafo de compilación real de Unity (<see cref="CompilationPipeline"/>), no
        /// los archivos <c>.asmdef</c>, para validar lo que efectivamente se compila.
        /// </remarks>
        private static void CheckAssemblyDependencies(ValidationReport report)
        {
            CompilationAssembly[] assemblies = CompilationPipeline.GetAssemblies();
            var byName = new Dictionary<string, CompilationAssembly>(assemblies.Length, StringComparer.Ordinal);
            for (int i = 0; i < assemblies.Length; i++)
            {
                byName[assemblies[i].name] = assemblies[i];
            }

            int findings = 0;
            for (int r = 0; r < AssemblyRules.Length; r++)
            {
                (string assemblyName, string[] forbidden) = AssemblyRules[r];
                if (!byName.TryGetValue(assemblyName, out CompilationAssembly assembly))
                {
                    findings++;
                    report.Add(ValidationSeverity.Error, RuleAssemblies, string.Empty,
                        $"No se encontró el ensamblado {assemblyName}. Debe existir su .asmdef con al menos un script.");
                    continue;
                }

                foreach (CompilationAssembly reference in assembly.assemblyReferences)
                {
                    if (Array.IndexOf(forbidden, reference.name) < 0) continue;

                    findings++;
                    report.Add(ValidationSeverity.Error, RuleAssemblies, assembly.name,
                        $"{assembly.name} no puede referenciar a {reference.name} (TDD §2.1).");
                }
            }

            if (findings == 0)
            {
                report.Pass(RuleAssemblies);
            }
        }

        // ---------------------------------------------------------------------
        // Utilidades
        // ---------------------------------------------------------------------

        /// <summary>Busca el GUID del material «ProBuilderDefault» dentro del paquete de ProBuilder.</summary>
        private static string FindProBuilderDefaultMaterialGuid()
        {
            string[] guids = AssetDatabase.FindAssets("ProBuilderDefault t:Material", new[] { "Packages/com.unity.probuilder" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (Path.GetFileNameWithoutExtension(path) == "ProBuilderDefault")
                {
                    return guids[i];
                }
            }
            return null;
        }

        /// <summary>Cuenta las apariciones no superpuestas de <paramref name="value"/> en <paramref name="text"/>.</summary>
        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = text.IndexOf(value, StringComparison.Ordinal);
            while (index >= 0)
            {
                count++;
                index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
            }
            return count;
        }
    }
}

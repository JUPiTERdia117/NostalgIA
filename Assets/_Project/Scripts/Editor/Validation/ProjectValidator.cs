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
    /// <para>Reglas evaluadas (nombres en <see cref="ValidationRules"/>):</para>
    /// <list type="number">
    /// <item><description><b>Sin Terrain</b> (restricción 1 del curso): ningún componente <c>Terrain</c>/<c>TerrainCollider</c> ni asset <c>TerrainData</c>.</description></item>
    /// <item><description><b>Sin scripts faltantes</b> en escenas y prefabs.</description></item>
    /// <item><description><b>Sin prefabs faltantes</b>: toda instancia de prefab apunta a un asset existente.</description></item>
    /// <item><description><b>Material por defecto de ProBuilder</b> en escenas de Estación (error desde la tarea F1-B02).</description></item>
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
    /// <para>
    /// Cada archivo se lee una sola vez: el texto de las escenas de Estación incluidas en el build se
    /// conserva en memoria durante la ejecución para reutilizarlo en la regla del material de
    /// ProBuilder, en lugar de volver a leer del disco archivos de varios megabytes.
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

        /// <summary>
        /// La Pista B sustituyó el material por defecto de ProBuilder en el Tutorial (tarea F1-B02,
        /// <c>Phase1VisualBuilder</c>), así que desde la Fase 1 su presencia en una escena de Estación
        /// es un <b>error</b> que bloquea el build: evita que una pieza nueva entre sin material.
        /// </summary>
        /// <remarks>Se mantiene como <c>static readonly</c> (y no <c>const</c>) para poder relajar la
        /// regla temporalmente sin que el compilador marque código inalcanzable.</remarks>
        private static readonly bool ProBuilderDefaultMaterialIsError = true;

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
        /// Ejecuta todas las reglas y devuelve el reporte. No modifica ningún activo ni escribe archivos.
        /// </summary>
        public static ValidationReport Run()
        {
            var report = new ValidationReport();
            try
            {
                CheckSerializationMode(report);

                var buildScenePaths = new HashSet<string>(StringComparer.Ordinal);
                foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                {
                    if (scene.enabled) buildScenePaths.Add(scene.path);
                }

                // Se leen una sola vez todas las escenas y prefabs; el texto de las escenas de
                // Estación del build se guarda para la regla del material de ProBuilder.
                List<string> assetPaths = CollectSceneAndPrefabPaths();
                var stationSceneTexts = new Dictionary<string, string>(StringComparer.Ordinal);
                ScanScenesAndPrefabs(report, assetPaths, buildScenePaths, stationSceneTexts);

                CheckProBuilderDefaultMaterial(report, stationSceneTexts);
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
                report.Pass(ValidationRules.Serialization);
                return;
            }
            report.Add(ValidationSeverity.Error, ValidationRules.Serialization, string.Empty,
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
        /// Recorre escenas y prefabs una sola vez y evalúa las reglas de Terrain, scripts faltantes
        /// y prefabs faltantes. Además busca assets <c>TerrainData</c> en todo el proyecto.
        /// </summary>
        /// <param name="report">Reporte donde se registran los hallazgos.</param>
        /// <param name="assetPaths">Escenas y prefabs a inspeccionar.</param>
        /// <param name="buildScenePaths">Escenas habilitadas en el build; sus fallos son errores.</param>
        /// <param name="stationSceneTexts">Salida: texto de las escenas de Estación del build.</param>
        private static void ScanScenesAndPrefabs(ValidationReport report, List<string> assetPaths,
            HashSet<string> buildScenePaths, Dictionary<string, string> stationSceneTexts)
        {
            int terrainFindings = 0;
            int missingScriptFindings = 0;
            int missingPrefabFindings = 0;
            // Caché GUID → existe: cada GUID se consulta a la base de datos de activos una sola vez,
            // aunque aparezca cientos de veces (por ejemplo, las 473 cajas del Tutorial).
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
                    report.Add(ValidationSeverity.Warning, ValidationRules.AssetReadability, path, $"No se pudo leer el archivo: {exception.Message}");
                    continue;
                }

                if (!text.StartsWith("%YAML", StringComparison.Ordinal))
                {
                    report.Add(ValidationSeverity.Warning, ValidationRules.AssetReadability, path, "El archivo no está serializado como texto; no se pudo inspeccionar.");
                    continue;
                }

                bool inBuild = buildScenePaths.Contains(path);
                if (inBuild && path.StartsWith(SceneNames.Paths.StationsFolder, StringComparison.Ordinal))
                {
                    stationSceneTexts[path] = text;
                }

                // Regla: Terrain.
                int terrainCount = TerrainHeaderRegex.Matches(text).Count;
                if (terrainCount > 0)
                {
                    terrainFindings++;
                    report.Add(ValidationSeverity.Error, ValidationRules.Terrain, path,
                        $"Contiene {terrainCount} componente(s) de Terrain. Está prohibido por la asignatura: sustituir por mallas 3D.");
                }

                // Reglas de referencias rotas: son error en escenas del build y en activos propios;
                // en contenido de terceros, advertencia.
                bool critical = inBuild || path.StartsWith("Assets/_Project/", StringComparison.Ordinal);
                ValidationSeverity severity = critical ? ValidationSeverity.Error : ValidationSeverity.Warning;

                int missingScripts = CountMissingReferences(ScriptReferenceRegex, text, guidExists);
                if (missingScripts > 0)
                {
                    missingScriptFindings++;
                    report.Add(severity, ValidationRules.MissingScripts, path,
                        $"{missingScripts} referencia(s) a scripts que no existen en el proyecto.");
                }

                // Unity muestra estas instancias como «Missing Prefab»: su contenido no se puede
                // editar ni actualizar y no representa al prefab vigente.
                int missingPrefabs = CountMissingReferences(SourcePrefabRegex, text, guidExists);
                if (missingPrefabs > 0)
                {
                    missingPrefabFindings++;
                    report.Add(severity, ValidationRules.MissingPrefabs, path,
                        $"{missingPrefabs} instancia(s) de un prefab que ya no existe (Missing Prefab). Restaurar el prefab o eliminar las instancias.");
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:TerrainData"))
            {
                terrainFindings++;
                report.Add(ValidationSeverity.Error, ValidationRules.Terrain, AssetDatabase.GUIDToAssetPath(guid),
                    "Asset TerrainData presente en el proyecto. Debe eliminarse.");
            }

            if (terrainFindings == 0)
            {
                report.Pass($"{ValidationRules.Terrain}: 0 componentes Terrain/TerrainCollider y 0 assets TerrainData en {assetPaths.Count} escenas y prefabs.");
            }
            if (missingScriptFindings == 0)
            {
                report.Pass($"{ValidationRules.MissingScripts}: {assetPaths.Count} escenas y prefabs revisados.");
            }
            if (missingPrefabFindings == 0)
            {
                report.Pass($"{ValidationRules.MissingPrefabs}: {assetPaths.Count} escenas y prefabs revisados.");
            }
        }

        /// <summary>
        /// Cuenta las referencias de <paramref name="text"/> cuyo GUID no corresponde a un activo existente.
        /// </summary>
        /// <remarks>
        /// Se valida la existencia con <see cref="AssetDatabase.GetMainAssetTypeAtPath"/> además de
        /// <see cref="AssetDatabase.GUIDToAssetPath(string)"/>: la segunda puede seguir devolviendo
        /// la ruta de un activo recién borrado hasta que la base de datos se actualiza, lo que
        /// ocultaría una referencia rota.
        /// </remarks>
        private static int CountMissingReferences(Regex regex, string text, Dictionary<string, bool> guidExists)
        {
            int missing = 0;
            foreach (Match match in regex.Matches(text))
            {
                string guid = match.Groups[1].Value;
                if (!guidExists.TryGetValue(guid, out bool exists))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    exists = !string.IsNullOrEmpty(assetPath) && AssetDatabase.GetMainAssetTypeAtPath(assetPath) != null;
                    guidExists[guid] = exists;
                }
                if (!exists) missing++;
            }
            return missing;
        }

        /// <summary>
        /// Cuenta las referencias al material por defecto de ProBuilder en las escenas de Estación del build.
        /// </summary>
        /// <remarks>
        /// El GUID del material se obtiene del propio paquete de ProBuilder en lugar de escribirlo en
        /// el código, para que la regla siga funcionando si el paquete se actualiza.
        /// </remarks>
        private static void CheckProBuilderDefaultMaterial(ValidationReport report, Dictionary<string, string> stationSceneTexts)
        {
            string materialGuid = FindProBuilderDefaultMaterialGuid();
            if (string.IsNullOrEmpty(materialGuid))
            {
                report.Add(ValidationSeverity.Info, ValidationRules.ProBuilderMaterial, string.Empty,
                    "No se encontró el material por defecto de ProBuilder; la regla no se evaluó.");
                return;
            }

            int offendingScenes = 0;
            foreach (KeyValuePair<string, string> scene in stationSceneTexts)
            {
                int count = CountOccurrences(scene.Value, materialGuid);
                if (count == 0) continue;

                offendingScenes++;
                report.Add(ProBuilderDefaultMaterialIsError ? ValidationSeverity.Error : ValidationSeverity.Warning,
                    ValidationRules.ProBuilderMaterial, scene.Key,
                    $"{count} referencia(s) al material gris de prototipo de ProBuilder. Reemplazar por materiales de la Estación (tarea F1-B02).");
            }

            if (offendingScenes == 0)
            {
                report.Pass(ValidationRules.ProBuilderMaterial);
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
                    report.Add(scene.enabled ? ValidationSeverity.Error : ValidationSeverity.Warning, ValidationRules.BuildScenes, scene.path,
                        "La escena registrada en Build Settings no existe.");
                    continue;
                }

                if (!scene.enabled) continue;
                if (firstEnabled == null) firstEnabled = scene.path;

                if (scene.path.StartsWith(SceneNames.Paths.SandboxFolder, StringComparison.Ordinal))
                {
                    findings++;
                    report.Add(ValidationSeverity.Error, ValidationRules.BuildScenes, scene.path,
                        "Las escenas de prueba (Sandbox) no deben incluirse en el build.");
                }
            }

            if (firstEnabled != SceneNames.Paths.Boot)
            {
                findings++;
                report.Add(ValidationSeverity.Error, ValidationRules.BuildScenes, firstEnabled ?? string.Empty,
                    $"La primera escena habilitada debe ser {SceneNames.Paths.Boot}.");
            }

            if (findings == 0)
            {
                report.Pass($"{ValidationRules.BuildScenes}: {scenes.Length} escena(s), {SceneNames.Boot} primero, sin escenas de prueba.");
            }
        }

        /// <summary>Compara los nombres de capa configurados con los del TDD (§13).</summary>
        private static void CheckLayers(ValidationReport report)
        {
            int findings = 0;
            for (int layer = 0; layer < Layers.ExpectedNames.Count; layer++)
            {
                string expected = Layers.ExpectedNames[layer];
                if (expected == null) continue;

                string actual = LayerMask.LayerToName(layer);
                if (actual == expected) continue;

                findings++;
                report.Add(ValidationSeverity.Error, ValidationRules.Layers, "ProjectSettings/TagManager.asset",
                    $"La capa {layer} se llama «{actual}» y debe llamarse «{expected}».");
            }

            if (findings == 0)
            {
                report.Pass(ValidationRules.Layers);
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
                report.Add(ValidationSeverity.Warning, ValidationRules.CollisionMatrix, "ProjectSettings/DynamicsManager.asset",
                    $"Las capas {LayerMask.LayerToName(a)} y {LayerMask.LayerToName(b)} deberían ignorar sus colisiones.");
            }

            for (int other = 0; other < 32; other++)
            {
                bool shouldCollide = other == Layers.Player;
                bool collides = !Physics.GetIgnoreLayerCollision(Layers.Trigger, other);
                if (collides == shouldCollide) continue;

                findings++;
                report.Add(ValidationSeverity.Warning, ValidationRules.CollisionMatrix, "ProjectSettings/DynamicsManager.asset",
                    shouldCollide
                        ? "La capa Trigger debe interactuar con Player."
                        : $"La capa Trigger solo debe interactuar con Player, pero también interactúa con la capa {other} ({LayerMask.LayerToName(other)}).");
            }

            if (findings == 0)
            {
                report.Pass(ValidationRules.CollisionMatrix);
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
                    report.Add(ValidationSeverity.Error, ValidationRules.Assemblies, string.Empty,
                        $"No se encontró el ensamblado {assemblyName}. Debe existir su .asmdef con al menos un script.");
                    continue;
                }

                foreach (CompilationAssembly reference in assembly.assemblyReferences)
                {
                    if (Array.IndexOf(forbidden, reference.name) < 0) continue;

                    findings++;
                    report.Add(ValidationSeverity.Error, ValidationRules.Assemblies, assembly.name,
                        $"{assembly.name} no puede referenciar a {reference.name} (TDD §2.1).");
                }
            }

            if (findings == 0)
            {
                report.Pass(ValidationRules.Assemblies);
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
        public static int CountOccurrences(string text, string value)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value)) return 0;

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

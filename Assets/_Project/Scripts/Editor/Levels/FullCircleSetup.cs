// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: FullCircleSetup.cs
// Propósito: cerrar el ciclo completo del juego: Dreamcore → Invierno, lava de
// Invierno con LavaBlue, portal que deja la IA y estación final.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using NostalgIA.Core;
using NostalgIA.Gameplay.Level;
using NostalgIA.Presentation.Levels;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NostalgIA.EditorTools.Levels
{
    /// <summary>
    /// Pasos de editor que dejan jugable el ciclo completo (Tutorial → Verano → Dreamcore →
    /// Invierno → estación final). Todos se pueden repetir sin duplicar nada.
    /// </summary>
    public static class FullCircleSetup
    {
        private const string MenuRoot = "NostalgIA/Niveles/Ciclo completo/";

        // ------------------------------------------------------------------ lava
        /// <summary>Material de lava que hizo el equipo (Shader Graph LavaBlue).</summary>
        public const string LavaBlueMaterialPath = "Assets/_Project/Art/Materials/Primavera/LavaBlue.mat";
        /// <summary>Material anterior de la lava de Invierno (shader BlueCodeLava).</summary>
        public const string OldInviernoLavaMaterialPath = "Assets/_Project/Art/Materials/Invierno/M_Invierno_Lava.mat";
        /// <summary>Archivo donde se guardan las mallas de lava con UV en coordenadas de mundo.</summary>
        public const string LavaMeshesPath = "Assets/_Project/Art/Meshes/Invierno/LavaBlue_UVMundo.asset";

        /// <summary>
        /// Metros que ocupa una repetición de la textura a lo ancho (X) y a lo largo (Z). Son las
        /// medidas de las albercas principales del Tutorial: un plano de Unity de 10 m escalado
        /// 3,4541745 × 2,7446938 muestra la imagen una vez, así que una repetición mide
        /// 34,54 × 27,45 m. Con estas medidas el texto se ve del mismo tamaño y con la misma
        /// proporción que en el Tutorial.
        /// </summary>
        public static readonly Vector2 LavaTileMeters = new Vector2(34.541745f, 27.446938f);

        // ------------------------------------------------------------------ portal final
        private const string FinalGateName = "LevelGate_EstacionFinal";
        private const string FinalAnchorName = "EsferaEnganche_PortalFinal";
        private const string AnchorTemplateName = "TEST_EsferaEnganche_LavaSimple";
        /// <summary>Altura de la esfera de gancho sobre el plano del portal, en m.</summary>
        private const float AnchorHeightAbovePortal = 2.5f;

        // ================================================================== menú
        /// <summary>Ejecuta todos los pasos en orden.</summary>
        [MenuItem(MenuRoot + "Aplicar todo", priority = 330)]
        public static void ApplyAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildFinalStation();
            ConnectDreamcoreToInvierno();
            SetupInvierno();
            Debug.Log("[Ciclo completo] Listo: Dreamcore → Invierno, lava LavaBlue, portal de la IA y estación final.");
        }

        // ================================================================== Dreamcore
        /// <summary>
        /// La meta de Dreamcore tenía dos puertas encimadas en z = 195: la puerta de nivel hacia
        /// Invierno y un portal heredado (<c>TutorialDoor</c>) que cargaba la escena de prueba
        /// OtherDimension y se cruzaba primero. Apago el portal heredado (igual que se hizo con la
        /// puerta del Tutorial al conectarlo con Verano) y me aseguro de que la puerta de nivel
        /// apunte a Invierno.
        /// </summary>
        [MenuItem(MenuRoot + "1. Dreamcore → Invierno", priority = 331)]
        public static void ConnectDreamcoreToInvierno()
        {
            Scene scene = EditorSceneManager.OpenScene(SceneNames.Paths.Dreamcore, OpenSceneMode.Single);
            IEnumerable<GameObject> all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);

            foreach (GameObject go in all)
            {
                MonoBehaviour door = go.GetComponents<MonoBehaviour>().FirstOrDefault(c => c != null && c.GetType().Name == "TutorialDoor");
                if (door == null || go.name != "Meta_PuertaDimensional") continue;
                go.SetActive(false);
                Debug.Log("[Ciclo completo] Portal heredado Meta_PuertaDimensional apagado en Dreamcore.");
            }

            LevelGate gate = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelGate>(true)).FirstOrDefault();
            if (gate == null)
            {
                Debug.LogError("[Ciclo completo] Dreamcore no tiene LevelGate de meta.");
                return;
            }
            gate.gameObject.SetActive(true);
            gate.Configure(SceneNames.InviernoIA, SceneNames.Paths.InviernoIA, gate.Aperture);
            EditorUtility.SetDirty(gate);
            LevelGateBuilder.EnsureInBuild(SceneNames.Paths.InviernoIA);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ================================================================== Invierno
        /// <summary>Lava con LavaBlue y portal de victoria en la escena de Invierno.</summary>
        [MenuItem(MenuRoot + "2. Invierno: lava LavaBlue y portal de la IA", priority = 332)]
        public static void SetupInvierno()
        {
            Scene scene = EditorSceneManager.OpenScene(SceneNames.Paths.InviernoIA, OpenSceneMode.Single);
            ReplaceInviernoLava(scene);
            PlaceVictoryPortal(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Cambia el material de las fosas de lava a LavaBlue. LavaBlue lee las UV de la malla
        /// (no la posición de mundo, como hacía BlueCodeLava), y las fosas de Invierno son cubos
        /// girados a lo largo de un arco: con sus UV originales cada tramo mostraría la imagen
        /// completa, estirada y girada de forma distinta. Por eso a cada tramo le hago una copia de
        /// su malla con UV calculadas desde la posición de mundo:
        /// <code>
        /// u = x_mundo / 34,54 − k_u      v = z_mundo / 27,45 − k_v
        /// </code>
        /// <list type="bullet">
        /// <item>Proporciones: una repetición mide lo mismo que en las albercas del Tutorial.</item>
        /// <item>Dirección: LavaBlue desplaza las UV con <c>_ScrollSped = (0; 0,5)</c>, así que el
        /// texto avanza hacia −v. Con v creciendo hacia +z, el texto avanza hacia −z, la misma
        /// dirección en que bajaba el código con BlueCodeLava. Con u creciendo hacia +x el texto no
        /// queda en espejo visto desde arriba.</item>
        /// <item>Continuidad: como las UV salen del mundo, el texto pasa de un tramo al siguiente
        /// sin cortes.</item>
        /// <item>k_u y k_v son enteros (la parte entera del centro de cada tramo). Restar un entero
        /// no cambia la imagen porque la textura se repite, y deja las UV cerca de 0: el nivel está
        /// en x = 9000 y sin esto las UV valdrían ~260, donde la precisión de 16 bits de los
        /// teléfonos ya no alcanza.</item>
        /// </list>
        /// El material es el mismo asset que usa el Tutorial, así que cualquier ajuste a LavaBlue
        /// se ve en los dos niveles.
        /// </summary>
        private static void ReplaceInviernoLava(Scene scene)
        {
            var lavaBlue = AssetDatabase.LoadAssetAtPath<Material>(LavaBlueMaterialPath);
            var oldLava = AssetDatabase.LoadAssetAtPath<Material>(OldInviernoLavaMaterialPath);
            if (lavaBlue == null || oldLava == null)
            {
                Debug.LogError("[Ciclo completo] No encontré LavaBlue.mat o M_Invierno_Lava.mat.");
                return;
            }

            List<MeshRenderer> targets = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true))
                .Where(r => r.sharedMaterials.Contains(oldLava))
                .ToList();
            if (targets.Count == 0)
            {
                Debug.Log("[Ciclo completo] La lava de Invierno ya usa LavaBlue.");
                return;
            }

            // Todas las mallas nuevas se guardan dentro de un solo archivo.
            var meshes = new List<Mesh>(targets.Count);
            foreach (MeshRenderer renderer in targets)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                Mesh mesh = Object.Instantiate(filter.sharedMesh);
                mesh.name = "LavaBlueUV_" + renderer.name;
                Vector3[] vertices = mesh.vertices;
                var uv = new Vector2[vertices.Length];
                Vector3 center = renderer.transform.position;
                float ku = Mathf.Floor(center.x / LavaTileMeters.x);
                float kv = Mathf.Floor(center.z / LavaTileMeters.y);
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 world = renderer.transform.TransformPoint(vertices[i]);
                    uv[i] = new Vector2(world.x / LavaTileMeters.x - ku, world.z / LavaTileMeters.y - kv);
                }
                mesh.uv = uv;
                mesh.RecalculateBounds();
                meshes.Add(mesh);

                filter.sharedMesh = mesh;
                Material[] materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                    if (materials[m] == oldLava) materials[m] = lavaBlue;
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
                EditorUtility.SetDirty(filter);
            }

            if (meshes.Count == 0) return;
            if (AssetDatabase.LoadAssetAtPath<Object>(LavaMeshesPath) != null) AssetDatabase.DeleteAsset(LavaMeshesPath);
            AssetDatabase.CreateAsset(meshes[0], LavaMeshesPath);
            for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], LavaMeshesPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Ciclo completo] {meshes.Count} tramos de lava de Invierno ahora usan LavaBlue.");
        }

        /// <summary>
        /// Deshace <see cref="ReplaceInviernoLava"/>: vuelve a poner el cubo original de Unity y el
        /// material <c>M_Invierno_Lava</c> (shader BlueCodeLava) en los tramos de lava de Invierno.
        /// Sirve para regresar a los shaders ShaderLab del commit 6c82207 (ver
        /// <c>documentos/Respaldo_Shaders_ShaderLab/README_RESTAURACION.md</c>).
        /// </summary>
        [MenuItem(MenuRoot + "Restaurar lava BlueCodeLava en Invierno", priority = 340)]
        public static void RestoreInviernoBlueCodeLava()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(SceneNames.Paths.InviernoIA, OpenSceneMode.Single);
            var lavaBlue = AssetDatabase.LoadAssetAtPath<Material>(LavaBlueMaterialPath);
            var oldLava = AssetDatabase.LoadAssetAtPath<Material>(OldInviernoLavaMaterialPath);
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            int restored = 0;
            foreach (MeshRenderer renderer in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.name.StartsWith("LavaBlueUV_")) continue;
                filter.sharedMesh = cube;
                Material[] materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                    if (materials[m] == lavaBlue) materials[m] = oldLava;
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
                EditorUtility.SetDirty(filter);
                restored++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Ciclo completo] {restored} tramos de lava de Invierno regresaron a BlueCodeLava. " +
                      $"Ya se puede borrar {LavaMeshesPath} si no se va a volver a usar.");
        }

        /// <summary>
        /// Coloca, apagados, el portal a la estación final y la esfera de gancho que aparecen al
        /// destruir a la IA. El portal queda acostado mirando hacia abajo, con el centro de su
        /// abertura donde flotaba el núcleo: el gancho jala al jugador hacia arriba, atraviesa la
        /// abertura de frente y cruza. La esfera es una copia de las esferas de gancho del nivel.
        /// </summary>
        private static void PlaceVictoryPortal(Scene scene)
        {
            AIBossController boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AIBossController>(true)).FirstOrDefault();
            if (boss == null)
            {
                Debug.LogError("[Ciclo completo] Invierno no tiene AIBossController.");
                return;
            }

            Transform arena = boss.transform.parent != null ? boss.transform.parent : boss.transform;
            Transform core = arena.Find("TEST_NucleoCentral_IA");
            Vector3 portalCenter = core != null ? core.position : arena.position + Vector3.up * 5.8f;

            // Portal: si ya existe se reutiliza.
            Transform existingGate = arena.Find(FinalGateName);
            GameObject gateObject;
            if (existingGate != null) gateObject = existingGate.gameObject;
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelGateBuilder.PrefabPath);
                gateObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                gateObject.name = FinalGateName;
                gateObject.transform.SetParent(arena, true);
            }

            // Mirando hacia abajo: forward = −Y y el "arriba" local de la puerta queda en +Z. El
            // pivote está en el borde inferior de la abertura, así que lo bajo media altura en −Z
            // para que el centro de la abertura quede donde estaba el núcleo.
            Vector2 aperture = LevelGateBuilder.Aperture;
            Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);
            Vector3 pivot = portalCenter - rotation * Vector3.up * (aperture.y * 0.5f);
            gateObject.transform.SetPositionAndRotation(pivot, rotation);

            var gate = gateObject.GetComponent<LevelGate>();
            gate.Configure(SceneNames.EstacionFinal, SceneNames.Paths.EstacionFinal, aperture);
            var gateSettings = new SerializedObject(gate);
            // La estación final pesa casi nada: se precarga en cuanto aparece el portal.
            gateSettings.FindProperty("preloadDelay").floatValue = 0.2f;
            gateSettings.ApplyModifiedPropertiesWithoutUndo();

            var visuals = gateObject.GetComponent<LevelGateVisuals>();
            if (visuals != null)
            {
                var look = new SerializedObject(visuals);
                look.FindProperty("colorTop").colorValue = new Color(0.78f, 0.45f, 1f, 1f);
                look.FindProperty("colorBottom").colorValue = new Color(0.45f, 0.3f, 1f, 1f);
                look.FindProperty("idleGlow").floatValue = 0.9f;
                look.FindProperty("glowIntensity").floatValue = 3.1f;
                look.ApplyModifiedPropertiesWithoutUndo();
            }

            // Esfera de gancho sobre el centro del portal.
            Transform existingAnchor = arena.Find(FinalAnchorName);
            GameObject anchor;
            if (existingAnchor != null) anchor = existingAnchor.gameObject;
            else
            {
                Transform template = scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(t => t.name == AnchorTemplateName);
                if (template != null)
                {
                    anchor = Object.Instantiate(template.gameObject, arena);
                }
                else
                {
                    anchor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    anchor.transform.SetParent(arena, false);
                    anchor.transform.localScale = Vector3.one * 1.65f;
                    anchor.layer = Layers.Grapple;
                }
                anchor.name = FinalAnchorName;
            }
            anchor.transform.position = portalCenter + Vector3.up * AnchorHeightAbovePortal;
            anchor.layer = Layers.Grapple;

            boss.ConfigureVictoryPortal(gateObject, anchor);
            EditorUtility.SetDirty(boss);

            // Se guardan apagados: AIBossController los enciende al vencer a la IA. Así tampoco
            // cuentan como objetos que la puerta de Dreamcore duerme y despierta.
            gateObject.SetActive(false);
            anchor.SetActive(false);
            LevelGateBuilder.EnsureInBuild(SceneNames.Paths.EstacionFinal);
            Debug.Log($"[Ciclo completo] Portal final en {portalCenter} y esfera de gancho a {AnchorHeightAbovePortal} m encima.");
        }

        // ================================================================== estación final
        /// <summary>
        /// Estación final provisional: solo un plano en el origen, un texto de victoria y una luz
        /// morada. No tiene enemigos, obstáculos ni scripts de juego; la puerta de llegada la crea
        /// <see cref="LevelGate"/> sola cuando la escena no trae una <c>LevelEntrance</c>
        /// (en (0; 0,05; 4) mirando a +Z). El origen está libre porque Invierno vive en x = 9000 y
        /// el Tutorial y Dreamcore (que también están en el origen) ya se descargaron.
        /// </summary>
        [MenuItem(MenuRoot + "3. Estación final", priority = 333)]
        public static void BuildFinalStation()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Plano de 200 × 200 m (el plano de Unity mide 10 m).
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Plano";
            floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);

            // Luz morada.
            var lightObject = new GameObject("Luz_Morada");
            lightObject.transform.SetPositionAndRotation(new Vector3(0f, 10f, 0f), Quaternion.Euler(50f, -30f, 0f));
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.62f, 0.28f, 1f, 1f);
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.1f, 0.24f, 1f);

            // Texto de victoria frente al punto de llegada (el jugador aparece mirando a +Z).
            var title = new GameObject("Texto_Ganaste");
            title.transform.SetPositionAndRotation(new Vector3(0f, 4.5f, 22f), Quaternion.identity);
            var text = title.AddComponent<TextMeshPro>();
            text.text = "¡GANASTE!\n<size=40%>Venciste a la IA y completaste las cuatro estaciones.</size>";
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(30f, 10f);

            EditorSceneManager.SaveScene(scene, SceneNames.Paths.EstacionFinal);
            LevelGateBuilder.EnsureInBuild(SceneNames.Paths.EstacionFinal);
            Debug.Log("[Ciclo completo] Estación final guardada en " + SceneNames.Paths.EstacionFinal);
        }
    }
}

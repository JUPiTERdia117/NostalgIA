// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: LevelKit.cs
// Propósito: piezas comunes para armar los niveles de prueba (rama Levels_test)
// solo con lo que ya existe en el Tutorial: sus materiales, sus prefabs y
// primitivas de Unity donde todavía no hay un asset propio.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NostalgIA.Core;
using NostalgIA.EditorTools.Setup;
using NostalgIA.Gameplay.Level;
using NostalgIA.Gameplay.Player;
using NostalgIA.Presentation.Audio;
using NostalgIA.Presentation.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NostalgIA.EditorTools.Levels
{
    /// <summary>
    /// Caja de herramientas para construir niveles con el kit visual del Tutorial. No crea
    /// materiales, mallas ni prefabs nuevos: todo sale de <c>Art/Materials/Primavera</c>,
    /// <c>Art/Materials/Prototype</c>, los prefabs que usa el Tutorial y las primitivas de Unity.
    /// </summary>
    /// <remarks>
    /// Código de colores que uso en los dos niveles (sale de los bocetos):
    /// <list type="bullet">
    /// <item>Turquesa (<c>Tutorial_Trim</c>, capa Grapple): superficies a las que el gancho se puede adherir.</item>
    /// <item>Gris (<c>Tutorial_Wall</c>): superficies y muros sin enganche.</item>
    /// <item>Cian brillante (<c>Tutorial_Grapple</c>): esferas de enganche, igual que en el Tutorial.</item>
    /// <item>Magenta (<c>Tutorial_HazardRail</c>): plataformas que se mueven.</item>
    /// <item>Rojo (<c>Tutorial_EnemyRed</c>): obstáculos que hacen daño o empujan.</item>
    /// <item>Verde (<c>Tutorial_Route</c>): pasto de las islas; verde translúcido (<c>Tutorial_Portal</c>): burbujas trampolín.</item>
    /// <item>Kit de estación (<c>M_Primavera_Kit</c>): pisos y muros del laberinto, como los pasillos del Tutorial.</item>
    /// </list>
    /// </remarks>
    public static class LevelKit
    {
        // ------------------------------------------------------------------ rutas del kit del Tutorial
        public const string PrimaveraFolder = "Assets/_Project/Art/Materials/Primavera/";
        public const string PrototypeFolder = "Assets/_Project/Art/Materials/Prototype/";
        public const string StarPrefab = "Assets/_Project/Prefabs/Level/Star.prefab";
        public const string CratePrefab = "Assets/_Project/Prefabs/Absorbables/Poolable_Crate.prefab";
        public const string LavaPrefab = "Assets/_Project/Prefabs/Hazards/Lava.prefab";
        public const string FlyingEmailPrefab = "Assets/_Project/Prefabs/Enemies/FlyingEmail.prefab";
        public const string HexbugPrefab = "Assets/_Project/Prefabs/Enemies/Hexbug.prefab";
        public const string HorsePrefab = "Assets/_Project/Prefabs/Enemies/Horse.prefab";
        public const string WinRarPrefab = "Assets/_Project/Prefabs/Enemies/WinRar.prefab";
        public const string SkyboxMaterial = PrimaveraFolder + "SKY_Primavera.mat";
        public const string VolumeProfile = "Assets/_Project/Rendering/Volumes/VP_Primavera.asset";
        public const string VolumeProfileLdr = "Assets/_Project/Rendering/Volumes/VP_Primavera_LDR.asset";

        // Nombres cortos de los materiales del Tutorial que uso.
        public const string Kit = "M_Primavera_Kit";
        public const string Trim = "Tutorial_Trim";
        public const string Wall = "Tutorial_Wall";
        public const string Floor = "Tutorial_Floor";
        public const string Grapple = "Tutorial_Grapple";
        public const string Moving = "Tutorial_HazardRail";
        public const string Hazard = "Tutorial_EnemyRed";
        public const string Grass = "Tutorial_Route";
        public const string Bubble = "Tutorial_Portal";
        public const string Beacon = "Tutorial_Collectible";
        public const string Water = "Objects";
        public const string Ammo = "Poolable";

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly Dictionary<PrimitiveType, Mesh> BuiltinMeshes = new Dictionary<PrimitiveType, Mesh>();

        /// <summary>Escena que se está construyendo.</summary>
        public static Scene Scene { get; private set; }

        /// <summary>Raíz del contenido del nivel.</summary>
        public static Transform Root { get; private set; }

        // ================================================================== escena
        /// <summary>Abre una escena vacía y crea la raíz del nivel.</summary>
        public static void BeginScene(string rootName)
        {
            LoadMaterials();
            Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Root = new GameObject(rootName).transform;
        }

        /// <summary>
        /// Mueve todo a la región del nivel, hornea la malla de navegación (la necesita el WinRar)
        /// y guarda la escena en la lista del build.
        /// </summary>
        public static void EndScene(string scenePath, Vector3 regionOrigin, Bounds navBoundsLocal)
        {
            LevelRegionTools.ShiftSceneRoots(Scene, regionOrigin);
            Physics.SyncTransforms();
            EditorSceneManager.SaveScene(Scene, scenePath);

            navBoundsLocal.center += regionOrigin;
            BakeNavMesh(scenePath, navBoundsLocal);

            EditorSceneManager.MarkSceneDirty(Scene);
            EditorSceneManager.SaveScene(Scene, scenePath);
            AssetDatabase.SaveAssets();
            LevelGateBuilder.EnsureInBuild(scenePath);
        }

        /// <summary>Material del kit del Tutorial por nombre corto.</summary>
        public static Material Mat(string name)
        {
            if (Materials.Count == 0) LoadMaterials();
            return Materials.TryGetValue(name, out Material m) ? m : null;
        }

        private static void LoadMaterials()
        {
            Materials.Clear();
            foreach (string folder in new[] { PrimaveraFolder, PrototypeFolder })
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder.TrimEnd('/') }))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (m != null && !Materials.ContainsKey(m.name)) Materials[m.name] = m;
                }
            }
        }

        // ================================================================== ambiente del Tutorial
        /// <summary>
        /// Misma luz, cielo, ambiente y postproceso que el Tutorial: así los niveles nuevos se ven
        /// como parte del mismo juego y se reutiliza el perfil LDR para móvil.
        /// </summary>
        public static void TutorialAtmosphere(bool fog, Color fogColor, float fogStart, float fogEnd)
        {
            Transform group = Group("Ambiente");
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterial);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.24f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.11f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.03f, 0.09f);
            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            var sunGo = new GameObject("Directional Light");
            sunGo.transform.SetParent(group, false);
            sunGo.transform.rotation = new Quaternion(0.5333118f, -0.24929643f, 0.16815256f, 0.79066724f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.92f, 0.95f, 1f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;

            var volumeGo = new GameObject("Volume_Primavera");
            volumeGo.transform.SetParent(group, false);
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfile);
            LdrVolumeProfileSwap swap = volumeGo.AddComponent<LdrVolumeProfileSwap>();
            var so = new SerializedObject(swap);
            so.FindProperty("ldrProfile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfileLdr);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================== jugador independiente y llegada
        /// <summary>
        /// Jugador y EventSystem para poder abrir el nivel solo; <see cref="StandaloneLevelContent"/>
        /// los apaga cuando se llega desde otro nivel por la puerta.
        /// </summary>
        public static void StandalonePlayer(Vector3 position, Quaternion rotation)
        {
            var standalone = new GameObject("--- STANDALONE ---");
            var bootstrap = new GameObject("LevelBootstrap");
            bootstrap.AddComponent<StandaloneLevelContent>().Configure(standalone);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1Builder.Paths.Player);
            if (prefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Scene);
                player.transform.SetPositionAndRotation(position, rotation);
                player.transform.SetParent(standalone.transform, true);
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
            eventSystem.transform.SetParent(standalone.transform, false);
            standalone.SetActive(false);
        }

        /// <summary>Marco de llegada donde aparece el jugador al cruzar la puerta del nivel anterior.</summary>
        public static void Entrance(Vector3 position, Quaternion rotation)
        {
            var entrance = new GameObject("LevelEntrance_PuertaInicio");
            entrance.transform.SetParent(Root, false);
            entrance.transform.SetPositionAndRotation(position, rotation);
            entrance.AddComponent<LevelEntrance>().Configure(string.Empty, LevelGateBuilder.Aperture);
        }

        /// <summary>Puerta de nivel (prefab actual) hacia otra escena.</summary>
        public static LevelGate Gate(string name, Transform parent, Vector3 pivot, Quaternion rotation, string destinationName, string destinationPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelGateBuilder.PrefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Scene);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pivot, rotation);
            LevelGate gate = go.GetComponent<LevelGate>();
            gate.Configure(destinationName, destinationPath, LevelGateBuilder.Aperture);
            return gate;
        }

        // ================================================================== geometría
        /// <summary>Caja con collider. <paramref name="layer"/> = Grapple si el gancho se puede adherir.</summary>
        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Quaternion rotation,
                                     string material, int layer = Layers.Default, bool isStatic = true)
        {
            GameObject go = Mesh(name, parent, PrimitiveType.Cube, material, center, size, rotation);
            go.layer = layer;
            go.AddComponent<BoxCollider>();
            if (isStatic) MarkStatic(go);
            return go;
        }

        /// <summary>
        /// Cilindro con su cara superior a la altura <paramref name="topY"/> y la base hundida a
        /// <paramref name="bottomY"/>; los radios permiten óvalos (islas). Lleva MeshCollider
        /// convexo para que el borde siga la forma real.
        /// </summary>
        public static GameObject Cylinder(string name, Transform parent, Vector3 centerXZ, float radiusX, float radiusZ,
                                          float bottomY, float topY, string material, int layer = Layers.Default, float yaw = 0f)
        {
            float height = Mathf.Max(0.05f, topY - bottomY);
            Vector3 center = new Vector3(centerXZ.x, bottomY + height * 0.5f, centerXZ.z);
            GameObject go = Mesh(name, parent, PrimitiveType.Cylinder, material, center,
                new Vector3(radiusX * 2f, height * 0.5f, radiusZ * 2f), Quaternion.Euler(0f, yaw, 0f));
            go.layer = layer;
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = Builtin(PrimitiveType.Cylinder);
            col.convex = true;
            MarkStatic(go);
            return go;
        }

        /// <summary>Objeto visual con malla primitiva y material del kit (sin collider).</summary>
        public static GameObject Mesh(string name, Transform parent, PrimitiveType type, string material,
                                      Vector3 position, Vector3 scale, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = Builtin(type);
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat(material);
            return go;
        }

        /// <summary>
        /// Plataforma que va y viene. El collider vive en la raíz y la malla en un hijo, como en
        /// los niveles anteriores, para que <see cref="MovingPlatform"/> arrastre al jugador.
        /// </summary>
        public static GameObject MovingPlatform(string name, Transform parent, Vector3 position, Vector3 size, Quaternion rotation,
                                                Vector3 travel, float oneWaySeconds, float phase, int layer = Layers.Default)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            root.layer = layer;
            root.AddComponent<BoxCollider>().size = size;

            GameObject mesh = Mesh("Malla", root.transform, PrimitiveType.Cube, Moving, position, size, rotation);
            mesh.layer = layer;

            MovingPlatform platform = root.AddComponent<MovingPlatform>();
            platform.Configure(travel, oneWaySeconds, phase, new Vector3(size.x * 0.48f, 0.8f, size.z * 0.48f), size.y * 0.5f + 0.45f);
            return root;
        }

        /// <summary>Esfera de enganche igual a las del Tutorial (1,5 m, material Tutorial_Grapple).</summary>
        public static GameObject GrappleSphere(string name, Transform parent, Vector3 position, float diameter = 1.5f)
        {
            GameObject go = Mesh(name, parent, PrimitiveType.Sphere, Grapple, position, Vector3.one * diameter, Quaternion.identity);
            go.layer = Layers.Grapple;
            go.AddComponent<SphereCollider>();
            MarkStatic(go);
            return go;
        }

        /// <summary>
        /// Burbuja trampolín: esfera disparadora con <see cref="BounceBubble"/>. Su frente (+Z) mira
        /// hacia <paramref name="aimAt"/> para empujar al jugador hacia ese lado.
        /// </summary>
        public static GameObject TrampolineBubble(string name, Transform parent, Vector3 position, float diameter,
                                                  float launchHeight, float forwardBoost, Vector3 aimAt)
        {
            Vector3 flat = Vector3.ProjectOnPlane(aimAt - position, Vector3.up);
            Quaternion facing = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat.normalized) : Quaternion.identity;
            GameObject go = Mesh(name, parent, PrimitiveType.Sphere, Bubble, position, Vector3.one * diameter, facing);
            go.layer = Layers.Trigger;
            go.AddComponent<SphereCollider>().isTrigger = true;
            go.AddComponent<BounceBubble>().Configure(launchHeight, forwardBoost);
            return go;
        }

        /// <summary>
        /// Aspas giratorias rojas. Con <paramref name="cross"/> son cuatro aspas en cruz; si no, una
        /// barra de dos aspas. El daño y el empuje los resuelve <see cref="RotatingHazardObstacle"/>.
        /// </summary>
        public static GameObject Spinner(string name, Transform parent, Vector3 pivot, float radius, float speedDeg, bool cross,
                                         float bladeHeight = 0.55f, bool withPost = true)
        {
            var spinner = new GameObject(name);
            spinner.transform.SetParent(parent, false);
            spinner.transform.position = pivot;
            spinner.layer = Layers.Hazard;

            // Sobre plataformas móviles el eje estorbaría su paso: ahí las aspas flotan sin poste.
            if (withPost)
                Box("Eje", spinner.transform, pivot + Vector3.up * 0.7f, new Vector3(0.9f, 1.4f, 0.9f), Quaternion.identity, Wall, Layers.Default, false);
            else
                Mesh("Buje", spinner.transform, PrimitiveType.Sphere, Wall, pivot + Vector3.up * bladeHeight, Vector3.one * 0.7f, Quaternion.identity);
            Mesh("Aspa_A", spinner.transform, PrimitiveType.Cube, Hazard, pivot + Vector3.up * bladeHeight,
                new Vector3(0.45f, 0.4f, radius * 2f), Quaternion.identity).layer = Layers.Hazard;
            if (cross)
            {
                Mesh("Aspa_B", spinner.transform, PrimitiveType.Cube, Hazard, pivot + Vector3.up * bladeHeight,
                    new Vector3(radius * 2f, 0.4f, 0.45f), Quaternion.identity).layer = Layers.Hazard;
            }

            spinner.AddComponent<RotatingHazardObstacle>()
                .Configure(speedDeg, cross, radius, new Vector2(0.35f, 0.32f), bladeHeight, 1, 10.5f, 4.8f);
            return spinner;
        }

        /// <summary>
        /// Bloque rojo que barre de lado a lado (<see cref="HazardMotion"/>, oscilación) y empuja al
        /// jugador al tocarlo. <paramref name="travelLocal"/> va en los ejes del soporte.
        /// </summary>
        public static GameObject SweeperBlock(string name, Transform parent, Vector3 center, Quaternion rotation, Vector3 size,
                                              Vector3 travelLocal, float period, float phase)
        {
            var holder = new GameObject(name + "_Soporte");
            holder.transform.SetParent(parent, false);
            holder.transform.SetPositionAndRotation(center, rotation);

            GameObject block = Box(name, holder.transform, center, size, rotation, Hazard, Layers.Default, false);
            Rigidbody body = block.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            block.AddComponent<HazardMotion>().Configure(HazardMotion.MotionKind.Oscillate, travelLocal, period, phase);

            var push = new GameObject("ZonaEmpuje");
            push.transform.SetParent(block.transform, false);
            push.layer = Layers.Hazard;
            var trigger = push.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.1f, 0.95f, 1.1f);
            push.AddComponent<ContactHazard>().Configure(ContactHazard.HazardEffect.Knockback, 12f, 4.2f, 1, false);
            return block;
        }

        /// <summary>Pared que baja desde arriba hasta el piso y vuelve a subir (aplasta).</summary>
        public static GameObject CrusherBlock(string name, Transform parent, Vector3 center, Quaternion rotation, Vector3 size,
                                              float drop, float period, float phase)
        {
            var holder = new GameObject(name + "_Soporte");
            holder.transform.SetParent(parent, false);
            holder.transform.SetPositionAndRotation(center, rotation);

            GameObject block = Box(name, holder.transform, center, size, rotation, Hazard, Layers.Default, false);
            Rigidbody body = block.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            block.AddComponent<HazardMotion>().Configure(HazardMotion.MotionKind.Crusher, new Vector3(0f, -drop, 0f), period, phase);

            var crush = new GameObject("ZonaAplastamiento");
            crush.transform.SetParent(block.transform, false);
            crush.transform.localPosition = new Vector3(0f, -0.52f, 0f);
            crush.layer = Layers.Hazard;
            var trigger = crush.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.92f, 0.22f, 0.88f);
            crush.AddComponent<ContactHazard>().Configure(ContactHazard.HazardEffect.Instakill, 0f, 0f, 99, true);
            return block;
        }

        /// <summary>
        /// Tramo de lava con el prefab <c>Lava</c> del Tutorial (plano de 10 × 10 m con
        /// <c>LavaHazard</c>), escalado al tamaño pedido y hundido para que el piso firme vecino
        /// nunca cuente como lava. Solo uno de cada <paramref name="lightEvery"/> tramos conserva
        /// su luz de resplandor: muchas luces en tiempo real cuestan demasiado en teléfonos.
        /// </summary>
        public static GameObject LavaPatch(string name, Transform parent, Vector3 surfaceCenter, float sizeX, float sizeZ,
                                           Quaternion rotation, bool keepLight)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LavaPrefab);
            var lava = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Scene);
            lava.name = name;
            lava.transform.SetParent(parent, false);
            lava.transform.SetPositionAndRotation(surfaceCenter, rotation);
            lava.transform.localScale = new Vector3(sizeX / 10f, 1f, sizeZ / 10f);
            if (!keepLight)
                foreach (Light l in lava.GetComponentsInChildren<Light>(true)) l.gameObject.SetActive(false);
            return lava;
        }

        // ================================================================== contenido de juego
        /// <summary>Caja absorbible del Tutorial (prefab <c>Poolable_Crate</c>).</summary>
        public static GameObject Crate(string name, Transform parent, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CratePrefab);
            var crate = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Scene);
            crate.name = name;
            crate.transform.SetParent(parent, false);
            crate.transform.position = position;
            return crate;
        }

        /// <summary>
        /// Surtidor que repone una caja absorbible al poco tiempo de tomarla, para que las peleas
        /// nunca se queden sin munición. Usa el cubo y el material <c>Poolable</c> del Tutorial.
        /// </summary>
        public static GameObject AmmoPad(string name, Transform parent, Vector3 floor)
        {
            GameObject pad = Mesh(name, parent, PrimitiveType.Cylinder, Trim, floor + Vector3.up * 0.05f,
                new Vector3(1.35f, 0.05f, 1.35f), Quaternion.identity);
            pad.AddComponent<RespawningAbsorbablePad>().Configure(Builtin(PrimitiveType.Cube), Mat(Ammo), 0.44f, 3.0f);
            return pad;
        }

        /// <summary>Punto de control como los del Tutorial: disparador invisible con Checkpoint.</summary>
        public static Checkpoint CheckpointAt(string label, Transform parent, Vector3 ground, float yaw, Vector3 size = default)
        {
            if (size == default) size = new Vector3(8f, 4f, 8f);
            var go = new GameObject("Checkpoint_" + label);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
            go.layer = Layers.Trigger;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return go.AddComponent<Checkpoint>();
        }

        /// <summary>Instancia un enemigo del proyecto.</summary>
        public static GameObject Enemy(string prefabPath, string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return null;
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Scene);
            enemy.name = name;
            enemy.transform.SetParent(parent, false);
            enemy.transform.SetPositionAndRotation(position, rotation);
            return enemy;
        }

        /// <summary>Mantiene a un enemigo terrestre sobre su plataforma (techo, isla o plataforma de lava).</summary>
        public static void Leash(GameObject enemy, Transform surface, Vector2 halfExtents)
        {
            if (enemy == null || surface == null) return;
            enemy.AddComponent<PlatformBoundsConstraint>().Configure(surface, halfExtents);
        }

        /// <summary>Alcance de detección del WinRar (BugEnemyController).</summary>
        public static void TuneWinRar(GameObject enemy, float detectionRange)
        {
            if (enemy == null) return;
            MonoBehaviour bug = enemy.GetComponents<MonoBehaviour>().FirstOrDefault(c => c != null && c.GetType().Name == "BugEnemyController");
            if (bug == null) return;
            var so = new SerializedObject(bug);
            SerializedProperty range = so.FindProperty("detectionRange");
            if (range != null) range.floatValue = detectionRange;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Estrella del proyecto con su índice dentro del nivel.</summary>
        public static GameObject Star(string name, Transform parent, Vector3 position, int index)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StarPrefab);
            var star = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Scene);
            star.name = name;
            star.transform.SetParent(parent, true);
            star.transform.position = position;
            MonoBehaviour controller = star.GetComponents<MonoBehaviour>().FirstOrDefault(c => c != null && c.GetType().Name == "StarController");
            if (controller != null)
            {
                var so = new SerializedObject(controller);
                SerializedProperty idx = so.FindProperty("starIndex");
                if (idx != null) idx.intValue = index;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (Light l in star.GetComponentsInChildren<Light>(true)) l.shadows = LightShadows.None;
            return star;
        }

        /// <summary>
        /// Al derrotar a <paramref name="targets"/> mueve la barrera, enciende lo que se indique (por
        /// ejemplo la estrella, que aparece donde cayó el enemigo) y muestra un aviso.
        /// </summary>
        public static DefeatUnlockController Unlock(string name, Transform parent, GameObject[] targets, Transform barrier,
                                                    Vector3 openOffset, float duration, GameObject[] activate, string toast, bool dropAtEnemy)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = barrier != null ? barrier.position : Vector3.zero;
            var unlock = go.AddComponent<DefeatUnlockController>();
            unlock.Configure(targets.Where(t => t != null).ToArray(), barrier, openOffset, duration, activate, null, toast, dropAtEnemy);
            AudioSlot(go, $"Sonido de desbloqueo ({name})", AudioSlotTrigger.ArenaGateOpen, 1f, 45f, 0.9f);
            return unlock;
        }

        /// <summary>Ranura de audio sin clip (el equipo asigna su sonido después).</summary>
        public static void AudioSlot(GameObject target, string description, AudioSlotTrigger trigger, float spatialBlend, float maxDistance, float volume)
        {
            if (target.GetComponent<AudioSource>() == null) target.AddComponent<AudioSource>();
            LevelAudioSlot slot = target.GetComponent<LevelAudioSlot>() ?? target.AddComponent<LevelAudioSlot>();
            slot.Configure(description, trigger, true, spatialBlend, maxDistance, volume);
        }

        // ================================================================== utilidades
        public static Transform Group(string name) => Child(Root, name);

        public static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>Estático para batching y oclusión (la luz se hornea después con Etapa 3).</summary>
        public static void MarkStatic(GameObject go)
        {
            const StaticEditorFlags flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
                                            StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
                                            StaticEditorFlags.ReflectionProbeStatic;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
        }

        public static Mesh Builtin(PrimitiveType type)
        {
            if (BuiltinMeshes.TryGetValue(type, out Mesh cached) && cached != null) return cached;
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            BuiltinMeshes[type] = mesh;
            return mesh;
        }

        /// <summary>
        /// Hornea la malla de navegación solo dentro de <paramref name="worldBounds"/> (el WinRar
        /// camina con NavMeshAgent).
        /// </summary>
        /// <remarks>
        /// Junto las fuentes yo misma en lugar de dejar que NavMeshSurface las recoja, porque así
        /// puedo quitar lo que no es piso: los triggers (el colchón del agua, la lava, los puntos de
        /// control), lo que se mueve (plataformas, aspas, cajas) y los propios enemigos. Con los
        /// triggers adentro la malla quedaba 1,4 m arriba del agua.
        /// <para>
        /// El componente NavMeshSurface va en la raíz del nivel: la puerta de nivel nunca apaga las
        /// raíces que tienen hijos, así la malla ya existe cuando despiertan los WinRar. Lo uso por
        /// reflexión para no agregar otra dependencia al ensamblado del editor.
        /// </para>
        /// </remarks>
        private static void BakeNavMesh(string scenePath, Bounds worldBounds)
        {
            Type surfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (surfaceType == null)
            {
                Debug.LogWarning("[Niveles] No encontré NavMeshSurface; el WinRar no tendrá malla de navegación.");
                return;
            }

            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(worldBounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(source => !IsWalkableSource(source));

            Transform host = Root;
            Vector3 localCenter = Quaternion.Inverse(host.rotation) * (worldBounds.center - host.position);
            var localBounds = new Bounds(localCenter, worldBounds.size);
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, localBounds, host.position, host.rotation);
            if (data == null) return;

            string folder = System.IO.Path.ChangeExtension(scenePath, null);
            Phase1Builder.EnsureFolder(folder);
            string assetPath = folder + "/NavMesh-" + System.IO.Path.GetFileNameWithoutExtension(scenePath) + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
            data.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(data, assetPath);

            Component surface = host.gameObject.AddComponent(surfaceType);
            PropertyInfo collect = surfaceType.GetProperty("collectObjects");
            collect.SetValue(surface, Enum.ToObject(collect.PropertyType, 1));   // CollectObjects.Volume
            surfaceType.GetProperty("size").SetValue(surface, worldBounds.size);
            surfaceType.GetProperty("center").SetValue(surface, localCenter);
            surfaceType.GetProperty("useGeometry").SetValue(surface, NavMeshCollectGeometry.PhysicsColliders);
            surfaceType.GetProperty("navMeshData").SetValue(surface, data);
        }

        /// <summary>Solo cuentan como piso los colliders sólidos y quietos que no son de un enemigo.</summary>
        private static bool IsWalkableSource(NavMeshBuildSource source)
        {
            if (!(source.component is Collider collider)) return true;
            if (collider.isTrigger || collider.attachedRigidbody != null) return false;
            if (collider.GetComponentInParent<NavMeshAgent>(true) != null) return false;
            // Estos componentes vuelven trigger a su collider hasta el Awake; en el editor todavía
            // se ven sólidos, así que los quito aquí.
            GameObject go = collider.gameObject;
            if (go.GetComponent<WaterFallCushion>() || go.GetComponent<BounceBubble>() || go.GetComponent<ContactHazard>() ||
                go.GetComponent<PromptZone>() || go.GetComponent<KillVolume>()) return false;
            int layer = go.layer;
            return layer != Layers.Enemy && layer != Layers.Player && layer != Layers.Trigger;
        }
    }
}

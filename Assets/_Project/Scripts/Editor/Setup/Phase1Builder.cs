// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: Phase1Builder.cs
// Propósito: constructor idempotente que crea los assets de la Fase 1, completa
// Player.prefab y aplica una única vez en el Tutorial los ajustes que el
// prototipo hacía por código en cada arranque (tareas F1-A13 a F1-A16).
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using NostalgIA.Core;
using NostalgIA.Core.Pooling;
using NostalgIA.EditorTools.Validation;
using NostalgIA.Gameplay.Aim;
using NostalgIA.Gameplay.Bootstrap;
using NostalgIA.Gameplay.Player;
using NostalgIA.Gameplay.Tools;
using NostalgIA.Input;
using NostalgIA.Input.TouchControls;
using NostalgIA.Presentation.CameraFX;
using NostalgIA.Presentation.UI;
using NostalgIA.Presentation.VFX;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MotorSettings = NostalgIA.Gameplay.Player.PlayerSettings;

namespace NostalgIA.EditorTools.Setup
{
    /// <summary>
    /// Constructor de editor de la Fase 1 (menú <c>NostalgIA › Fase 1</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// En este módulo se decidió configurar escenas y prefabs con un constructor en lugar de
    /// editarlos a mano: el procedimiento queda documentado en código, se puede repetir tras un
    /// conflicto de fusión y es <b>idempotente</b> (cada paso busca lo que ya existe antes de crear
    /// algo, de modo que ejecutarlo dos veces no duplica objetos).
    /// </para>
    /// <para>Pasos de <see cref="BuildAll"/>:</para>
    /// <list type="number">
    /// <item>Assets de datos con los valores del prototipo (<c>PlayerSettings</c>, <c>VacuumStats</c>,
    /// <c>GrappleStats</c>, <c>InputReader</c>, <c>TouchLayoutSettings</c>).</item>
    /// <item>Pool de fragmentos (prefab, material y <c>PoolDefinition</c>).</item>
    /// <item>Prefab persistente <c>GameRoot</c> en una carpeta <c>Resources</c>.</item>
    /// <item>Prefabs de interfaz: retícula y controles táctiles.</item>
    /// <item><c>Player.prefab</c>: capas, componentes nuevos y referencias.</item>
    /// <item>Tutorial: checkpoints, estrellas, islas estáticas, volumen de vacío y disposición del HUD.</item>
    /// <item>Acabado visual con <see cref="Phase1VisualBuilder"/>.</item>
    /// <item>Validación del proyecto y reporte.</item>
    /// </list>
    /// </remarks>
    public static class Phase1Builder
    {
        // ------------------------------------------------------------------ rutas
        private const string MenuRoot = "NostalgIA/Fase 1/";
        private const string DataPlayer = "Assets/_Project/Data/Player";
        private const string DataTools = "Assets/_Project/Data/Tools";
        private const string DataInput = "Assets/_Project/Data/Input";
        private const string DataPools = "Assets/_Project/Data/Pools";
        private const string CoreResources = "Assets/_Project/Prefabs/Core/Resources";
        private const string UIPrefabs = "Assets/_Project/Prefabs/UI";
        private const string VfxPrefabs = "Assets/_Project/Art/VFX/Prefabs";
        private const string VfxMaterials = "Assets/_Project/Art/Materials/VFX";

        /// <summary>Ruta de los assets que crea el constructor (las usan también las pruebas).</summary>
        public static class Paths
        {
            public const string PlayerSettings = DataPlayer + "/PlayerSettings_Default.asset";
            public const string VacuumStats = DataTools + "/VacuumStats_Default.asset";
            public const string GrappleStats = DataTools + "/GrappleStats_Default.asset";
            public const string InputReader = DataInput + "/InputReader.asset";
            public const string TouchLayout = DataInput + "/TouchLayoutSettings_Default.asset";
            public const string DebrisPool = DataPools + "/Debris_Pool.asset";
            public const string DebrisPrefab = VfxPrefabs + "/DebrisFragment.prefab";
            public const string DebrisMaterial = VfxMaterials + "/Debris_Default.mat";
            public const string GameRoot = CoreResources + "/" + NostalgIA.Gameplay.Bootstrap.GameRoot.ResourcePath + ".prefab";
            public const string Reticle = UIPrefabs + "/HUD_Reticle.prefab";
            public const string TouchControls = UIPrefabs + "/TouchControls.prefab";
            public const string Player = "Assets/_Project/Prefabs/Player/Player.prefab";
            public const string InputActions = "Assets/_Project/Input/InputSystem_Actions.inputactions";
        }

        /// <summary>
        /// Coordenadas de los checkpoints del Tutorial, verificadas en el prototipo dentro de los
        /// muros y sobre piso firme. Antes se aplicaban por código en cada arranque; ahora se
        /// escriben una vez en la escena, que pasa a ser la fuente de verdad (TDD §5.3).
        /// </summary>
        private static readonly (string name, Vector3 pos, float yaw, Vector3 size, string label)[] TutorialCheckpoints =
        {
            ("Checkpoint_1",  new Vector3(-12.3f, 1.0f,  21.0f), 180f, new Vector3(10f, 5f, 5f), "Punto de control: Inicio"),
            ("Checkpoint_2",  new Vector3(-12.3f, 1.0f, -14.0f), 180f, new Vector3(10f, 5f, 5f), "Punto de control: Agilidad"),
            ("Checkpoint_3",  new Vector3(-10.0f, 1.0f, -48.0f),  90f, new Vector3(10f, 5f, 8f), "Punto de control: Sala de Combate"),
            ("Checkpoint_4",  new Vector3( 42.0f, 1.0f, -49.0f), 135f, new Vector3(10f, 5f, 8f), "Punto de control: Zona Este"),
            ("Checkpoint_5",  new Vector3( 54.5f, 1.0f, -61.0f), 180f, new Vector3(10f, 5f, 6f), "Punto de control: Muralla de Cajas"),
            ("Checkpoint_6",  new Vector3( 56.5f, 1.0f,-114.0f),  90f, new Vector3(10f, 5f, 9f), "Punto de control: Abismo Azul"),
            ("Checkpoint_7",  new Vector3( 98.0f, 1.0f,-114.0f),  90f, new Vector3( 9f, 5f, 9f), "Punto de control: Isla Segura 1"),
            ("Checkpoint_8",  new Vector3(138.0f, 1.0f,-114.0f),  90f, new Vector3( 9f, 5f, 9f), "Punto de control: Isla Segura 2"),
            ("Checkpoint_9",  new Vector3(246.0f, 1.0f,-114.0f),   0f, new Vector3(12f, 5f, 9f), "Punto de control: Gran Canal"),
            ("Checkpoint_10", new Vector3(243.2f, 1.2f,  61.0f),   0f, new Vector3( 9f, 5f, 6f), "Punto de control: Plataforma Arena"),
            ("Checkpoint_11", new Vector3(243.5f,19.0f,  95.0f),   0f, new Vector3(10f, 6f, 6f), "Punto de control: Escalera al Portal")
        };

        /// <summary>Altura de la cara superior del volumen de vacío (umbral del prototipo).</summary>
        private const float VoidHeight = -15f;

        // ================================================================== menús
        /// <summary>Ejecuta todos los pasos en orden.</summary>
        [MenuItem(MenuRoot + "Montar todo", priority = 100)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            CreateDataAssets();
            CreateDebrisPool();
            CreateGameRoot();
            CreateReticlePrefab();
            CreateTouchControlsPrefab();
            ConfigurePlayerPrefab();
            ConfigureTutorialScene();
            Phase1VisualBuilder.BuildAllWithoutPrompt();   // Acabado visual (Pista B): materiales, VFX, postprocesado.
            AssetDatabase.SaveAssets();

            ValidationReport report = ProjectValidator.Run();
            ProjectValidator.WriteReport(report);
            Debug.Log($"[Fase 1] Montaje terminado. Validación: {report.ErrorCount} errores, {report.WarningCount} advertencias.");
        }

        /// <summary>Paso 1: assets de datos.</summary>
        [MenuItem(MenuRoot + "1. Assets de datos", priority = 111)]
        public static void CreateDataAssets()
        {
            EnsureFolder(DataPlayer);
            EnsureFolder(DataTools);
            EnsureFolder(DataInput);

            LoadOrCreate<MotorSettings>(Paths.PlayerSettings);
            LoadOrCreate<VacuumStats>(Paths.VacuumStats);
            LoadOrCreate<GrappleStats>(Paths.GrappleStats);
            LoadOrCreate<TouchLayoutSettings>(Paths.TouchLayout);

            InputReader reader = LoadOrCreate<InputReader>(Paths.InputReader);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Paths.InputActions);
            SetObject(reader, "actions", actions);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Paso 2: pool de fragmentos (material, prefab y definición).</summary>
        [MenuItem(MenuRoot + "2. Pool de fragmentos", priority = 112)]
        public static void CreateDebrisPool()
        {
            EnsureFolder(VfxPrefabs);
            EnsureFolder(VfxMaterials);
            EnsureFolder(DataPools);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(Paths.DebrisMaterial);
            if (material == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(lit != null ? lit : Shader.Find("Standard")) { name = "Debris_Default" };
                material.SetColor("_BaseColor", new Color(0.55f, 0.6f, 0.68f, 1f));
                AssetDatabase.CreateAsset(material, Paths.DebrisMaterial);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<PooledObject>(Paths.DebrisPrefab);
            if (prefab == null)
            {
                var go = new GameObject("DebrisFragment");
                try
                {
                    go.layer = Layers.Debris;
                    go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    go.AddComponent<BoxCollider>();
                    var body = go.AddComponent<Rigidbody>();
                    body.mass = 0.15f;
                    body.interpolation = RigidbodyInterpolation.None;
                    go.AddComponent<PooledObject>();
                    go.AddComponent<DebrisFragment>();
                    prefab = PrefabUtility.SaveAsPrefabAsset(go, Paths.DebrisPrefab).GetComponent<PooledObject>();
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }

            PoolDefinition pool = LoadOrCreate<PoolDefinition>(Paths.DebrisPool);
            pool.Configure(prefab, 24, 64, 12, 32);
            EditorUtility.SetDirty(pool);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Paso 3: prefab persistente de servicios.</summary>
        [MenuItem(MenuRoot + "3. GameRoot", priority = 113)]
        public static void CreateGameRoot()
        {
            EnsureFolder(CoreResources);
            var reader = AssetDatabase.LoadAssetAtPath<InputReader>(Paths.InputReader);
            var pool = AssetDatabase.LoadAssetAtPath<PoolDefinition>(Paths.DebrisPool);

            GameObject root = LoadPrefabForEdit(Paths.GameRoot, "GameRoot", out bool isNew);
            try
            {
                GameRoot gameRoot = GetOrAdd<GameRoot>(root);
                SetObject(gameRoot, "inputReader", reader);
                SetObjectArray(gameRoot, "prewarmPools", pool);

                Transform vfx = GetOrCreateChild(root.transform, "VFX");
                DebrisBurstEmitter emitter = GetOrAdd<DebrisBurstEmitter>(vfx.gameObject);
                SetObject(emitter, "debrisPool", pool);

                SavePrefab(root, Paths.GameRoot, isNew);
            }
            finally
            {
                UnloadPrefab(root, isNew);
            }
        }

        /// <summary>Paso 4a: retícula con sus cuatro estados y anillo de enfriamiento.</summary>
        [MenuItem(MenuRoot + "4. Prefabs de interfaz", priority = 114)]
        public static void CreateUiPrefabs()
        {
            CreateReticlePrefab();
            CreateTouchControlsPrefab();
        }

        /// <summary>Crea (o completa) el prefab de la retícula.</summary>
        public static void CreateReticlePrefab()
        {
            EnsureFolder(UIPrefabs);
            GameObject root = LoadPrefabForEdit(Paths.Reticle, "HUD_Reticle", out bool isNew);
            try
            {
                ConfigureOverlayCanvas(root, 100, false);
                Transform reticle = GetOrCreateUiChild(root.transform, "Reticle");
                var reticleRect = (RectTransform)reticle;
                reticleRect.anchorMin = reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
                reticleRect.sizeDelta = Vector2.zero;
                reticleRect.anchoredPosition = Vector2.zero;

                // Cuatro marcas y un punto central, cada una con contorno oscuro para legibilidad
                // sobre fondos claros (mismo diseño que la retícula del prototipo).
                var marks = new List<Graphic>(5);
                (string name, Vector2 pos, Vector2 size)[] layout =
                {
                    ("Left", new Vector2(-8f, 0f), new Vector2(6f, 2f)),
                    ("Right", new Vector2(8f, 0f), new Vector2(6f, 2f)),
                    ("Bottom", new Vector2(0f, -8f), new Vector2(2f, 6f)),
                    ("Top", new Vector2(0f, 8f), new Vector2(2f, 6f)),
                    ("Center", Vector2.zero, new Vector2(2f, 2f))
                };
                foreach ((string name, Vector2 pos, Vector2 size) in layout)
                {
                    Image outline = GetOrCreateImage(reticle, name + "_Outline", pos, size + Vector2.one * 2f, new Color(0f, 0f, 0f, 0.8f));
                    outline.raycastTarget = false;
                    Image mark = GetOrCreateImage(reticle, name, pos, size, Color.white);
                    mark.raycastTarget = false;
                    marks.Add(mark);
                }

                Image ring = GetOrCreateImage(reticle, "CooldownRing", Vector2.zero, new Vector2(34f, 34f), new Color(1f, 0.72f, 0.2f, 0.8f));
                ring.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                ring.type = Image.Type.Filled;
                ring.fillMethod = Image.FillMethod.Radial360;
                ring.fillOrigin = (int)Image.Origin360.Top;
                ring.fillClockwise = false;
                ring.raycastTarget = false;
                ring.transform.SetAsFirstSibling();

                ReticlePresenter presenter = GetOrAdd<ReticlePresenter>(root);
                var so = new SerializedObject(presenter);
                SerializedProperty marksProp = so.FindProperty("marks");
                marksProp.arraySize = marks.Count;
                for (int i = 0; i < marks.Count; i++) marksProp.GetArrayElementAtIndex(i).objectReferenceValue = marks[i];
                so.FindProperty("scaledRoot").objectReferenceValue = reticleRect;
                so.FindProperty("cooldownRing").objectReferenceValue = ring;
                so.ApplyModifiedPropertiesWithoutUndo();

                SavePrefab(root, Paths.Reticle, isNew);
            }
            finally
            {
                UnloadPrefab(root, isNew);
            }
        }

        /// <summary>Crea (o completa) el prefab de controles táctiles (TDD §7.5).</summary>
        public static void CreateTouchControlsPrefab()
        {
            EnsureFolder(UIPrefabs);
            var settings = AssetDatabase.LoadAssetAtPath<TouchLayoutSettings>(Paths.TouchLayout);
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            GameObject root = LoadPrefabForEdit(Paths.TouchControls, "TouchControls", out bool isNew);
            try
            {
                ConfigureOverlayCanvas(root, 90, true);
                GetOrAdd<CanvasGroup>(root);
                TouchControlsVisibility visibility = GetOrAdd<TouchControlsVisibility>(root);
                SetObject(visibility, "settings", settings);

                Transform safeArea = GetOrCreateUiChild(root.transform, "SafeArea");
                Stretch((RectTransform)safeArea, Vector2.zero, Vector2.one);
                GetOrAdd<SafeAreaFitter>(safeArea.gameObject);

                // Zona de movimiento: mitad izquierda, transparente pero receptora de toques.
                Transform moveZone = GetOrCreateUiChild(safeArea, "MoveZone");
                Stretch((RectTransform)moveZone, Vector2.zero, new Vector2(0.5f, 1f));
                Image moveImage = GetOrAdd<Image>(moveZone.gameObject);
                moveImage.color = new Color(1f, 1f, 1f, 0f);
                Image stickBase = GetOrCreateImage(moveZone, "StickBase", new Vector2(-220f, -280f), new Vector2(220f, 220f), new Color(1f, 1f, 1f, 0.35f));
                stickBase.sprite = knob;
                stickBase.raycastTarget = false;
                Image stickKnob = GetOrCreateImage(stickBase.transform, "StickKnob", Vector2.zero, new Vector2(90f, 90f), new Color(1f, 1f, 1f, 0.8f));
                stickKnob.sprite = knob;
                stickKnob.raycastTarget = false;
                DynamicTouchStick stick = GetOrAdd<DynamicTouchStick>(moveZone.gameObject);
                var stickSo = new SerializedObject(stick);
                stickSo.FindProperty("settings").objectReferenceValue = settings;
                stickSo.FindProperty("stickBase").objectReferenceValue = stickBase.rectTransform;
                stickSo.FindProperty("knob").objectReferenceValue = stickKnob.rectTransform;
                stickSo.FindProperty("restPosition").vector2Value = new Vector2(-220f, -280f);
                stickSo.ApplyModifiedPropertiesWithoutUndo();

                // Zona de mirada: mitad derecha. Va antes que los botones en el orden de hermanos para
                // que los botones (dibujados encima) reciban sus toques primero.
                Transform lookZone = GetOrCreateUiChild(safeArea, "LookZone");
                Stretch((RectTransform)lookZone, new Vector2(0.5f, 0f), Vector2.one);
                Image lookImage = GetOrAdd<Image>(lookZone.gameObject);
                lookImage.color = new Color(1f, 1f, 1f, 0f);
                GetOrAdd<TouchLookZone>(lookZone.gameObject);
                lookZone.SetSiblingIndex(moveZone.GetSiblingIndex() + 1);

                // Pad de herramienta y botones, anclados a la esquina inferior derecha.
                Image toolPad = CreateTouchButton(safeArea, "ToolPad", new Vector2(1f, 0f), new Vector2(-220f, 230f), 190f, "HERRAMIENTA", knob);
                TouchToolPad pad = GetOrAdd<TouchToolPad>(toolPad.gameObject);
                SetObject(pad, "settings", settings);

                ConfigureOnScreenButton(CreateTouchButton(safeArea, "JumpButton", new Vector2(1f, 0f), new Vector2(-430f, 110f), 130f, "SALTAR", knob), "<Gamepad>/buttonSouth");
                ConfigureOnScreenButton(CreateTouchButton(safeArea, "CrouchButton", new Vector2(1f, 0f), new Vector2(-430f, 270f), 110f, "AGACHAR", knob), "<Gamepad>/rightStickPress");
                ConfigureOnScreenButton(CreateTouchButton(safeArea, "SwitchToolButton", new Vector2(1f, 0f), new Vector2(-220f, 420f), 110f, "CAMBIAR", knob), "<Gamepad>/buttonNorth");
                ConfigureOnScreenButton(CreateTouchButton(safeArea, "PauseButton", new Vector2(0f, 1f), new Vector2(90f, -90f), 90f, "PAUSA", knob), "<Gamepad>/start");

                SavePrefab(root, Paths.TouchControls, isNew);
            }
            finally
            {
                UnloadPrefab(root, isNew);
            }
        }

        /// <summary>Paso 5: completa <c>Player.prefab</c> con la arquitectura de la Fase 1.</summary>
        [MenuItem(MenuRoot + "5. Player.prefab", priority = 115)]
        public static void ConfigurePlayerPrefab()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MotorSettings>(Paths.PlayerSettings);
            var vacuumStats = AssetDatabase.LoadAssetAtPath<VacuumStats>(Paths.VacuumStats);
            var grappleStats = AssetDatabase.LoadAssetAtPath<GrappleStats>(Paths.GrappleStats);
            var reticlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Paths.Reticle);
            var touchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Paths.TouchControls);

            GameObject root = PrefabUtility.LoadPrefabContents(Paths.Player);
            try
            {
                RemoveMissingScripts(root);

                Transform cameraParent = FindDeep(root.transform, "CameraParent");
                Camera camera = root.GetComponentInChildren<Camera>(true);
                Transform groundCheck = FindDeep(root.transform, "GroundCheck");
                Transform tool = FindDeep(root.transform, "Tool");

                // Capa Player en toda la jerarquía: la puntería, los lanzamientos y los fragmentos la
                // ignoran, y los volúmenes Trigger solo interactúan con ella (TDD §13).
                SetLayerRecursively(root.transform, Layers.Player);

                PlayerMotor motor = GetOrAdd<PlayerMotor>(root);
                var motorSo = new SerializedObject(motor);
                motorSo.FindProperty("settings").objectReferenceValue = settings;
                motorSo.FindProperty("playerCamera").objectReferenceValue = camera != null ? camera.transform : null;
                motorSo.FindProperty("cameraParent").objectReferenceValue = cameraParent;
                motorSo.FindProperty("groundCheck").objectReferenceValue = groundCheck;
                motorSo.FindProperty("groundMask").intValue = Layers.Masks.SolidGeometry | Layers.Masks.Absorbable;
                motorSo.ApplyModifiedPropertiesWithoutUndo();

                PlayerHealth health = GetOrAdd<PlayerHealth>(root);
                SetObject(health, "settings", settings);
                GetOrAdd<PlayerContactDamage>(root);

                AimService aim = GetOrAdd<AimService>(root);
                SetObject(aim, "aimCamera", camera != null ? camera.transform : null);

                if (tool != null && camera != null && tool.parent != camera.transform)
                    tool.SetParent(camera.transform, true);   // conserva la pose en el mundo

                VacuumTool vacuum = null;
                GrappleTool grapple = null;
                if (tool != null)
                {
                    vacuum = GetOrAdd<VacuumTool>(tool.gameObject);
                    SetObject(vacuum, "stats", vacuumStats);
                    grapple = GetOrAdd<GrappleTool>(tool.gameObject);
                    var grappleSo = new SerializedObject(grapple);
                    grappleSo.FindProperty("stats").objectReferenceValue = grappleStats;
                    grappleSo.FindProperty("motor").objectReferenceValue = motor;
                    grappleSo.ApplyModifiedPropertiesWithoutUndo();
                    // La retroalimentación visual heredada vive aún en Assembly-CSharp, que este
                    // ensamblado de editor no referencia; se agrega por nombre de tipo.
                    AddLegacyComponent(tool.gameObject, "ToolVisualController");
                }

                ToolController tools = GetOrAdd<ToolController>(root);
                var toolsSo = new SerializedObject(tools);
                toolsSo.FindProperty("vacuum").objectReferenceValue = vacuum;
                toolsSo.FindProperty("grapple").objectReferenceValue = grapple;
                toolsSo.FindProperty("aim").objectReferenceValue = aim;
                toolsSo.ApplyModifiedPropertiesWithoutUndo();

                RespawnService respawn = GetOrAdd<RespawnService>(root);
                SetObject(respawn, "tools", tools);

                if (camera != null) GetOrAdd<PlayerCameraFeedback>(camera.gameObject);

                EnsureNestedPrefab(root.transform, reticlePrefab, "HUD_Reticle");
                EnsureNestedPrefab(root.transform, touchPrefab, "TouchControls");

                PrefabUtility.SaveAsPrefabAsset(root, Paths.Player);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Paso 6: aplica en el Tutorial los ajustes que el prototipo hacía por código.</summary>
        [MenuItem(MenuRoot + "6. Escena del Tutorial", priority = 116)]
        public static void ConfigureTutorialScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = SceneManager.GetActiveScene().path;
            Scene scene = EditorSceneManager.OpenScene(SceneNames.Paths.Tutorial, OpenSceneMode.Single);

            BakeCheckpoints();
            BakeStarsAndGuard();
            MakeGiantCratesStatic();
            EnsureKillVolume(scene);
            BakeHudLayout();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (!string.IsNullOrEmpty(previous) && previous != SceneNames.Paths.Tutorial)
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }

        // ================================================================== Tutorial
        /// <summary>Coloca los checkpoints con la tabla verificada y ajusta sus volúmenes.</summary>
        private static void BakeCheckpoints()
        {
            var byName = new Dictionary<string, Checkpoint>();
            foreach (Checkpoint cp in Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                byName[cp.gameObject.name] = cp;

            for (int i = 0; i < TutorialCheckpoints.Length; i++)
            {
                (string name, Vector3 pos, float yaw, Vector3 size, string label) = TutorialCheckpoints[i];
                if (!byName.TryGetValue(name, out Checkpoint cp))
                {
                    Debug.LogWarning($"[Fase 1] No se encontró {name} en el Tutorial.");
                    continue;
                }

                Undo.RecordObject(cp.transform, "Checkpoint");
                cp.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                cp.gameObject.layer = Layers.Trigger;
                if (cp.TryGetComponent(out BoxCollider box))
                {
                    box.isTrigger = true;
                    box.size = size;
                    box.center = Vector3.zero;
                }
                cp.Configure(i, label);
                EditorUtility.SetDirty(cp);
            }
        }

        /// <summary>Reubica las estrellas y el guardia WinRar como hacía el prototipo, e indexa las estrellas.</summary>
        private static void BakeStarsAndGuard()
        {
            GameObject star1 = FindInScene("Star_1");
            GameObject star2 = FindInScene("Star_2");
            if (star1 != null && star1.transform.position.z > 0f && star2 != null)
            {
                star1.transform.position = new Vector3(15f, 1.25f, -50f);
                star2.transform.position = new Vector3(213.42f, 7.25f, -106.87f);
            }

            System.Type starType = FindLegacyType("StarController");
            if (starType != null)
            {
                foreach (Object star in Object.FindObjectsByType(starType, FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    string n = star.name;
                    int underscore = n.LastIndexOf('_');
                    if (underscore < 0 || !int.TryParse(n.Substring(underscore + 1), out int number)) continue;
                    var starSo = new SerializedObject(star);
                    SerializedProperty index = starSo.FindProperty("starIndex");
                    if (index == null) continue;
                    index.intValue = number - 1;
                    starSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            GameObject guard = FindInScene("Enemy_Tutorial_WinRar_Guard");
            if (guard != null && Vector3.Distance(guard.transform.position, new Vector3(243.2f, 1.2f, 61f)) < 11f)
                guard.transform.position = new Vector3(243.2f, 0.8f, 73.2f);
        }

        /// <summary>
        /// Las tres islas gigantes sobre la lava (cajas absorbibles escaladas más de 5×) se vuelven
        /// plataformas estáticas: capa Default, cuerpo cinemático y sin componente absorbible.
        /// </summary>
        private static void MakeGiantCratesStatic()
        {
            foreach (Absorbable absorbable in Object.FindObjectsByType<Absorbable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Vector3 s = absorbable.transform.localScale;
                if (s.x <= 5f && s.z <= 5f) continue;

                GameObject go = absorbable.gameObject;
                go.layer = Layers.Default;
                if (go.TryGetComponent(out Rigidbody body))
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                }
                Undo.DestroyObjectImmediate(absorbable);
                EditorUtility.SetDirty(go);
            }
        }

        /// <summary>
        /// Crea el volumen de vacío bajo toda la escena: su cara superior queda en y = −15 (umbral
        /// del prototipo) y cubre la huella XZ de todos los renderers más 100 m de margen.
        /// </summary>
        private static void EnsureKillVolume(Scene scene)
        {
            const string name = "KillVolume_Void";
            GameObject existing = FindInScene(name);
            GameObject go = existing != null ? existing : new GameObject(name);
            if (existing == null) SceneManager.MoveGameObjectToScene(go, scene);

            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (r.gameObject.scene != scene) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }

            const float thickness = 10f;
            const float margin = 100f;
            go.layer = Layers.Trigger;
            go.transform.SetPositionAndRotation(new Vector3(bounds.center.x, VoidHeight - thickness * 0.5f, bounds.center.z), Quaternion.identity);
            go.transform.localScale = Vector3.one;
            BoxCollider box = GetOrAdd<BoxCollider>(go);
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = new Vector3(bounds.size.x + margin * 2f, thickness, bounds.size.z + margin * 2f);
            GetOrAdd<KillVolume>(go);
        }

        /// <summary>
        /// Aplica en el editor la disposición minimalista del HUD que el prototipo reajustaba por
        /// código en cada arranque (tarjeta de estado arriba a la izquierda, guía de dos líneas
        /// arriba a la derecha, notificación inferior).
        /// </summary>
        private static void BakeHudLayout()
        {
            TutorialHudPresenter hud = Object.FindFirstObjectByType<TutorialHudPresenter>(FindObjectsInactive.Include);
            if (hud == null) return;
            var so = new SerializedObject(hud);
            var healthText = so.FindProperty("healthText").objectReferenceValue as TextMeshProUGUI;
            var toolText = so.FindProperty("toolText").objectReferenceValue as TextMeshProUGUI;
            var scoreText = so.FindProperty("scoreText").objectReferenceValue as TextMeshProUGUI;
            var guidePanel = so.FindProperty("guidePanel").objectReferenceValue as GameObject;
            var zoneTitle = so.FindProperty("zoneTitleText").objectReferenceValue as TextMeshProUGUI;
            var controls = so.FindProperty("controlsText").objectReferenceValue as TextMeshProUGUI;
            var toast = so.FindProperty("checkpointNotificationText").objectReferenceValue as TextMeshProUGUI;

            // Lienzos duplicados: se desactiva el que no contiene la guía referenciada.
            GameObject legacyCanvas = FindInScene("Tutorial_Canvas");
            GameObject mainCanvas = FindInScene("TutorialCanvas");
            if (legacyCanvas != null && mainCanvas != null && legacyCanvas != mainCanvas)
            {
                bool usingMain = guidePanel != null && guidePanel.transform.IsChildOf(mainCanvas.transform);
                (usingMain ? legacyCanvas : mainCanvas).SetActive(false);
            }

            if (healthText != null && healthText.transform.parent is RectTransform card)
            {
                SetRect(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -20f), new Vector2(315f, 74f));
                if (card.TryGetComponent(out Image bg)) bg.color = new Color(0.04f, 0.06f, 0.10f, 0.56f);
                if (card.Find("AccentLine") is RectTransform accent) accent.sizeDelta = new Vector2(0f, 2f);
                SetRect(healthText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(-32f, 26f));
                healthText.fontSize = 18f;
                healthText.alignment = TextAlignmentOptions.Left;
            }

            if (toolText != null)
            {
                SetRect(toolText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(16f, -40f), new Vector2(-32f, 24f));
                toolText.fontSize = 15.5f;
                toolText.alignment = TextAlignmentOptions.Left;
            }

            if (scoreText != null) scoreText.gameObject.SetActive(false);

            if (guidePanel != null)
            {
                if (guidePanel.transform is RectTransform guideRect)
                    SetRect(guideRect, Vector2.one, Vector2.one, Vector2.one, new Vector2(-22f, -20f), new Vector2(450f, 84f));
                if (guidePanel.TryGetComponent(out Image guideBg)) guideBg.color = new Color(0.04f, 0.06f, 0.10f, 0.62f);
                if (guidePanel.transform.Find("AccentLine") is RectTransform accent) accent.sizeDelta = new Vector2(0f, 2f);
                GetOrAdd<CanvasGroup>(guidePanel).alpha = 1f;
            }

            HideReferencedText(so, "phaseBadgeText");
            HideReferencedText(so, "descriptionText");
            HideReferencedText(so, "taskText");

            if (zoneTitle != null)
            {
                SetRect(zoneTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -13f), new Vector2(-36f, 28f));
                zoneTitle.fontSize = 17.5f;
                zoneTitle.alignment = TextAlignmentOptions.Left;
                zoneTitle.textWrappingMode = TextWrappingModes.NoWrap;
                zoneTitle.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (controls != null)
            {
                SetRect(controls.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -44f), new Vector2(-36f, 26f));
                controls.fontSize = 15f;
                controls.alignment = TextAlignmentOptions.Left;
                controls.textWrappingMode = TextWrappingModes.NoWrap;
            }

            if (toast != null)
            {
                if (toast.transform.parent is RectTransform toastRect && toastRect.GetComponent<Image>() != null)
                {
                    SetRect(toastRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(380f, 40f));
                    toastRect.GetComponent<Image>().color = new Color(0.04f, 0.08f, 0.06f, 0.68f);
                }
                toast.fontSize = 15.5f;
            }

            EditorUtility.SetDirty(hud);
        }

        // ================================================================== utilidades
        // Internas (no privadas) para que Phase1VisualBuilder las reutilice sin duplicarlas.
        /// <summary>Crea la carpeta (y sus padres) si no existe.</summary>
        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Carga un ScriptableObject o lo crea con sus valores por defecto.</summary>
        internal static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Asigna una referencia serializada privada.</summary>
        internal static void SetObject(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"[Fase 1] {target.GetType().Name} no tiene el campo «{property}».");
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        /// <summary>Asigna un arreglo serializado de un solo elemento.</summary>
        internal static void SetObjectArray(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            p.arraySize = value != null ? 1 : 0;
            if (value != null) p.GetArrayElementAtIndex(0).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Obtiene un componente o lo agrega.</summary>
        internal static T GetOrAdd<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();
        }

        /// <summary>Carga el contenido de un prefab para editarlo, o crea un objeto nuevo.</summary>
        internal static GameObject LoadPrefabForEdit(string path, string rootName, out bool isNew)
        {
            isNew = AssetDatabase.LoadAssetAtPath<GameObject>(path) == null;
            return isNew ? new GameObject(rootName) : PrefabUtility.LoadPrefabContents(path);
        }

        /// <summary>Guarda el prefab editado.</summary>
        internal static void SavePrefab(GameObject root, string path, bool isNew)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        /// <summary>Libera el contenido cargado o el objeto temporal.</summary>
        internal static void UnloadPrefab(GameObject root, bool isNew)
        {
            if (root == null) return;
            if (isNew) Object.DestroyImmediate(root);
            else PrefabUtility.UnloadPrefabContents(root);
        }

        /// <summary>Busca un hijo directo por nombre o lo crea.</summary>
        internal static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>Busca un hijo de interfaz por nombre o lo crea con <see cref="RectTransform"/>.</summary>
        internal static Transform GetOrCreateUiChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = Layers.UI;
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>Crea (o reutiliza) una imagen centrada con tamaño y color.</summary>
        internal static Image GetOrCreateImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            Transform t = GetOrCreateUiChild(parent, name);
            Image image = GetOrAdd<Image>(t.gameObject);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            image.color = color;
            return image;
        }

        /// <summary>Crea un botón táctil redondo con etiqueta, anclado a una esquina.</summary>
        internal static Image CreateTouchButton(Transform parent, string name, Vector2 anchor, Vector2 position, float size, string label, Sprite sprite)
        {
            Transform t = GetOrCreateUiChild(parent, name);
            var rect = (RectTransform)t;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);
            Image image = GetOrAdd<Image>(t.gameObject);
            image.sprite = sprite;
            image.color = new Color(1f, 1f, 1f, 0.85f);

            Transform labelT = GetOrCreateUiChild(t, "Label");
            Stretch((RectTransform)labelT, Vector2.zero, Vector2.one);
            TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(labelT.gameObject);
            text.text = label;
            text.fontSize = size >= 150f ? 22f : 18f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.05f, 0.07f, 0.12f, 1f);
            text.raycastTarget = false;
            return image;
        }

        /// <summary>Configura un <see cref="OnScreenButton"/> con la ruta del mando virtual.</summary>
        internal static void ConfigureOnScreenButton(Image buttonImage, string controlPath)
        {
            OnScreenButton button = GetOrAdd<OnScreenButton>(buttonImage.gameObject);
            var so = new SerializedObject(button);
            so.FindProperty("m_ControlPath").stringValue = controlPath;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Configura un lienzo superpuesto con escalado de referencia 1920 × 1080.</summary>
        internal static void ConfigureOverlayCanvas(GameObject root, int sortingOrder, bool interactive)
        {
            if (root.GetComponent<RectTransform>() == null) root.AddComponent<RectTransform>();
            root.layer = Layers.UI;
            Canvas canvas = GetOrAdd<Canvas>(root);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = GetOrAdd<CanvasScaler>(root);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (interactive) GetOrAdd<GraphicRaycaster>(root);
        }

        /// <summary>Estira un RectTransform entre dos anclas sin márgenes.</summary>
        internal static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Asigna anclas, pivote, posición y tamaño de un RectTransform.</summary>
        internal static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Oculta el texto referenciado por una propiedad del presentador.</summary>
        internal static void HideReferencedText(SerializedObject so, string property)
        {
            if (so.FindProperty(property).objectReferenceValue is Component c) c.gameObject.SetActive(false);
        }

        /// <summary>Busca un descendiente por nombre (búsqueda en profundidad).</summary>
        internal static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Busca un objeto por nombre en las escenas abiertas, incluidos los inactivos.</summary>
        internal static GameObject FindInScene(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t.gameObject;
            return null;
        }

        /// <summary>Asigna la capa a un objeto y a todos sus descendientes.</summary>
        internal static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursively(t.GetChild(i), layer);
        }

        /// <summary>Elimina los componentes cuyo script ya no existe.</summary>
        internal static void RemoveMissingScripts(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        }

        /// <summary>
        /// Busca un tipo de componente heredado (ensamblado <c>Assembly-CSharp</c>) por su nombre,
        /// con la caché de tipos del editor.
        /// </summary>
        internal static System.Type FindLegacyType(string typeName)
        {
            foreach (System.Type type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
                if (type.Name == typeName && type.Namespace == null) return type;
            return null;
        }

        /// <summary>Agrega (si falta) un componente heredado identificado por nombre de tipo.</summary>
        internal static void AddLegacyComponent(GameObject go, string typeName)
        {
            System.Type type = FindLegacyType(typeName);
            if (type == null)
            {
                Debug.LogWarning($"[Fase 1] No se encontró el componente heredado {typeName}.");
                return;
            }
            if (go.GetComponent(type) == null) go.AddComponent(type);
        }

        /// <summary>Agrega una instancia anidada de un prefab si aún no existe un hijo con ese nombre.</summary>
        internal static void EnsureNestedPrefab(Transform parent, GameObject prefab, string name)
        {
            if (prefab == null || parent.Find(name) != null) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
        }
    }
}

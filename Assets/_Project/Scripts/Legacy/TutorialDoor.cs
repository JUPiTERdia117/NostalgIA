using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Player;
using NostalgIA.Gameplay.Tools;
using System.Collections;
using NostalgIA.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Controlador de Portal Dimensional No-Euclidiano reutilizable como Prefab ("DimensionalPortal").
/// - Permite seleccionar en el Inspector a qué Escena de Unity (.unity) se va a transportar.
/// - Permite colocar y orientar el portal libremente en cualquier posición y rotación (360°).
/// - Carga la escena de destino de forma aditiva en una región espacial aislada para que puedas
///   ver la otra escena en tiempo real a través de la puerta, rodear la puerta sin que el otro mundo
///   esté físicamente detrás, y cruzar de ida y vuelta de manera 100% fluida.
/// </summary>
public class TutorialDoor : MonoBehaviour
{
    [Header("Destino del Portal (Selección de Escena)")]
#if UNITY_EDITOR
    [Tooltip("Selecciona aquí el activo de Escena (.unity) al que transportará este portal.")]
    [SerializeField] private UnityEditor.SceneAsset targetSceneAsset;
#endif
    [Tooltip("Nombre de la escena de destino que se cargará y conectará a través del portal.")]
    public string targetSceneName = "OtherDimension";

    [Tooltip("Identificador del canal del portal para enlazar pares de portales entre escenas.")]
    public string portalChannelId = "MainPortal";

    [Tooltip("Marca esta casilla si este portal es el portal de retorno colocado dentro de la escena de destino.")]
    public bool isReturnPortal = false;

    [Tooltip("Offset espacial donde se ubica la escena de destino cargada aditivamente para no colisionar con la escena actual.")]
    public Vector3 additiveWorldSpatialOffset = new Vector3(-650f, 120f, 650f);

    [Header("Visual Feedback")]
    [SerializeField] private Light doorLight;
    [SerializeField] private Color portalColor = new Color(0.15f, 0.85f, 1f, 1f);
    [SerializeField] private Color frameBodyColor = new Color(0.12f, 0.16f, 0.22f, 1f);

    private NonEuclideanPortal tutorialSidePortal;
    private NonEuclideanPortal otherDimensionPortal;
    private GameObject otherDimensionRoot;
    private Transform detachedAnchorTransform;
    private bool portalConstructed = false;

    public NonEuclideanPortal PortalComponent => tutorialSidePortal;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (targetSceneAsset != null && targetSceneName != targetSceneAsset.name)
        {
            targetSceneName = targetSceneAsset.name;
        }
    }
#endif

    private void Start()
    {
        SetupDimensionalPortalSystem();
    }

    private void LateUpdate()
    {
        // Si el objeto contenedor tenía una escala deformada (como el Door original 4x5x0.3) y usa un ancla 1:1 separada,
        // sincronizar siempre su posición y rotación libre con este Transform.
        if (detachedAnchorTransform != null && detachedAnchorTransform != transform)
        {
            detachedAnchorTransform.SetPositionAndRotation(transform.position, transform.rotation);
        }
    }

    public void SetupDimensionalPortalSystem()
    {
        if (portalConstructed) return;
        portalConstructed = true;

#if UNITY_EDITOR
        if (targetSceneAsset != null && string.IsNullOrEmpty(targetSceneName))
        {
            targetSceneName = targetSceneAsset.name;
        }
#endif

        // 1. Ocultar el cubo sólido base (si existe en el objeto raíz) para dejar libre la ventana dimensional
        Renderer selfRend = GetComponent<Renderer>();
        if (selfRend != null) selfRend.enabled = false;

        Collider selfCol = GetComponent<Collider>();
        if (selfCol != null)
        {
            selfCol.isTrigger = true;
            if (selfCol is BoxCollider bc)
            {
                bc.size = new Vector3(3.6f, 4.8f, 2.4f);
                bc.center = Vector3.zero;
            }
        }

        // 2. Determinar si este objeto ya tiene escala uniforme 1:1 (como en el Prefab DimensionalPortal)
        //    o si tenía escala deformada heredada (como el Door 4x5x0.3), respetando SIEMPRE su posición y rotación libre.
        bool hasUnitScale = (transform.lossyScale - Vector3.one).sqrMagnitude < 0.02f;
        GameObject localPortalObj;

        if (hasUnitScale)
        {
            localPortalObj = gameObject;
            detachedAnchorTransform = null;
        }
        else
        {
            string anchorName = $"DimensionalPortal_Anchor_{name}";
            Transform existingAnchor = transform.parent != null ? transform.parent.Find(anchorName) : null;
            localPortalObj = existingAnchor != null ? existingAnchor.gameObject : new GameObject(anchorName);
            if (transform.parent != null) localPortalObj.transform.SetParent(transform.parent, true);
            localPortalObj.transform.SetPositionAndRotation(transform.position, transform.rotation);
            localPortalObj.transform.localScale = Vector3.one;
            detachedAnchorTransform = localPortalObj.transform;
        }

        BuildOrRefreshDoorwayFrameAndSurface(localPortalObj.transform, frameBodyColor, portalColor, out MeshRenderer localSurface);

        tutorialSidePortal = localPortalObj.GetComponent<NonEuclideanPortal>();
        if (tutorialSidePortal == null) tutorialSidePortal = localPortalObj.AddComponent<NonEuclideanPortal>();
        tutorialSidePortal.viewSurface = localSurface;
        tutorialSidePortal.portalFrameColor = portalColor;
        tutorialSidePortal.InitializePortalSurface();

        PortalBridgeNotifier notifier = localPortalObj.GetComponent<PortalBridgeNotifier>();
        if (notifier == null) notifier = localPortalObj.AddComponent<PortalBridgeNotifier>();
        notifier.ownerDoor = this;
        notifier.isReturnPortal = isReturnPortal;

        // Si este portal es el portal de retorno dentro de la escena de destino, el portal de entrada lo vinculará al cargar la escena
        if (isReturnPortal)
        {
            return;
        }

        // 3. Cargar la Escena de destino seleccionada y vincular ambos portales bidireccionalmente
        StartCoroutine(LoadAndLinkTargetSceneRoutine());
    }

    private IEnumerator LoadAndLinkTargetSceneRoutine()
    {
        Scene destinationScene = default;
        bool sceneLoadedSuccessfully = false;

        if (!string.IsNullOrEmpty(targetSceneName) && targetSceneName != gameObject.scene.name)
        {
            Scene existingScene = SceneManager.GetSceneByName(targetSceneName);
            if (existingScene.isLoaded)
            {
                destinationScene = existingScene;
                sceneLoadedSuccessfully = true;
            }
            else
            {
                AsyncOperation loadOp = null;

                if (Application.CanStreamedLevelBeLoaded(targetSceneName))
                {
                    loadOp = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
                }
#if UNITY_EDITOR
                else if (targetSceneAsset != null)
                {
                    string scenePath = UnityEditor.AssetDatabase.GetAssetPath(targetSceneAsset);
                    if (!string.IsNullOrEmpty(scenePath))
                    {
                        loadOp = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                            scenePath,
                            new LoadSceneParameters(LoadSceneMode.Additive));
                    }
                }
                else
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets($"{targetSceneName} t:Scene");
                    if (guids != null && guids.Length > 0)
                    {
                        string scenePath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        loadOp = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                            scenePath,
                            new LoadSceneParameters(LoadSceneMode.Additive));
                    }
                }
#endif

                if (loadOp != null)
                {
                    while (!loadOp.isDone)
                    {
                        yield return null;
                    }
                    destinationScene = SceneManager.GetSceneByName(targetSceneName);
                    if (!destinationScene.IsValid() || !destinationScene.isLoaded)
                    {
                        // Si se cargó por ruta, tomar la última escena cargada
                        destinationScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
                    }
                    sceneLoadedSuccessfully = destinationScene.IsValid() && destinationScene.isLoaded;
                }
            }
        }

        if (sceneLoadedSuccessfully)
        {
            SetupLoadedSceneAsLinkedDimension(destinationScene, out otherDimensionRoot, out otherDimensionPortal);
        }
        else
        {
            // Fallback por si el usuario escribió un nombre de escena que aún no existe en el proyecto
            BuildOtherDimensionWorld(out otherDimensionRoot, out otherDimensionPortal);
        }

        if (tutorialSidePortal != null && otherDimensionPortal != null)
        {
            tutorialSidePortal.linkedPortal = otherDimensionPortal;
            otherDimensionPortal.linkedPortal = tutorialSidePortal;
            otherDimensionPortal.myWorldRoot = otherDimensionRoot;

            tutorialSidePortal.InitializePortalSurface();
            otherDimensionPortal.InitializePortalSurface();

            PortalBridgeNotifier returnNotifier = otherDimensionPortal.gameObject.GetComponent<PortalBridgeNotifier>();
            if (returnNotifier == null) returnNotifier = otherDimensionPortal.gameObject.AddComponent<PortalBridgeNotifier>();
            returnNotifier.ownerDoor = this;
            returnNotifier.isReturnPortal = true;
        }
    }

    /// <summary>
    /// Empaqueta los objetos raíz de la escena cargada aditivamente dentro de un contenedor desplazado
    /// para que ambos mundos coexistan sin colisionar y puedan verse a través del portal en tiempo real.
    /// </summary>
    private void SetupLoadedSceneAsLinkedDimension(Scene loadedScene, out GameObject worldRoot, out NonEuclideanPortal returnPortal)
    {
        string containerName = $"--- SCENE_DIMENSION_{loadedScene.name} ---";
        GameObject[] roots = loadedScene.GetRootGameObjects();

        worldRoot = null;
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == containerName)
            {
                worldRoot = roots[i];
                break;
            }
        }

        if (worldRoot == null)
        {
            worldRoot = new GameObject(containerName);
            SceneManager.MoveGameObjectToScene(worldRoot, loadedScene);
            worldRoot.transform.position = additiveWorldSpatialOffset;
            worldRoot.transform.rotation = Quaternion.identity;

            for (int i = 0; i < roots.Length; i++)
            {
                GameObject rootObj = roots[i];
                if (rootObj == null || rootObj == worldRoot) continue;

                // Desactivar cámaras principales, AudioListeners, luces direccionales o jugadores duplicados en la escena secundaria
                SanitizeAdditiveSceneObject(rootObj);

                rootObj.transform.SetParent(worldRoot.transform, false);
            }
        }

        // Si la escena cargada es OtherDimension, asegurar que sus materiales y elementos interactivos estén listos
        if (loadedScene.name == "OtherDimension")
        {
            EnsureNexusArchitectureInSceneRoot(worldRoot.transform);
        }

        // Buscar un TutorialDoor o NonEuclideanPortal de retorno dentro de la escena cargada
        returnPortal = null;
        TutorialDoor[] doorsInScene = worldRoot.GetComponentsInChildren<TutorialDoor>(true);
        for (int i = 0; i < doorsInScene.Length; i++)
        {
            TutorialDoor candidate = doorsInScene[i];
            if (candidate == this) continue;
            candidate.isReturnPortal = true;
            candidate.SetupDimensionalPortalSystem();
            if (candidate.PortalComponent != null)
            {
                returnPortal = candidate.PortalComponent;
                if (candidate.portalChannelId == portalChannelId) break;
            }
        }

        if (returnPortal == null)
        {
            NonEuclideanPortal[] portalsInScene = worldRoot.GetComponentsInChildren<NonEuclideanPortal>(true);
            for (int i = 0; i < portalsInScene.Length; i++)
            {
                if (portalsInScene[i] != tutorialSidePortal)
                {
                    returnPortal = portalsInScene[i];
                    break;
                }
            }
        }

        // Si la escena seleccionada por el usuario no tenía un portal de regreso colocado, crear uno automáticamente en su punto de entrada
        if (returnPortal == null)
        {
            GameObject returnPortalObj = new GameObject("DimensionalDoor_ReturnAnchor");
            returnPortalObj.transform.SetParent(worldRoot.transform, false);
            returnPortalObj.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            returnPortalObj.transform.localRotation = Quaternion.identity;

            Color returnGlow = new Color(0.2f, 1.0f, 0.55f, 1f);
            BuildOrRefreshDoorwayFrameAndSurface(returnPortalObj.transform, new Color(0.14f, 0.16f, 0.24f), returnGlow, out MeshRenderer returnSurf);

            returnPortal = returnPortalObj.AddComponent<NonEuclideanPortal>();
            returnPortal.viewSurface = returnSurf;
            returnPortal.portalFrameColor = returnGlow;
        }
    }

    private void SanitizeAdditiveSceneObject(GameObject rootObj)
    {
        if (rootObj.CompareTag("Player") || rootObj.GetComponentInChildren<PlayerHealth>(true) != null)
        {
            rootObj.SetActive(false);
            return;
        }

        AudioListener[] listeners = rootObj.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            listeners[i].enabled = false;
        }

        EventSystem[] eventSystems = rootObj.GetComponentsInChildren<EventSystem>(true);
        for (int i = 0; i < eventSystems.Length; i++)
        {
            eventSystems[i].gameObject.SetActive(false);
        }

        Camera[] cameras = rootObj.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i].name != "PortalViewCamera")
            {
                cameras[i].enabled = false;
            }
        }

        Light[] lights = rootObj.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional)
            {
                lights[i].enabled = false;
            }
        }
    }

    public void BuildOrRefreshDoorwayFrameAndSurface(Transform portalRoot, Color frameColor, Color rimGlowColor, out MeshRenderer surfaceRenderer)
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material frameMat = new Material(litShader);
        frameMat.color = frameColor;
        if (frameMat.HasProperty("_BaseColor")) frameMat.SetColor("_BaseColor", frameColor);
        if (frameMat.HasProperty("_Metallic")) frameMat.SetFloat("_Metallic", 0.75f);
        if (frameMat.HasProperty("_Smoothness")) frameMat.SetFloat("_Smoothness", 0.7f);

        Material neonTrimMat = new Material(litShader);
        neonTrimMat.color = rimGlowColor;
        if (neonTrimMat.HasProperty("_BaseColor")) neonTrimMat.SetColor("_BaseColor", rimGlowColor);
        if (neonTrimMat.HasProperty("_EmissionColor"))
        {
            neonTrimMat.EnableKeyword("_EMISSION");
            neonTrimMat.SetColor("_EmissionColor", rimGlowColor * 2.8f);
        }

        // Si el Prefab ya incluye las piezas hijas, reutilizarlas y aplicarles el material; si no, construirlas
        EnsureFramePiece(portalRoot, "Frame_LeftPost", new Vector3(-1.95f, 0f, 0f), new Vector3(0.42f, 4.8f, 0.45f), frameMat, true);
        EnsureFramePiece(portalRoot, "Frame_RightPost", new Vector3(1.95f, 0f, 0f), new Vector3(0.42f, 4.8f, 0.45f), frameMat, true);
        EnsureFramePiece(portalRoot, "Frame_TopHeader", new Vector3(0f, 2.5f, 0f), new Vector3(4.32f, 0.45f, 0.52f), frameMat, true);
        EnsureFramePiece(portalRoot, "Frame_Threshold", new Vector3(0f, -2.35f, 0f), new Vector3(4.32f, 0.14f, 0.52f), neonTrimMat, false);

        EnsureFramePiece(portalRoot, "Neon_Left", new Vector3(-1.72f, 0f, 0f), new Vector3(0.06f, 4.6f, 0.48f), neonTrimMat, false);
        EnsureFramePiece(portalRoot, "Neon_Right", new Vector3(1.72f, 0f, 0f), new Vector3(0.06f, 4.6f, 0.48f), neonTrimMat, false);
        EnsureFramePiece(portalRoot, "Neon_Top", new Vector3(0f, 2.26f, 0f), new Vector3(3.5f, 0.06f, 0.48f), neonTrimMat, false);

        Transform existingSurf = portalRoot.Find("PortalSurface");
        GameObject surfObj;
        if (existingSurf != null)
        {
            surfObj = existingSurf.gameObject;
        }
        else
        {
            surfObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surfObj.name = "PortalSurface";
            surfObj.transform.SetParent(portalRoot, false);
        }
        surfObj.transform.localPosition = Vector3.zero;
        surfObj.transform.localRotation = Quaternion.identity;
        surfObj.transform.localScale = new Vector3(3.45f, 4.55f, 0.04f);

        Collider surfCol = surfObj.GetComponent<Collider>();
        if (surfCol != null) Destroy(surfCol);

        surfaceRenderer = surfObj.GetComponent<MeshRenderer>();
        surfaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        surfaceRenderer.receiveShadows = false;

        BoxCollider triggerCol = portalRoot.GetComponent<BoxCollider>();
        if (triggerCol == null) triggerCol = portalRoot.gameObject.AddComponent<BoxCollider>();
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector3(3.6f, 4.8f, 2.4f);
        triggerCol.center = Vector3.zero;
    }

    private void EnsureFramePiece(Transform parent, string pieceName, Vector3 localPos, Vector3 localScale, Material mat, bool withCollider)
    {
        Transform existing = parent.Find(pieceName);
        GameObject piece;
        if (existing != null)
        {
            piece = existing.gameObject;
        }
        else
        {
            piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = pieceName;
            piece.transform.SetParent(parent, false);
        }

        piece.transform.localPosition = localPos;
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = localScale;

        Renderer r = piece.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;

        if (!withCollider)
        {
            Collider c = piece.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }
    }

    private void EnsureNexusArchitectureInSceneRoot(Transform rootTransform)
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Material floorMat = new Material(litShader);
        floorMat.color = new Color(0.08f, 0.55f, 0.22f);
        if (floorMat.HasProperty("_BaseColor")) floorMat.SetColor("_BaseColor", new Color(0.08f, 0.55f, 0.22f));
        if (floorMat.HasProperty("_Smoothness")) floorMat.SetFloat("_Smoothness", 0.5f);

        Material wallMat = new Material(litShader);
        wallMat.color = new Color(0.62f, 0.65f, 0.72f);
        if (wallMat.HasProperty("_BaseColor")) wallMat.SetColor("_BaseColor", new Color(0.62f, 0.65f, 0.72f));

        Material pillarMat = new Material(litShader);
        pillarMat.color = new Color(0.45f, 0.50f, 0.78f);
        if (pillarMat.HasProperty("_BaseColor")) pillarMat.SetColor("_BaseColor", new Color(0.45f, 0.50f, 0.78f));
        if (pillarMat.HasProperty("_Smoothness")) pillarMat.SetFloat("_Smoothness", 0.65f);

        Shader blueCodeShader = Shader.Find("Tutorial/BlueCodeLava") ?? Resources.Load<Shader>("BlueCodeLava");
        Material codePoolMat = blueCodeShader != null ? new Material(blueCodeShader) : pillarMat;

        EnsureOrCreateWorldBlock(rootTransform, "Nexus_MainFloor", new Vector3(0f, -0.5f, 22f), new Vector3(46f, 1f, 75f), floorMat);
        EnsureOrCreateWorldBlock(rootTransform, "Nexus_CeilingCodePanel", new Vector3(0f, 13.8f, 24f), new Vector3(12f, 0.25f, 38f), codePoolMat);
        EnsureOrCreateWorldBlock(rootTransform, "Nexus_LeftCodeChannel", new Vector3(-14f, -0.38f, 24f), new Vector3(6f, 0.8f, 44f), codePoolMat);
        EnsureOrCreateWorldBlock(rootTransform, "Nexus_RightCodeChannel", new Vector3(14f, -0.38f, 24f), new Vector3(6f, 0.8f, 44f), codePoolMat);

        float[] zOffsets = { 8f, 20f, 32f, 44f };
        for (int i = 0; i < zOffsets.Length; i++)
        {
            CreatePillarWithCap(rootTransform, $"Pillar_L_{i}", new Vector3(-7.5f, 0f, zOffsets[i]), pillarMat);
            CreatePillarWithCap(rootTransform, $"Pillar_R_{i}", new Vector3(7.5f, 0f, zOffsets[i]), pillarMat);
        }

        EnsureOrCreateWorldBlock(rootTransform, "Nexus_BackWall", new Vector3(0f, 7f, 58f), new Vector3(46f, 15f, 1.5f), wallMat);
        EnsureOrCreateWorldBlock(rootTransform, "Nexus_FrontWall_BehindDoor", new Vector3(0f, 7f, -15f), new Vector3(46f, 15f, 1.5f), wallMat);
        EnsureOrCreateWorldBlock(rootTransform, "Nexus_LeftWall", new Vector3(-22.5f, 7f, 22f), new Vector3(1.5f, 15f, 75f), wallMat);
        EnsureOrCreateWorldBlock(rootTransform, "Nexus_RightWall", new Vector3(22.5f, 7f, 22f), new Vector3(1.5f, 15f, 75f), wallMat);
        EnsureOrCreateWorldBlock(rootTransform, "Nexus_Roof", new Vector3(0f, 14.5f, 22f), new Vector3(46f, 1f, 75f), wallMat);

        Material crateMat = new Material(litShader);
        crateMat.color = new Color(1.0f, 0.65f, 0.1f);
        if (crateMat.HasProperty("_BaseColor")) crateMat.SetColor("_BaseColor", new Color(1.0f, 0.65f, 0.1f));

        // Capa Absorbable (antes «Poolable») tomada de las constantes del núcleo.
        int poolLayer = Layers.Absorbable;

        Vector3[] nexusCrates =
        {
            new Vector3(-4f, 0.6f, 16f),
            new Vector3(4f, 0.6f, 16f),
            new Vector3(-4f, 0.6f, 28f),
            new Vector3(4f, 0.6f, 28f)
        };
        for (int i = 0; i < nexusCrates.Length; i++)
        {
            string crateName = $"Nexus_PoolableCrate_{i + 1}";
            if (rootTransform.Find(crateName) == null)
            {
                GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = crateName;
                c.transform.SetParent(rootTransform, false);
                c.transform.localPosition = nexusCrates[i];
                c.transform.localScale = Vector3.one * 0.85f;
                c.layer = poolLayer;
                c.GetComponent<Renderer>().sharedMaterial = crateMat;
                Rigidbody rb = c.AddComponent<Rigidbody>();
                rb.mass = 1.5f;
                c.AddComponent<Absorbable>();
            }
        }

        if (rootTransform.Find("NexusLight_1") == null)
        {
            CreateDimensionLight(rootTransform, "NexusLight_1", new Vector3(0f, 10f, 12f), new Color(0.75f, 0.9f, 1f), 45f, 3.2f);
        }
        if (rootTransform.Find("NexusLight_2") == null)
        {
            CreateDimensionLight(rootTransform, "NexusLight_2", new Vector3(0f, 10f, 38f), new Color(0.45f, 0.8f, 1f), 45f, 3.2f);
        }
    }

    private void EnsureOrCreateWorldBlock(Transform parent, string blockName, Vector3 localPos, Vector3 localScale, Material mat)
    {
        Transform existing = parent.Find(blockName);
        if (existing != null)
        {
            Renderer r = existing.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
        }
        else
        {
            CreateWorldBlock(parent, blockName, localPos, localScale, mat);
        }
    }

    /// <summary>
    /// Construye la dimensión de respaldo si la escena indicada no pudo cargarse.
    /// </summary>
    private void BuildOtherDimensionWorld(out GameObject worldRoot, out NonEuclideanPortal returnPortal)
    {
        GameObject existingWorld = GameObject.Find("--- OTHER_DIMENSION_WORLD ---");
        if (existingWorld != null) Destroy(existingWorld);

        worldRoot = new GameObject("--- OTHER_DIMENSION_WORLD ---");
        worldRoot.transform.position = additiveWorldSpatialOffset;

        EnsureNexusArchitectureInSceneRoot(worldRoot.transform);

        GameObject returnPortalObj = new GameObject("DimensionalDoor_OtherWorldAnchor");
        returnPortalObj.transform.SetParent(worldRoot.transform, false);
        returnPortalObj.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        returnPortalObj.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        Color returnGlow = new Color(0.2f, 1.0f, 0.55f, 1f);
        BuildOrRefreshDoorwayFrameAndSurface(returnPortalObj.transform, new Color(0.14f, 0.16f, 0.24f), returnGlow, out MeshRenderer returnSurf);

        returnPortal = returnPortalObj.AddComponent<NonEuclideanPortal>();
        returnPortal.viewSurface = returnSurf;
        returnPortal.portalFrameColor = returnGlow;
    }

    private void CreatePillarWithCap(Transform parent, string baseName, Vector3 basePos, Material mat)
    {
        EnsureOrCreateWorldBlock(parent, baseName + "_Base", basePos + new Vector3(0f, 0.25f, 0f), new Vector3(2.4f, 0.5f, 2.4f), mat);
        EnsureOrCreateWorldBlock(parent, baseName + "_Shaft", basePos + new Vector3(0f, 6.5f, 0f), new Vector3(1.3f, 12.0f, 1.3f), mat);
        EnsureOrCreateWorldBlock(parent, baseName + "_Capital", basePos + new Vector3(0f, 12.8f, 0f), new Vector3(2.8f, 0.8f, 2.8f), mat);
    }

    private GameObject CreateWorldBlock(Transform parent, string blockName, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = blockName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
        return go;
    }

    private void CreateDimensionLight(Transform parent, string lightName, Vector3 localPos, Color col, float range, float intensity)
    {
        GameObject lObj = new GameObject(lightName);
        lObj.transform.SetParent(parent, false);
        lObj.transform.localPosition = localPos;
        Light l = lObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = col;
        l.range = range;
        l.intensity = intensity;
    }

    /// <summary>
    /// Notifica el cruce del portal mediante el bus de eventos (Fase 1): el HUD del Tutorial
    /// completa el último paso al entrar a la dimensión enlazada y avisa al regresar.
    /// </summary>
    public void OnPlayerCrossedDimensionalPortal(NonEuclideanPortal destinationPortal)
    {
        bool enteredNewDimension = destinationPortal == otherDimensionPortal;
        EventBus<DimensionalPortalCrossed>.Raise(new DimensionalPortalCrossed(enteredNewDimension));
    }
}

/// <summary>
/// Puente auxiliar para notificar a TutorialDoor cuando su NonEuclideanPortal teletransporta al jugador.
/// </summary>
public class PortalBridgeNotifier : MonoBehaviour
{
    public TutorialDoor ownerDoor;
    public bool isReturnPortal = false;
}

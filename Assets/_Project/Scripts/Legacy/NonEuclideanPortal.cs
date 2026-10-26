using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Sistema de Portal No-Euclidiano ("Puerta a otra dimensión").
/// - Permite colocar y orientar el portal libremente en cualquier posición y rotación.
/// - Permite rodear la puerta en 360° sin que el otro mundo exista físicamente detrás.
/// - Muestra la escena/dimensión vinculada en tiempo real a través del umbral con perspectiva exacta y recorte oblicuo.
/// - Teletransporta al jugador (y objetos físicos lanzados) de forma continua y bidireccional.
/// </summary>
public class NonEuclideanPortal : MonoBehaviour
{
    [Header("Vinculación Dimensional")]
    [Tooltip("Portal gemelo en la escena/dimensión de destino. Al cruzar este portal sales por el gemelo y viceversa.")]
    public NonEuclideanPortal linkedPortal;

    [Tooltip("Contenedor raíz del mundo/escena al que pertenece ESTE portal (para culling inteligente de mundos grandes).")]
    public GameObject myWorldRoot;

    [Header("Superficie Visual del Portal")]
    [Tooltip("Renderer de la malla de la ventana del portal.")]
    public MeshRenderer viewSurface;
    public Color portalFrameColor = new Color(0.15f, 0.85f, 1.0f, 1.0f);

    [Header("Rendimiento y Activación")]
    [Tooltip("Distancia máxima a la que este portal renderiza la vista de la otra dimensión.")]
    public float maxRenderDistance = 120f;
    [Range(0.25f, 1f)]
    public float renderTextureResolutionScale = 0.85f;

    // Cámara para renderizar la vista del portal vinculado
    private Camera portalCamera;
    private RenderTexture viewTexture;
    private Material portalMaterial;
    private Camera mainCamera;

    // Control de cruce fluido del jugador (dot-product de qué lado del plano de la puerta está)
    private Transform trackedPlayer;
    private CharacterController trackedCC;
    private EasyPeasyFirstPersonController.FirstPersonController trackedFPC;
    private bool playerInTrigger = false;
    private float previousSideDot = 0f;
    private float lastTeleportTime = -99f;

    private static bool isRenderingPortal = false;
    private static readonly List<NonEuclideanPortal> allPortals = new List<NonEuclideanPortal>();

    private void OnEnable()
    {
        if (!allPortals.Contains(this)) allPortals.Add(this);
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        allPortals.Remove(this);
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        ReleaseRenderTexture();
    }

    private void Start()
    {
        InitializePortalSurface();
        FindPlayerAndCamera();
    }

    public void InitializePortalSurface()
    {
        if (viewSurface == null)
        {
            Transform childSurf = transform.Find("PortalSurface");
            if (childSurf != null) viewSurface = childSurf.GetComponent<MeshRenderer>();
        }

        if (viewSurface != null)
        {
            Shader portalShader = Shader.Find("Tutorial/DimensionalPortal") ?? Resources.Load<Shader>("DimensionalPortal");
            if (portalShader != null)
            {
                portalMaterial = new Material(portalShader);
                portalMaterial.SetColor("_FrameColor", portalFrameColor);
                viewSurface.sharedMaterial = portalMaterial;
            }
        }

        if (portalCamera == null)
        {
            Transform camChild = transform.Find("PortalViewCamera");
            GameObject camObj = camChild != null ? camChild.gameObject : new GameObject("PortalViewCamera");
            camObj.transform.SetParent(transform, false);
            portalCamera = camObj.GetComponent<Camera>();
            if (portalCamera == null) portalCamera = camObj.AddComponent<Camera>();
            portalCamera.enabled = false;
            portalCamera.nearClipPlane = 0.05f;
            portalCamera.farClipPlane = 500f;

            var addData = camObj.GetComponent<UniversalAdditionalCameraData>();
            if (addData == null) addData = camObj.AddComponent<UniversalAdditionalCameraData>();
            addData.renderShadows = false;
            addData.renderPostProcessing = false;
        }
    }

    private void FindPlayerAndCamera()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (trackedPlayer == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null)
            {
                trackedPlayer = pObj.transform;
                trackedCC = pObj.GetComponent<CharacterController>();
                trackedFPC = pObj.GetComponent<EasyPeasyFirstPersonController.FirstPersonController>();
            }
        }
    }

    private void Update()
    {
        if (trackedPlayer == null || mainCamera == null)
        {
            FindPlayerAndCamera();
            if (trackedPlayer == null) return;
        }

        CheckContinuousPortalTraversal();
    }

    /// <summary>
    /// Comprueba si el jugador cruzó el plano geométrico de la puerta desde cualquier lado (frente o reverso)
    /// respetando la posición y orientación libre del portal.
    /// </summary>
    private void CheckContinuousPortalTraversal()
    {
        if (linkedPortal == null) return;
        if (Time.unscaledTime - lastTeleportTime < 0.18f) return;

        Vector3 playerCheckPos = mainCamera != null ? mainCamera.transform.position : (trackedPlayer.position + Vector3.up * 1.2f);
        Vector3 localOffset = transform.InverseTransformPoint(playerCheckPos);

        // Dimensiones del marco libre de la puerta en espacio local del portal
        bool insideDoorFrame = Mathf.Abs(localOffset.x) <= 2.15f &&
                               Mathf.Abs(localOffset.y) <= 2.85f &&
                               Mathf.Abs(localOffset.z) <= 1.9f;

        Vector3 portalToPlayer = playerCheckPos - transform.position;
        float currentSideDot = Vector3.Dot(transform.forward, portalToPlayer);

        if (insideDoorFrame || playerInTrigger)
        {
            if (previousSideDot != 0f && (currentSideDot * previousSideDot) < 0f &&
                Mathf.Abs(localOffset.x) <= 2.2f && Mathf.Abs(localOffset.y) <= 2.95f)
            {
                TeleportPlayerThroughPortal();
                return;
            }
            previousSideDot = currentSideDot;
        }
        else
        {
            previousSideDot = 0f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
        {
            playerInTrigger = true;
            Vector3 checkPos = mainCamera != null ? mainCamera.transform.position : other.transform.position;
            previousSideDot = Vector3.Dot(transform.forward, checkPos - transform.position);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
        {
            playerInTrigger = false;
            previousSideDot = 0f;
        }
    }

    private void TeleportPlayerThroughPortal()
    {
        if (linkedPortal == null || trackedPlayer == null) return;

        // Asegurar que la escena/dimensión de destino esté activa antes de mover al jugador
        if (linkedPortal.myWorldRoot != null && !linkedPortal.myWorldRoot.activeSelf)
        {
            linkedPortal.myWorldRoot.SetActive(true);
        }

        lastTeleportTime = Time.unscaledTime;
        linkedPortal.lastTeleportTime = Time.unscaledTime;
        linkedPortal.previousSideDot = 0f;
        previousSideDot = 0f;

        // Matriz de transformación relativa 3D completa: soporta cualquier posición y rotación en ambos portales
        Matrix4x4 flipMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 180f, 0f), Vector3.one);
        Matrix4x4 portalTransferMatrix = linkedPortal.transform.localToWorldMatrix * flipMatrix * transform.worldToLocalMatrix;

        // 1. Calcular nueva posición y orientación horizontal del jugador manteniendo el offset exacto respecto al marco
        Vector3 newPlayerPos = portalTransferMatrix.MultiplyPoint3x4(trackedPlayer.position);
        Vector3 newForward = portalTransferMatrix.MultiplyVector(trackedPlayer.forward);
        newForward.y = 0f;
        if (newForward.sqrMagnitude < 0.001f)
        {
            newForward = Vector3.ProjectOnPlane(linkedPortal.transform.forward, Vector3.up);
            if (newForward.sqrMagnitude < 0.001f) newForward = Vector3.forward;
        }
        Quaternion newPlayerRot = Quaternion.LookRotation(newForward.normalized, Vector3.up);

        // Pequeño empuje en la dirección de salida para evitar re-colisionar con el plano en el siguiente frame
        Vector3 moveVec = (trackedFPC != null && trackedFPC.currentVelocity.sqrMagnitude > 0.1f)
            ? trackedFPC.currentVelocity.normalized
            : transform.forward;
        Vector3 exitDir = portalTransferMatrix.MultiplyVector(moveVec);
        exitDir.y = 0f;
        if (exitDir.sqrMagnitude > 0.001f) exitDir.Normalize();
        newPlayerPos += exitDir * 0.35f;

        // 2. Rotar también la velocidad e inercia actual del jugador para que el movimiento sea 100% continuo
        if (trackedFPC != null)
        {
            if (trackedFPC.GrapleControllerSafe != null && trackedFPC.GrapleControllerSafe.IsGrappling)
            {
                trackedFPC.GrapleControllerSafe.Release();
            }
            trackedFPC.currentVelocity = portalTransferMatrix.MultiplyVector(trackedFPC.currentVelocity);
        }

        if (trackedCC != null) trackedCC.enabled = false;
        trackedPlayer.SetPositionAndRotation(newPlayerPos, newPlayerRot);
        if (trackedCC != null) trackedCC.enabled = true;

        Physics.SyncTransforms();

        // Notificar a TutorialDoor / PortalBridgeNotifier si existe en este portal
        PortalBridgeNotifier bridge = GetComponent<PortalBridgeNotifier>();
        if (bridge != null && bridge.ownerDoor != null)
        {
            bridge.ownerDoor.OnPlayerCrossedDimensionalPortal(linkedPortal);
        }
        else
        {
            TutorialDoor door = GetComponent<TutorialDoor>() ?? GetComponentInParent<TutorialDoor>();
            if (door != null)
            {
                door.OnPlayerCrossedDimensionalPortal(linkedPortal);
            }
        }
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (isRenderingPortal) return;
        if (cam != mainCamera && cam.cameraType != CameraType.SceneView) return;
        if (linkedPortal == null || viewSurface == null || !viewSurface.isVisible) return;

        float distToCam = Vector3.Distance(cam.transform.position, transform.position);
        if (distToCam > maxRenderDistance) return;

        if (linkedPortal.myWorldRoot != null && !linkedPortal.myWorldRoot.activeSelf)
        {
            linkedPortal.myWorldRoot.SetActive(true);
        }

        EnsureRenderTexture();
        if (viewTexture == null || portalCamera == null) return;

        isRenderingPortal = true;
        try
        {
            RenderLinkedDimension(context, cam);
        }
        finally
        {
            isRenderingPortal = false;
        }
    }

    private void RenderLinkedDimension(ScriptableRenderContext context, Camera viewerCam)
    {
        Matrix4x4 flipMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 180f, 0f), Vector3.one);
        Matrix4x4 localToWorld = linkedPortal.transform.localToWorldMatrix * flipMatrix * transform.worldToLocalMatrix * viewerCam.transform.localToWorldMatrix;

        portalCamera.transform.SetPositionAndRotation(localToWorld.GetColumn(3), localToWorld.rotation);
        portalCamera.fieldOfView = viewerCam.fieldOfView;
        portalCamera.aspect = viewerCam.aspect;
        portalCamera.targetTexture = viewTexture;

        // Plano de recorte oblicuo (Oblique Near-Clip Plane) respetando la orientación libre del portal
        Transform clipPlaneTransform = linkedPortal.transform;
        Vector3 clipNormal = clipPlaneTransform.forward;
        float dot = Vector3.Dot(transform.forward, viewerCam.transform.position - transform.position);
        if (dot < 0f) clipNormal = -clipNormal;

        int side = System.Math.Sign(Vector3.Dot(clipNormal, clipPlaneTransform.position - portalCamera.transform.position));
        Vector3 camSpacePos = portalCamera.worldToCameraMatrix.MultiplyPoint(clipPlaneTransform.position);
        Vector3 camSpaceNormal = portalCamera.worldToCameraMatrix.MultiplyVector(clipNormal) * side;
        float camSpaceDst = -Vector3.Dot(camSpacePos, camSpaceNormal) + 0.05f;

        if (Mathf.Abs(camSpaceDst) > 0.2f)
        {
            Vector4 clipPlaneCameraSpace = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDst);
            portalCamera.projectionMatrix = viewerCam.CalculateObliqueMatrix(clipPlaneCameraSpace);
        }
        else
        {
            portalCamera.projectionMatrix = viewerCam.projectionMatrix;
        }

        bool linkedSurfState = linkedPortal.viewSurface != null && linkedPortal.viewSurface.enabled;
        if (linkedPortal.viewSurface != null) linkedPortal.viewSurface.enabled = false;

        UniversalRenderPipeline.RenderSingleCamera(context, portalCamera);

        if (linkedPortal.viewSurface != null) linkedPortal.viewSurface.enabled = linkedSurfState;

        if (portalMaterial != null)
        {
            portalMaterial.SetTexture("_PortalTex", viewTexture);
        }
    }

    private void EnsureRenderTexture()
    {
        int w = Mathf.Max(256, Mathf.RoundToInt(Screen.width * renderTextureResolutionScale));
        int h = Mathf.Max(256, Mathf.RoundToInt(Screen.height * renderTextureResolutionScale));

        if (viewTexture == null || viewTexture.width != w || viewTexture.height != h)
        {
            ReleaseRenderTexture();
            viewTexture = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR);
            viewTexture.name = $"PortalRT_{name}_{w}x{h}";
            viewTexture.Create();
            if (portalMaterial != null)
            {
                portalMaterial.SetTexture("_PortalTex", viewTexture);
            }
        }
    }

    private void ReleaseRenderTexture()
    {
        if (viewTexture != null)
        {
            if (portalCamera != null && portalCamera.targetTexture == viewTexture)
            {
                portalCamera.targetTexture = null;
            }
            viewTexture.Release();
            Destroy(viewTexture);
            viewTexture = null;
        }
    }
}

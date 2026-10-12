using UnityEngine;
using UnityEngine.UI;
using EasyPeasyFirstPersonController;

/// <summary>Una única mira de cámara para las dos herramientas y su retícula.</summary>
public class PlayerAim : MonoBehaviour
{
    private Camera aimCamera;
    private Canvas reticleCanvas;
    private RectTransform reticle;

    public Ray AimRay => aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    public bool IsReady => aimCamera != null && aimCamera.isActiveAndEnabled;

    public static PlayerAim GetOrCreate(FirstPersonController controller)
    {
        if (controller == null || controller.playerCamera == null) return null;
        PlayerAim aim = controller.GetComponent<PlayerAim>();
        if (aim == null) aim = controller.gameObject.AddComponent<PlayerAim>();
        aim.aimCamera = controller.playerCamera.GetComponent<Camera>();
        return aim;
    }

    public bool TryGetHit(float distance, out RaycastHit nearest)
    {
        nearest = default;
        if (!IsReady) return false;
        float closest = float.PositiveInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(AimRay, distance,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.distance >= closest) continue;
            nearest = hit;
            closest = hit.distance;
        }
        return nearest.collider != null;
    }

    public Vector3 GetEndPoint(float distance)
    {
        return TryGetHit(distance, out RaycastHit hit) ? hit.point : AimRay.GetPoint(distance);
    }

    void Start()
    {
        GameObject canvasObject = new GameObject("Retícula de herramientas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        reticleCanvas = canvasObject.GetComponent<Canvas>();
        reticleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        reticleCanvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        reticle = new GameObject("Retícula", typeof(RectTransform)).GetComponent<RectTransform>();
        reticle.SetParent(canvasObject.transform, false);
        reticle.sizeDelta = Vector2.zero;
        AddMark(new Vector2(-8, 0), new Vector2(6, 2));
        AddMark(new Vector2(8, 0), new Vector2(6, 2));
        AddMark(new Vector2(0, -8), new Vector2(2, 6));
        AddMark(new Vector2(0, 8), new Vector2(2, 6));
        AddMark(Vector2.zero, new Vector2(2, 2));
    }

    private void AddMark(Vector2 position, Vector2 size)
    {
        CreateImage("Contorno", position, size + Vector2.one * 2, new Color(0, 0, 0, 0.8f));
        CreateImage("Marca", position, size, Color.white);
    }

    private void CreateImage(string label, Vector2 position, Vector2 size, Color color)
    {
        Image mark = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        mark.transform.SetParent(reticle, false);
        mark.rectTransform.anchoredPosition = position;
        mark.rectTransform.sizeDelta = size;
        mark.color = color;
        mark.raycastTarget = false;
    }

    void LateUpdate()
    {
        if (reticleCanvas == null) return;
        reticleCanvas.enabled = IsReady;
        if (!IsReady) return;
        // También coincide cuando la cámara ocupa solo una parte de la pantalla.
        Vector2 center = aimCamera.rect.center;
        reticle.anchorMin = reticle.anchorMax = center;
        reticle.anchoredPosition = Vector2.zero;
    }

    void OnDisable() { if (reticleCanvas != null) reticleCanvas.enabled = false; }
}

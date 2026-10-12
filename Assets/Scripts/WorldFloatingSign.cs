using UnityEngine;
using TMPro;

/// <summary>
/// Cartel flotante diégetico en el mundo con orientación hacia el jugador y texto instructivo claro.
/// </summary>
public class WorldFloatingSign : MonoBehaviour
{
    [SerializeField] private TextMeshPro textMesh;
    private Transform playerCamera;

    void Start()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null) playerCamera = mainCam.transform;
    }

    void Update()
    {
        if (playerCamera != null)
        {
            Vector3 lookDir = transform.position - playerCamera.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
    }

    public void SetText(string title, string instruction)
    {
        if (textMesh != null)
        {
            textMesh.text = $"<color=#FFE066><b>{title}</b></color>\n<size=75%>{instruction}</size>";
        }
    }
}

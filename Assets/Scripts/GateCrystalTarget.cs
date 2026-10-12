using System.Collections;
using UnityEngine;

/// <summary>
/// Diana de cristal mágico.
/// Al recibir impacto de un proyectil de la aspiradora o contacto, se destruye y abre el paso con efecto sonoro.
/// </summary>
public class GateCrystalTarget : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private Transform gateDoor;
    [SerializeField] private Vector3 openOffset = new Vector3(0, -6f, 0);
    [SerializeField] private float openDuration = 1.2f;

    private bool isOpened = false;

    void OnCollisionEnter(Collision collision)
    {
        CheckHit(collision.gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        CheckHit(other.gameObject);
    }

    private void CheckHit(GameObject hitObj)
    {
        if (isOpened) return;

        bool isPoolable = hitObj.layer == LayerMask.NameToLayer("Poolable");
        Rigidbody rb = hitObj.GetComponent<Rigidbody>();
        bool hasVelocity = rb != null && rb.linearVelocity.magnitude > 1.0f;
        bool isPlayer = hitObj.CompareTag("Player") || hitObj.GetComponentInParent<PlayerController>() != null;

        if (isPoolable || hasVelocity || isPlayer)
        {
            OpenGate();
        }
    }

    [ContextMenu("Open Gate")]
    public void OpenGate()
    {
        if (isOpened) return;
        isOpened = true;

        if (GameAudio.Instance != null)
        {
            GameAudio.Instance.PlayEnemyHit();
            GameAudio.Instance.PlayCheckpoint();
        }

        LevelUIController ui = FindAnyObjectByType<LevelUIController>();
        if (ui != null)
        {
            ui.ShowToast("¡DIANA DE CRISTAL ACTIVADA!");
        }

        Renderer rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = false;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (gateDoor != null)
        {
            StartCoroutine(AnimateGateOpen());
        }
    }

    private IEnumerator AnimateGateOpen()
    {
        Vector3 startPos = gateDoor.position;
        Vector3 targetPos = startPos + openOffset;
        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / openDuration);
            gateDoor.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        gateDoor.position = targetPos;
    }
}

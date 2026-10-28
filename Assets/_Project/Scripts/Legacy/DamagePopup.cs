using UnityEngine;

/// <summary>
/// Texto emergente flotante en el mundo 3D que muestra el daño recibido
/// y se eleva desvaneciéndose hacia arriba mirando siempre a la cámara.
/// </summary>
public class DamagePopup : MonoBehaviour
{
    private TextMesh textMesh;
    private float moveSpeed = 1.8f;
    private Color textColor = Color.yellow;
    private float lifeTime = 0.85f;
    private float currentLife;

    public static DamagePopup Create(Vector3 worldPosition, int damageAmount, bool isCritical = false)
    {
        GameObject obj = new GameObject("DamagePopup");
        obj.transform.position = worldPosition + Vector3.up * 0.4f + Random.insideUnitSphere * 0.15f;

        DamagePopup popup = obj.AddComponent<DamagePopup>();
        popup.Setup(damageAmount, isCritical);
        return popup;
    }

    private void Setup(int damageAmount, bool isCritical)
    {
        textMesh = gameObject.AddComponent<TextMesh>();
        textMesh.text = $"-{damageAmount}";
        textMesh.fontSize = isCritical ? 42 : 32;
        textMesh.characterSize = 0.08f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontStyle = isCritical ? FontStyle.Bold : FontStyle.Bold;

        textColor = isCritical ? new Color(1f, 0.15f, 0.1f) : new Color(1f, 0.85f, 0.15f);
        textMesh.color = textColor;

        // Desactivar sombras del texto para máxima claridad
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        currentLife = lifeTime;
    }

    private void Update()
    {
        // Mirar siempre hacia la cámara del jugador (Billboard)
        if (Camera.main != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }

        // Subir gradualmente
        transform.position += Vector3.up * (moveSpeed * Time.deltaTime);

        // Desvanecer alpha
        currentLife -= Time.deltaTime;
        float alpha = Mathf.Clamp01(currentLife / lifeTime);
        if (textMesh != null)
        {
            Color c = textColor;
            c.a = alpha;
            textMesh.color = c;
        }

        if (currentLife <= 0f)
        {
            Destroy(gameObject);
        }
    }
}

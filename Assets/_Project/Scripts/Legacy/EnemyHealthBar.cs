using UnityEngine;

/// <summary>
/// Barra de vida visual en el espacio del mundo que flota sobre la cabeza del enemigo
/// y mira siempre hacia la cámara del jugador (Billboard).
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    private EnemyHealth health;
    private Transform bgTransform;
    private Transform fillTransform;
    private Renderer fillRenderer;
    private MaterialPropertyBlock fillPropBlock;
    private TextMesh hpTextMesh;

    [SerializeField] private float heightOffset = 1.0f;
    [SerializeField] private Vector2 barSize = new Vector2(0.85f, 0.12f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public static EnemyHealthBar Attach(EnemyHealth enemyHealth, float customHeight = 1.0f)
    {
        GameObject barObj = new GameObject("HealthBar_Billboard");
        barObj.transform.SetParent(enemyHealth.transform, false);
        barObj.transform.localPosition = Vector3.up * customHeight;

        EnemyHealthBar bar = barObj.AddComponent<EnemyHealthBar>();
        bar.health = enemyHealth;
        bar.heightOffset = customHeight;
        bar.Initialize();
        return bar;
    }

    private void Initialize()
    {
        fillPropBlock = new MaterialPropertyBlock();
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");

        // 1. Fondo de la barra (Gris oscuro / Negro)
        GameObject bgObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bgObj.name = "BG";
        // Quito el collider del quad ANTES de colgarlo del enemigo: Destroy espera al final del
        // frame y, mientras tanto, un MeshCollider cóncavo bajo un Rigidbody dinámico (las arañas
        // que suelta el nido) llenaba la consola de errores de física.
        DestroyImmediate(bgObj.GetComponent<Collider>());
        bgObj.transform.SetParent(transform, false);
        bgObj.transform.localPosition = new Vector3(0, 0, 0.005f);
        bgObj.transform.localScale = new Vector3(barSize.x + 0.06f, barSize.y + 0.04f, 1f);

        Renderer bgRend = bgObj.GetComponent<Renderer>();
        bgRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bgRend.receiveShadows = false;
        Material bgMat = new Material(unlitShader);
        bgMat.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.12f, 0.85f));
        bgRend.sharedMaterial = bgMat;

        // 2. Barra de relleno de vida
        GameObject fillObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fillObj.name = "Fill";
        // Igual que con el fondo: sin collider antes de colgarlo del enemigo.
        DestroyImmediate(fillObj.GetComponent<Collider>());
        fillObj.transform.SetParent(transform, false);
        fillObj.transform.localPosition = Vector3.zero;
        fillObj.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);

        fillTransform = fillObj.transform;
        fillRenderer = fillObj.GetComponent<Renderer>();
        fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fillRenderer.receiveShadows = false;
        Material fillMat = new Material(unlitShader);
        fillRenderer.sharedMaterial = fillMat;

        // 3. Texto numérico de salud (ej. 3/3)
        GameObject textObj = new GameObject("HPText");
        textObj.transform.SetParent(transform, false);
        textObj.transform.localPosition = new Vector3(0, barSize.y * 1.3f, -0.01f);
        hpTextMesh = textObj.AddComponent<TextMesh>();
        // Misma fuente que el resto del juego (JikiTourai); el texto 3D usa el material de la fuente.
        Font gameFont = Resources.Load<Font>("Fonts/JikiTourai_ES");
        if (gameFont != null)
        {
            hpTextMesh.font = gameFont;
            hpTextMesh.GetComponent<MeshRenderer>().sharedMaterial = gameFont.material;
        }
        hpTextMesh.fontSize = 24;
        hpTextMesh.characterSize = 0.05f;
        hpTextMesh.anchor = TextAnchor.MiddleCenter;
        hpTextMesh.alignment = TextAlignment.Center;
        hpTextMesh.color = Color.white;
        Renderer textRend = textObj.GetComponent<Renderer>();
        if (textRend != null)
        {
            textRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            textRend.receiveShadows = false;
        }

        UpdateBar();
    }

    private void LateUpdate()
    {
        if (Camera.main != null)
        {
            // Billboard hacia la cámara
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }

    public void UpdateBar()
    {
        if (health == null || fillTransform == null) return;

        float ratio = Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth);

        // Escalar y desplazar el pivote para que se vacíe hacia la izquierda
        fillTransform.localScale = new Vector3(barSize.x * ratio, barSize.y, 1f);
        fillTransform.localPosition = new Vector3(-barSize.x * 0.5f * (1f - ratio), 0, 0);

        // Color según salud: Verde (>50%) -> Amarillo (>25%) -> Rojo (<=25%)
        Color barColor;
        if (ratio > 0.5f)
        {
            barColor = Color.Lerp(new Color(1f, 0.8f, 0.1f), new Color(0.2f, 0.9f, 0.25f), (ratio - 0.5f) * 2f);
        }
        else
        {
            barColor = Color.Lerp(new Color(0.95f, 0.15f, 0.1f), new Color(1f, 0.8f, 0.1f), ratio * 2f);
        }

        fillRenderer.GetPropertyBlock(fillPropBlock);
        fillPropBlock.SetColor(BaseColorId, barColor);
        fillPropBlock.SetColor(ColorId, barColor);
        fillRenderer.SetPropertyBlock(fillPropBlock);

        if (hpTextMesh != null)
        {
            hpTextMesh.text = $"{health.CurrentHealth}/{health.MaxHealth}";
        }
    }
}

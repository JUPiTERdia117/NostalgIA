using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;

/// <summary>Selecciona la herramienta y traduce pulsaciones cortas/largas del botón móvil.
/// El cambio de herramienta SOLO ocurre mediante el botón dedicado de cambiar herramienta ([Q] / Gamepad North).</summary>
public class PlayerController : MonoBehaviour, IDamageable
{
    public enum Tool { Grapple, Vacuum }

    [SerializeField] private ObjectVacum vacuum;
    [SerializeField] private GrapleController graple;
    [SerializeField] private Tool selectedTool = Tool.Vacuum;
    [Tooltip("Segundos de pulsación para empezar a aspirar. Soltar antes lanza un objeto.")]
    [SerializeField, Min(0.05f)] private float vacuumHoldTime = 0.2f;
    [SerializeField] private OnScreenStick toolStick;
    [Tooltip("Tiempo de invulnerabilidad tras recibir daño.")]
    [SerializeField] private float invulnerabilityDuration = 0.5f;

    public Tool SelectedTool => selectedTool;
    public GrapleController GrapleController => graple;
    public ObjectVacum VacuumController => vacuum;
    public bool dead = false;
    public int health = 3;
    private GameController gameController;
    private bool toolHeld;
    private bool mouseVacuumHeld;
    private bool waitForToolRelease;
    private float toolPressedAt;
    private Gamepad toolGamepad;
    private float lastDamageTime = -99f;

    void Awake()
    {
        if (graple == null) graple = GetComponentInChildren<GrapleController>(true);
        if (vacuum == null) vacuum = GetComponentInChildren<ObjectVacum>(true);
        gameController = FindAnyObjectByType<GameController>();
    }

    void OnEnable() { ApplyToolSelection(); }

    void Update()
    {
        if (Time.timeScale <= 0f)
        {
            CancelToolInput();
            return;
        }

        Gamepad gamepad = Gamepad.current;
        bool toolDown = gamepad != null && gamepad.buttonWest.isPressed;
        if (!toolDown) waitForToolRelease = false;

        // ÚNICA forma permitida de cambiar de herramienta: botón indicado ([Q] o botón Norte del mando)
        bool changeTool = (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame);
        if (changeTool)
        {
            ChangeTool();
        }
        else if (!waitForToolRelease)
        {
            if (toolHeld && (toolGamepad == null || !toolGamepad.added || toolGamepad != gamepad))
                CancelToolInput();
            else
            {
                if (gamepad != null && gamepad.buttonWest.wasPressedThisFrame)
                {
                    toolGamepad = gamepad;
                    ToolButtonDown();
                }
                if (toolHeld && !toolDown) ToolButtonUp();
            }
        }

        // Tecla X o Clics solo ejecutan la acción de la herramienta ACTUALMENTE seleccionada,
        // NUNCA cambian de herramienta automáticamente.
        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            if (selectedTool == Tool.Grapple && graple != null && graple.isActiveAndEnabled)
            {
                graple.Graple();
            }
        }

        Mouse mouse = Application.isMobilePlatform ? null : Mouse.current;
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (mouse != null && !overUI)
        {
            if (selectedTool == Tool.Vacuum)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    mouseVacuumHeld = true;
                }
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    if (vacuum != null && vacuum.isActiveAndEnabled) vacuum.LaunchObject();
                }
            }
            else if (selectedTool == Tool.Grapple)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (graple != null && graple.isActiveAndEnabled) graple.Graple();
                }
            }
        }
        if (mouse == null || !mouse.leftButton.isPressed || selectedTool != Tool.Vacuum)
        {
            mouseVacuumHeld = false;
        }

        if (vacuum != null)
            vacuum.vacuumActive = selectedTool == Tool.Vacuum && vacuum.isActiveAndEnabled &&
                (mouseVacuumHeld || (toolHeld && Time.unscaledTime - toolPressedAt >= Mathf.Max(0.05f, vacuumHoldTime)));
    }

    [ContextMenu("Take Damage")]
    public void TakeDamageContextMenu() => TakeDamage(1);

    public void TakeDamage(int damage)
    {
        if (dead) return;
        // Daño letal por lava o caída ignora el cooldown de invulnerabilidad
        if (damage < 90 && Time.time - lastDamageTime < invulnerabilityDuration) return;

        lastDamageTime = Time.time;
        health -= damage;
        Debug.Log($"[PlayerController] Player took {damage} damage! Remaining health: {health}");

        if (gameController != null)
        {
            gameController.OnPlayerTookDamage();
        }

        if (health <= 0)
        {
            Die();
        }
    }
    
    [ContextMenu("Kill Player")]
    public void Die()
    {
        if (dead) return;
        dead = true;
        health = 0;
        Debug.Log("[PlayerController] ¡El jugador ha muerto!");
        if (gameController != null)
        {
            gameController.OnPlayerDied();
        }
        else
        {
            gameController = FindAnyObjectByType<GameController>();
            if (gameController != null) gameController.OnPlayerDied();
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (dead) return;
        if (hit.gameObject.name.Contains("Lava") || hit.gameObject.CompareTag("Lava") ||
            hit.gameObject.GetComponentInParent<LavaHazard>() != null)
        {
            TakeDamage(99);
            return;
        }

        if (hit.gameObject.CompareTag("Enemy") || hit.gameObject.GetComponentInParent<EnemyHealth>() != null)
        {
            TakeDamage(1);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckHazards(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        CheckHazards(other.gameObject);
    }

    private void CheckHazards(GameObject obj)
    {
        if (dead) return;
        if (obj.name.Contains("Lava") || obj.CompareTag("Lava") ||
            obj.GetComponentInParent<LavaHazard>() != null)
        {
            TakeDamage(99);
            return;
        }

        if (obj.CompareTag("Enemy") || obj.GetComponentInParent<EnemyHealth>() != null)
        {
            TakeDamage(1);
        }
    }

    public void ChangeTool()
    {
        SelectTool(selectedTool == Tool.Grapple ? Tool.Vacuum : Tool.Grapple);
    }

    private void SelectTool(Tool tool)
    {
        if (selectedTool == tool) return;
        CancelToolInput();
        if (graple != null && graple.IsGrappling) graple.Release();
        selectedTool = tool;
        ApplyToolSelection();
    }

    private void ApplyToolSelection()
    {
        // Ambas herramientas comparten GameObject; solo cambia el componente y su rayo.
        if (graple != null) graple.enabled = selectedTool == Tool.Grapple;
        if (vacuum != null) vacuum.enabled = selectedTool == Tool.Vacuum;
        if (toolStick != null)
        {
            if (graple != null && graple.enabled)
            {
                toolStick.enabled = false;
            }
            else
            {
                toolStick.enabled = true;
            }
        }
    }

    private void ToolButtonDown()
    {
        if (toolHeld) return;
        toolHeld = true;
        toolPressedAt = Time.unscaledTime;
        if (selectedTool == Tool.Grapple && graple != null && graple.isActiveAndEnabled)
            graple.Graple();
    }

    private void ToolButtonUp()
    {
        if (!toolHeld) return;
        bool tap = Time.unscaledTime - toolPressedAt < Mathf.Max(0.05f, vacuumHoldTime);
        toolHeld = false;
        toolGamepad = null;
        if (vacuum == null) return;
        vacuum.vacuumActive = false;
        if (selectedTool == Tool.Vacuum && tap && vacuum.isActiveAndEnabled)
            vacuum.LaunchObject();
    }

    private void CancelToolInput()
    {
        toolHeld = false;
        toolGamepad = null;
        mouseVacuumHeld = false;
        waitForToolRelease = Gamepad.current != null && Gamepad.current.buttonWest.isPressed;
        if (vacuum != null) vacuum.vacuumActive = false;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) CancelToolInput();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) CancelToolInput();
    }

    void OnDisable()
    {
        CancelToolInput();
        if (graple != null) graple.Release();
    }
}

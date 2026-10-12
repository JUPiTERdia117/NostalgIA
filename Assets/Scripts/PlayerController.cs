using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;

/// <summary>Selecciona la herramienta y traduce pulsaciones cortas/largas del botón móvil.</summary>
public class PlayerController : MonoBehaviour
{
    public enum Tool { Grapple, Vacuum }

    [SerializeField] private ObjectVacum vacuum;
    [SerializeField] private GrapleController graple;
    [SerializeField] private Tool selectedTool = Tool.Grapple;
    [Tooltip("Segundos de pulsación para empezar a aspirar. Soltar antes lanza un objeto.")]
    [SerializeField, Min(0.05f)] private float vacuumHoldTime = 0.2f;
    [SerializeField] private OnScreenStick toolStick;

    public Tool SelectedTool => selectedTool;
    public bool dead = false;
    public int health = 3;
    private GameController gameController;
    private bool toolHeld;
    private bool mouseVacuumHeld;
    private bool waitForToolRelease;
    private float toolPressedAt;
    private Gamepad toolGamepad;

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

        // Evita interpretar un toque sobre la UI como un clic de aspiración.
        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            SelectTool(Tool.Grapple);
            if (graple != null && graple.isActiveAndEnabled) graple.Graple();
        }
        Mouse mouse = Application.isMobilePlatform ? null : Mouse.current;
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (mouse != null && !overUI)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                SelectTool(Tool.Vacuum);
                mouseVacuumHeld = true;
            }
            if (mouse.rightButton.wasPressedThisFrame)
            {
                SelectTool(Tool.Vacuum);
                if (vacuum != null && vacuum.isActiveAndEnabled) vacuum.LaunchObject();
            }
        }
        if (mouse == null || !mouse.leftButton.isPressed) mouseVacuumHeld = false;

        if (vacuum != null)
            vacuum.vacuumActive = selectedTool == Tool.Vacuum && vacuum.isActiveAndEnabled &&
                (mouseVacuumHeld || (toolHeld && Time.unscaledTime - toolPressedAt >= Mathf.Max(0.05f, vacuumHoldTime)));
    }

    [ContextMenu("Take Damage")]
    public void TakeDamage(int damage)
    {
        health -= damage;
        if (health <= 0)
        {
            Die();
        }
    }
    
    [ContextMenu("Kill Player")]
    void Die()
    {
        dead = true;
        health = 0;
        gameController.RespawnPlayer();
    }

    public void ChangeTool()
    {
        SelectTool(selectedTool == Tool.Grapple ? Tool.Vacuum : Tool.Grapple);
        
        
        
    }

    private void SelectTool(Tool tool)
    {
        if (selectedTool == tool) return;
        CancelToolInput();
        selectedTool = tool;
        ApplyToolSelection();
    }

    private void ApplyToolSelection()
    {
        // Ambas herramientas comparten GameObject; solo cambia el componente y su rayo.
        if (graple != null) graple.enabled = selectedTool == Tool.Grapple;
        if (vacuum != null) vacuum.enabled = selectedTool == Tool.Vacuum;
        if(graple.enabled)
        {
            toolStick.enabled = false;
        }
        else
        {
            toolStick.enabled = true;
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

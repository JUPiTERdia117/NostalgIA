using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Conecta el ratón con la aspiradora y la tecla X con el gancho.
/// </summary>
public class PlayerController : MonoBehaviour
{
    
    [Tooltip("Aspiradora controlada con el ratón: clic izquierdo para absorber y pulsación del derecho para lanzar.")]
    [SerializeField] private ObjectVacum vacuum;

    [Tooltip("Gancho: X engancha y una segunda pulsación lo suelta conservando el impulso.")]
    [SerializeField] private GrapleController graple;
    void Awake()
    {
        if (graple == null) graple = GetComponentInChildren<GrapleController>();
        if (vacuum == null) vacuum = GetComponentInChildren<ObjectVacum>();
    }
    /// <summary>
    /// Lee el teclado y el ratón en cada fotograma usando el paquete Input System.
    /// </summary>
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            if (graple != null && graple.isActiveAndEnabled) graple.Graple();
        }

        if (vacuum == null) return;

        // El botón izquierdo mantiene la absorción activa; admite que no haya ratón conectado.
        vacuum.vacuumActive = Mouse.current != null && Mouse.current.leftButton.isPressed;

        // El derecho lanza solo al pulsarlo, sin repetir mientras se mantiene presionado.
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            vacuum.LaunchObject();
        }
    }

    /// <summary>
    /// Detiene la aspiradora si se desactiva el controlador del jugador.
    /// </summary>
    void OnDisable()
    {
        if (graple != null) graple.Release();
        if (vacuum != null)
        {
            vacuum.vacuumActive = false;
        }
    }
}

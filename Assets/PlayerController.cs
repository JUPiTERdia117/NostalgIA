using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Conecta los botones del ratón con las acciones de la aspiradora.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Tooltip("Valor reservado para movimiento. Actualmente no se utiliza; el movimiento lo controla FirstPersonController.")]
    [SerializeField] private float moveSpeed = 5.0f;
    [Tooltip("Aspiradora controlada con el ratón: clic izquierdo para absorber y pulsación del derecho para lanzar.")]
    [SerializeField] private ObjectVacum vacuum;
    /// <summary>
    /// Lee el ratón en cada fotograma usando el paquete Input System.
    /// </summary>
    void Update()
    {
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
        if (vacuum != null)
        {
            vacuum.vacuumActive = false;
        }
    }
}

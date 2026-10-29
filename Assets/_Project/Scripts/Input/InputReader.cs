// -----------------------------------------------------------------------------
// NostalgIA — Entrada (NostalgIA.Input)
// Archivo: InputReader.cs
// Propósito: punto único de lectura de la entrada de juego para teclado y ratón,
// mando y pantalla táctil (TDD §7.2, tarea F1-A03).
// -----------------------------------------------------------------------------

using System;
using NostalgIA.Core.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NostalgIA.Input
{
    /// <summary>
    /// Lector de entrada del jugador: valores continuos, eventos de pulsación, activación de mapas
    /// y reasignaciones persistentes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es un <see cref="ScriptableObject"/> para que su configuración (sensibilidades, curva del
    /// stick) viva en un asset editable y para que cualquier sistema lo pueda referenciar sin
    /// buscar objetos en la escena. <c>GameRoot</c> lo inicializa una vez y lo registra en el
    /// <c>ServiceLocator</c>.
    /// </para>
    /// <para>
    /// Las acciones se buscan por nombre una única vez en <see cref="Initialize"/> y se guardan
    /// las referencias. En juego, leer un valor es una llamada a <c>ReadValue</c> o
    /// <c>IsPressed</c>, sin cadenas ni asignaciones.
    /// </para>
    /// <para>
    /// <b>Mirada combinada.</b> El ángulo de este frame se calcula sumando las fuentes, sin
    /// ramas por dispositivo, de modo que conectar un mando no desactiva el ratón ni viceversa:
    /// <code>
    /// Δθ = Δratón · kRatón  +  (Δtáctil / altoPantalla) · kTáctil  +  ω(stick) · Δt
    /// </code>
    /// donde <c>ω(stick)</c> es la velocidad angular del stick tras su zona muerta, su curva de
    /// respuesta y su aceleración (heredadas del prototipo). El valor se calcula de forma
    /// perezosa la primera vez que se consulta en cada frame y se reutiliza en el resto del frame.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "NostalgIA/Entrada/Input Reader", fileName = "InputReader")]
    public sealed class InputReader : ScriptableObject
    {
        /// <summary>Clave de <c>PlayerPrefs</c> donde se guardan las reasignaciones.</summary>
        public const string BindingOverridesKey = "nostalgia.input.bindingOverrides";

        [Tooltip("Asset de acciones. Si se deja vacío se usan las acciones de todo el proyecto (Project-wide Actions).")]
        [SerializeField] private InputActionAsset actions;

        [Header("Ratón")]
        [Tooltip("Grados de giro por píxel de desplazamiento del ratón (prototipo: 0,05 × 15 = 0,75).")]
        [SerializeField, Min(0.001f)] private float mouseDegreesPerPixel = 0.75f;

        [Header("Táctil")]
        [Tooltip("Grados de giro al arrastrar el dedo una altura completa de pantalla. Normalizar por la altura hace la sensibilidad independiente de la resolución.")]
        [SerializeField, Min(1f)] private float touchDegreesPerScreenHeight = 180f;

        [Header("Stick derecho")]
        [Tooltip("Velocidad angular máxima horizontal, en grados por segundo (prototipo: 2,5 × 15).")]
        [SerializeField, Min(0f)] private float stickMaxSpeedX = 37.5f;
        [Tooltip("Velocidad angular máxima vertical, en grados por segundo (prototipo: 2,5 × 15).")]
        [SerializeField, Min(0f)] private float stickMaxSpeedY = 37.5f;
        [Tooltip("Rampa de aceleración relativa a la velocidad máxima, por segundo. 3 alcanza la velocidad máxima en ≈ 0,33 s.")]
        [SerializeField, Min(0.01f)] private float stickAcceleration = 3f;
        [Tooltip("Aplica la rampa de aceleración del prototipo. Desactivada, la velocidad sigue al stick de inmediato.")]
        [SerializeField] private bool useStickAcceleration = true;
        [Tooltip("Zona muerta por eje del stick.")]
        [SerializeField, Range(0f, 0.95f)] private float stickDeadZone = 0.1f;
        [Tooltip("Exponente de la curva de respuesta: 1 es lineal; valores mayores dan más precisión cerca del centro.")]
        [SerializeField, Range(1f, 3f)] private float stickResponseExponent = 1f;

        [Header("Stick táctil de vista (pad de la aspiradora)")]
        [Tooltip("Velocidad angular máxima horizontal del pad de la aspiradora usado como stick, en grados por segundo.")]
        [SerializeField, Min(0f)] private float touchStickMaxSpeedX = 110f;
        [Tooltip("Velocidad angular máxima vertical del pad de la aspiradora usado como stick, en grados por segundo.")]
        [SerializeField, Min(0f)] private float touchStickMaxSpeedY = 70f;

        [Header("General")]
        [Tooltip("Invierte el eje vertical de la mirada en todas las fuentes.")]
        [SerializeField] private bool invertY;

        // ------------------------------------------------------------- acciones cacheadas
        private InputActionAsset activeAsset;
        private InputActionMap gameplayMap;
        private InputActionMap uiMap;
        private InputAction moveAction;
        private InputAction lookDeltaAction;
        private InputAction lookStickAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private InputAction switchToolAction;
        private InputAction absorbAction;
        private InputAction fireAction;
        private InputAction grappleAction;
        private InputAction pauseAction;

        // ------------------------------------------------------------- estado por frame
        [NonSerialized] private bool initialized;
        [NonSerialized] private int lookFrame = -1;
        [NonSerialized] private Vector2 lookDegrees;
        [NonSerialized] private Vector2 stickAngularVelocity;
        [NonSerialized] private Vector2 pendingTouchDelta;
        [NonSerialized] private bool virtualSprint;
        [NonSerialized] private bool virtualAbsorb;
        [NonSerialized] private Vector2 virtualLookStick;
        [NonSerialized] private Vector2 touchStickAngularVelocity;
        [NonSerialized] private InputSchemeResolver schemeResolver;

        // ------------------------------------------------------------- eventos de pulsación
        /// <summary>Se pulsó saltar.</summary>
        public event Action JumpPressed;
        /// <summary>Se pulsó agacharse.</summary>
        public event Action CrouchPressed;
        /// <summary>Se soltó agacharse.</summary>
        public event Action CrouchReleased;
        /// <summary>Se pulsó cambiar de herramienta.</summary>
        public event Action SwitchToolPressed;
        /// <summary>Empezó a mantenerse absorber.</summary>
        public event Action AbsorbPressed;
        /// <summary>Se dejó de mantener absorber.</summary>
        public event Action AbsorbReleased;
        /// <summary>Se pulsó disparar.</summary>
        public event Action FirePressed;
        /// <summary>Se pulsó el gancho.</summary>
        public event Action GrapplePressed;
        /// <summary>Se pulsó pausa.</summary>
        public event Action PausePressed;

        // ------------------------------------------------------------- lectura continua
        /// <summary>Indica si el lector está inicializado.</summary>
        public bool IsInitialized => initialized;

        /// <summary>Asset de acciones en uso.</summary>
        public InputActionAsset Actions => activeAsset;

        /// <summary>Esquema de control activo.</summary>
        public InputScheme CurrentScheme => schemeResolver != null ? schemeResolver.Current : InputScheme.KeyboardMouse;

        /// <summary>Movimiento de este frame, con magnitud acotada a 1.</summary>
        public Vector2 Move
        {
            get
            {
                if (moveAction == null || !moveAction.enabled) return Vector2.zero;
                return Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            }
        }

        /// <summary>Giro de cámara de este frame en grados (x = guiñada, y = cabeceo; positivo hacia arriba).</summary>
        public Vector2 LookDegrees
        {
            get
            {
                // Cálculo perezoso: una sola vez por frame, sin importar cuántos sistemas lo lean.
                int frame = Time.frameCount;
                if (frame != lookFrame)
                {
                    lookFrame = frame;
                    lookDegrees = ComputeLook(Time.unscaledDeltaTime);
                }
                return lookDegrees;
            }
        }

        /// <summary>Correr está mantenido (tecla, L3 o pad táctil llevado al borde superior).</summary>
        public bool SprintHeld => virtualSprint || (sprintAction != null && sprintAction.enabled && sprintAction.IsPressed());

        /// <summary>Agacharse está mantenido.</summary>
        public bool CrouchHeld => crouchAction != null && crouchAction.enabled && crouchAction.IsPressed();

        /// <summary>Absorber está mantenido (clic derecho, LT o pad táctil mantenido).</summary>
        public bool AbsorbHeld => virtualAbsorb || (absorbAction != null && absorbAction.enabled && absorbAction.IsPressed());

        /// <summary>
        /// Indica si el mapa de juego está activo por completo.
        /// </summary>
        /// <remarks>
        /// Se consulta la acción de movimiento y no <c>InputActionMap.enabled</c>, porque este
        /// último es verdadero en cuanto una sola acción del mapa está activa, como ocurre con la
        /// acción de pausa durante <see cref="GameState.Paused"/>.
        /// </remarks>
        public bool GameplayEnabled => moveAction != null && moveAction.enabled;

        // ------------------------------------------------------------- ciclo de vida
        /// <summary>
        /// Busca y cachea las acciones, se suscribe a sus callbacks, aplica las reasignaciones
        /// guardadas e inicia la detección de esquema. Idempotente.
        /// </summary>
        public void Initialize()
        {
            if (initialized) return;

            activeAsset = actions != null ? actions : InputSystem.actions;
            if (activeAsset == null)
            {
                Debug.LogError("[InputReader] No hay asset de acciones asignado ni acciones de todo el proyecto.", this);
                return;
            }

            gameplayMap = activeAsset.FindActionMap(InputActionNames.GameplayMap, true);
            uiMap = activeAsset.FindActionMap(InputActionNames.UIMap, false);

            moveAction = gameplayMap.FindAction(InputActionNames.Move, true);
            lookDeltaAction = gameplayMap.FindAction(InputActionNames.LookDelta, true);
            lookStickAction = gameplayMap.FindAction(InputActionNames.LookStick, true);
            jumpAction = gameplayMap.FindAction(InputActionNames.Jump, true);
            sprintAction = gameplayMap.FindAction(InputActionNames.Sprint, true);
            crouchAction = gameplayMap.FindAction(InputActionNames.Crouch, true);
            switchToolAction = gameplayMap.FindAction(InputActionNames.SwitchTool, true);
            absorbAction = gameplayMap.FindAction(InputActionNames.Absorb, true);
            fireAction = gameplayMap.FindAction(InputActionNames.Fire, true);
            grappleAction = gameplayMap.FindAction(InputActionNames.Grapple, true);
            pauseAction = gameplayMap.FindAction(InputActionNames.Pause, true);

            // Los delegados se crean una sola vez aquí; en juego no hay suscripciones nuevas.
            jumpAction.performed += OnJump;
            crouchAction.performed += OnCrouchPerformed;
            crouchAction.canceled += OnCrouchCanceled;
            switchToolAction.performed += OnSwitchTool;
            absorbAction.performed += OnAbsorbPerformed;
            absorbAction.canceled += OnAbsorbCanceled;
            fireAction.performed += OnFire;
            grappleAction.performed += OnGrapple;
            pauseAction.performed += OnPause;

            LoadBindingOverrides();

            // Las acciones de todo el proyecto se activan al arrancar; partimos de todo desactivado
            // y dejamos que el estado de juego decida qué mapa activar.
            activeAsset.Disable();

            schemeResolver = new InputSchemeResolver();
            schemeResolver.Enable();

            EventBus<GameStateChanged>.Subscribe(OnGameStateChanged);
            initialized = true;
        }

        /// <summary>Cancela suscripciones y desactiva las acciones. Lo llama <c>GameRoot</c> al destruirse.</summary>
        public void Shutdown()
        {
            if (!initialized) return;
            EventBus<GameStateChanged>.Unsubscribe(OnGameStateChanged);

            jumpAction.performed -= OnJump;
            crouchAction.performed -= OnCrouchPerformed;
            crouchAction.canceled -= OnCrouchCanceled;
            switchToolAction.performed -= OnSwitchTool;
            absorbAction.performed -= OnAbsorbPerformed;
            absorbAction.canceled -= OnAbsorbCanceled;
            fireAction.performed -= OnFire;
            grappleAction.performed -= OnGrapple;
            pauseAction.performed -= OnPause;

            schemeResolver?.Disable();
            schemeResolver = null;
            activeAsset.Disable();
            ResetTransientState();
            initialized = false;
        }

        // ------------------------------------------------------------- mapas
        /// <summary>Activa el mapa de juego y desactiva el de interfaz.</summary>
        public void EnableGameplay()
        {
            if (!initialized) return;
            uiMap?.Disable();
            gameplayMap.Enable();
        }

        /// <summary>
        /// Activa el mapa de interfaz. La acción de pausa permanece activa para poder reanudar
        /// con la misma tecla o botón.
        /// </summary>
        public void EnableUI()
        {
            if (!initialized) return;
            gameplayMap.Disable();
            ResetTransientState();
            uiMap?.Enable();
            pauseAction.Enable();
        }

        /// <summary>Desactiva toda la entrada (carga y reaparición).</summary>
        public void DisableAll()
        {
            if (!initialized) return;
            gameplayMap.Disable();
            uiMap?.Disable();
            ResetTransientState();
        }

        /// <summary>Aplica la tabla del TDD §3.2: qué mapa corresponde a cada estado global.</summary>
        private void OnGameStateChanged(in GameStateChanged evt)
        {
            switch (evt.Current)
            {
                case GameState.Playing:
                    EnableGameplay();
                    break;
                case GameState.Paused:
                case GameState.MainMenu:
                case GameState.StationSelect:
                case GameState.Results:
                    EnableUI();
                    break;
                default:
                    DisableAll();
                    break;
            }
        }

        // ------------------------------------------------------------- inyección táctil
        /// <summary>Acumula un arrastre de la zona táctil de mirada, en píxeles de pantalla.</summary>
        public void AddTouchLookDelta(Vector2 pixels)
        {
            if (!GameplayEnabled) return;
            pendingTouchDelta += pixels;
            schemeResolver?.Report(InputScheme.Touch);
        }

        /// <summary>
        /// Stick de vista virtual: el pad de la aspiradora lo usa mientras se mantiene presionado, para
        /// girar la cámara sin soltar la succión. El valor va de −1 a 1 por eje y se convierte en
        /// velocidad angular igual que un stick de mando (zona muerta, curva y rampa), pero con sus
        /// propias velocidades máximas (<c>touchStickMaxSpeedX/Y</c>).
        /// </summary>
        public void SetVirtualLookStick(Vector2 value)
        {
            virtualLookStick = GameplayEnabled ? Vector2.ClampMagnitude(value, 1f) : Vector2.zero;
            if (virtualLookStick != Vector2.zero) schemeResolver?.Report(InputScheme.Touch);
        }

        /// <summary>Correr virtual del pad táctil izquierdo (llevado al borde superior).</summary>
        public void SetVirtualSprint(bool held)
        {
            virtualSprint = held && GameplayEnabled;
            if (held) schemeResolver?.Report(InputScheme.Touch);
        }

        /// <summary>Absorber virtual del pad táctil derecho (mantener).</summary>
        public void SetVirtualAbsorb(bool held)
        {
            bool next = held && GameplayEnabled;
            if (next == virtualAbsorb) return;
            virtualAbsorb = next;
            schemeResolver?.Report(InputScheme.Touch);
            if (next) AbsorbPressed?.Invoke();
            else AbsorbReleased?.Invoke();
        }

        /// <summary>Disparo virtual (toque corto del pad derecho con la aspiradora).</summary>
        public void InjectFire()
        {
            if (!GameplayEnabled) return;
            schemeResolver?.Report(InputScheme.Touch);
            FirePressed?.Invoke();
        }

        /// <summary>Gancho virtual (toque del pad derecho con el gancho).</summary>
        public void InjectGrapple()
        {
            if (!GameplayEnabled) return;
            schemeResolver?.Report(InputScheme.Touch);
            GrapplePressed?.Invoke();
        }

        // ------------------------------------------------------------- reasignación persistente
        /// <summary>Guarda las reasignaciones actuales en <c>PlayerPrefs</c>.</summary>
        public void SaveBindingOverrides()
        {
            if (activeAsset == null) return;
            PlayerPrefs.SetString(BindingOverridesKey, activeAsset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        /// <summary>Carga las reasignaciones guardadas (si existen).</summary>
        public void LoadBindingOverrides()
        {
            if (activeAsset == null || !PlayerPrefs.HasKey(BindingOverridesKey)) return;
            string json = PlayerPrefs.GetString(BindingOverridesKey);
            if (!string.IsNullOrEmpty(json)) activeAsset.LoadBindingOverridesFromJson(json);
        }

        /// <summary>Elimina las reasignaciones y restaura los controles del GDD.</summary>
        public void ClearBindingOverrides()
        {
            if (activeAsset == null) return;
            activeAsset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(BindingOverridesKey);
        }

        // ------------------------------------------------------------- cálculo de la mirada
        /// <summary>Combina las tres fuentes de mirada en grados para este frame.</summary>
        /// <param name="deltaTime">Δt sin escalar del frame, en segundos.</param>
        private Vector2 ComputeLook(float deltaTime)
        {
            if (!GameplayEnabled)
            {
                stickAngularVelocity = Vector2.zero;
                touchStickAngularVelocity = Vector2.zero;
                virtualLookStick = Vector2.zero;
                pendingTouchDelta = Vector2.zero;
                return Vector2.zero;
            }

            // 1) Ratón: el delta ya es el desplazamiento del frame, en píxeles.
            Vector2 result = lookDeltaAction.ReadValue<Vector2>() * mouseDegreesPerPixel;

            // 2) Táctil: el arrastre se normaliza por la altura de pantalla para que un mismo gesto
            //    gire lo mismo en un teléfono de 720 p que en una tableta de 1600 p.
            float screenHeight = Mathf.Max(1f, Screen.height);
            result += pendingTouchDelta * (touchDegreesPerScreenHeight / screenHeight);
            pendingTouchDelta = Vector2.zero;

            // 3) Stick: velocidad angular con zona muerta, curva y rampa; se integra por Δt.
            Vector2 stick = lookStickAction.ReadValue<Vector2>();
            stickAngularVelocity.x = AccelerateAxis(stick.x, stickAngularVelocity.x, stickMaxSpeedX, deltaTime);
            stickAngularVelocity.y = AccelerateAxis(stick.y, stickAngularVelocity.y, stickMaxSpeedY, deltaTime);
            result += stickAngularVelocity * deltaTime;

            // 4) Stick táctil del pad de la aspiradora: misma integración que el stick del mando,
            //    con su propia velocidad para que girar mientras absorbo no se sienta lento.
            touchStickAngularVelocity.x = AccelerateAxis(virtualLookStick.x, touchStickAngularVelocity.x, touchStickMaxSpeedX, deltaTime);
            touchStickAngularVelocity.y = AccelerateAxis(virtualLookStick.y, touchStickAngularVelocity.y, touchStickMaxSpeedY, deltaTime);
            result += touchStickAngularVelocity * deltaTime;

            if (invertY) result.y = -result.y;
            return result;
        }

        /// <summary>
        /// Velocidad angular de un eje del stick.
        /// </summary>
        /// <remarks>
        /// 1. Zona muerta y re-escalado: <c>m' = clamp01((|v| − z) / (1 − z))</c>, de modo que la
        ///    salida empieza en 0 justo al salir de la zona muerta y llega a 1 en el tope.
        /// 2. Curva de respuesta: <c>m'' = m'^p</c> (p = 1 conserva el comportamiento lineal del prototipo).
        /// 3. Objetivo: <c>ω* = signo(v) · m'' · ωmáx</c>.
        /// 4. Rampa: la velocidad avanza hacia ω* a razón de <c>ωmáx · a</c> grados/s², y se
        ///    reinicia a cero al invertir el sentido para no seguir girando hacia el lado anterior.
        /// </remarks>
        private float AccelerateAxis(float value, float velocity, float maxSpeed, float deltaTime)
        {
            if (deltaTime <= 0f) return 0f;
            float deadZone = Mathf.Clamp(stickDeadZone, 0f, 0.95f);
            float magnitude = Mathf.Abs(value);
            if (magnitude <= deadZone) return 0f;

            float normalized = Mathf.Clamp01((magnitude - deadZone) / (1f - deadZone));
            float shaped = Mathf.Pow(normalized, stickResponseExponent);
            float target = Mathf.Sign(value) * shaped * maxSpeed;
            if (!useStickAcceleration) return target;
            if (velocity * target < 0f) velocity = 0f;
            return Mathf.MoveTowards(velocity, target, Mathf.Abs(maxSpeed) * Mathf.Max(0.01f, stickAcceleration) * deltaTime);
        }

        /// <summary>Restablece los valores virtuales y acumulados (al cambiar de mapa o perder el foco).</summary>
        public void ResetTransientState()
        {
            stickAngularVelocity = Vector2.zero;
            touchStickAngularVelocity = Vector2.zero;
            virtualLookStick = Vector2.zero;
            pendingTouchDelta = Vector2.zero;
            virtualSprint = false;
            if (virtualAbsorb)
            {
                virtualAbsorb = false;
                AbsorbReleased?.Invoke();
            }
        }

        // ------------------------------------------------------------- callbacks del Input System
        private void OnJump(InputAction.CallbackContext context) => JumpPressed?.Invoke();
        private void OnCrouchPerformed(InputAction.CallbackContext context) => CrouchPressed?.Invoke();
        private void OnCrouchCanceled(InputAction.CallbackContext context) => CrouchReleased?.Invoke();
        private void OnSwitchTool(InputAction.CallbackContext context) => SwitchToolPressed?.Invoke();
        private void OnFire(InputAction.CallbackContext context) => FirePressed?.Invoke();
        private void OnGrapple(InputAction.CallbackContext context) => GrapplePressed?.Invoke();
        private void OnPause(InputAction.CallbackContext context) => PausePressed?.Invoke();

        private void OnAbsorbPerformed(InputAction.CallbackContext context)
        {
            // Si el pad táctil ya mantiene la succión, no se repite el evento.
            if (!virtualAbsorb) AbsorbPressed?.Invoke();
        }

        private void OnAbsorbCanceled(InputAction.CallbackContext context)
        {
            if (!virtualAbsorb) AbsorbReleased?.Invoke();
        }

        /// <summary>
        /// Al recargar el asset en el editor (salida del modo de juego sin recarga de dominio) se
        /// descarta el estado de la sesión anterior.
        /// </summary>
        private void OnDisable()
        {
            if (initialized) Shutdown();
        }
    }
}

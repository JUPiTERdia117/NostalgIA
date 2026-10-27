// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: PlayerMotor.cs
// Propósito: motor de locomoción del jugador en primera persona con máquina de
// estados Grounded / Crouching / Sliding / Airborne / Grappling
// (TDD §5.1, tarea F1-A06).
//
// Atribución: código derivado de «EasyPeasy First Person Controller» (Unity Asset
// Store, licencia Standard Unity Asset Store EULA; ver CREDITS.md), modificado y
// reestructurado por el equipo de NostalgIA: estados fusionados en un solo
// componente sin asignaciones, entrada desde InputReader, conservación de impulso
// aéreo, gancho integrado como estado y eventos de locomoción.
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Core.Events;
using NostalgIA.Core.Services;
using NostalgIA.Core.State;
using NostalgIA.Input;
using UnityEngine;

namespace NostalgIA.Gameplay.Player
{
    /// <summary>
    /// Motor del jugador: lee la entrada, integra la velocidad y mueve el <see cref="CharacterController"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Diseño de la máquina de estados.</b> En este módulo se decidió representar los estados
    /// con un enumerado y un método <c>Tick</c> por estado, en lugar de un objeto por estado
    /// como hacía el controlador original (que creaba una instancia nueva en cada transición).
    /// Así las transiciones no asignan memoria y todo el estado vive en campos de este componente.
    /// </para>
    /// <para>
    /// <b>Convención de velocidad.</b> <see cref="velocity"/> guarda la velocidad completa en m/s:
    /// las componentes x y z son el movimiento horizontal y la componente y es la velocidad vertical.
    /// Cada estado la actualiza y llama una sola vez a <c>CharacterController.Move(v · Δt)</c>
    /// (o una vez por subpaso en <see cref="LocomotionState.Grappling"/>).
    /// </para>
    /// <para>
    /// <b>Conservación de impulso aéreo (TDD §5.1).</b> En el aire, el control solo acelera hacia la
    /// dirección de entrada <c>ŵ</c> mientras la proyección <c>v·ŵ</c> sea menor que
    /// <c>maxAirSpeed</c>; nunca frena hacia una velocidad objetivo como el controlador original
    /// (que reducía 8 m/s a 5 m/s durante un salto). El exceso sobre <c>maxAirSpeed</c> decae
    /// exponencialmente con el arrastre aéreo, de modo que un salto tras deslizarse o soltar el
    /// gancho conserva la velocidad.
    /// </para>
    /// <para>
    /// <b>Responsabilidades excluidas.</b> FOV, balanceo de cámara, inclinación y sacudidas son
    /// presentación: <c>PlayerCameraFeedback</c> los calcula leyendo <see cref="IPlayerMotionState"/>.
    /// Este motor solo controla la altura y el cabeceo del pivote de cámara, que afectan a la
    /// jugabilidad (ver bajo obstáculos, apuntar).
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour, IPlayerMotionState
    {
        /// <summary>Velocidad hacia abajo con que se mantiene pegado al suelo, en m/s.</summary>
        private const float GroundStickSpeed = 5f;

        /// <summary>Velocidad vertical al abandonar el suelo sin saltar, en m/s (valor del prototipo).</summary>
        private const float LeaveGroundVerticalSpeed = -2f;

        /// <summary>Velocidad hacia abajo durante el deslizamiento en suelo, en m/s (valor del prototipo).</summary>
        private const float SlideStickSpeed = 20f;

        /// <summary>Rapidez mínima prevista para evaluar un choque frontal del deslizamiento, en m/s.</summary>
        private const float SlideCrashMinSpeed = 4f;

        /// <summary>Fracción de la rapidez prevista por debajo de la cual se considera choque.</summary>
        private const float SlideCrashRatio = 0.2f;

        [Header("Datos")]
        [Tooltip("Parámetros de diseño del jugador. Si falta, se usan los valores por defecto del prototipo.")]
        [SerializeField] private PlayerSettings settings;

        [Header("Referencias")]
        [Tooltip("Cámara del jugador (hija del pivote).")]
        [SerializeField] private Transform playerCamera;
        [Tooltip("Pivote de la cámara: el motor controla su altura y su cabeceo.")]
        [SerializeField] private Transform cameraParent;
        [Tooltip("Punto en los pies para la comprobación de suelo.")]
        [SerializeField] private Transform groundCheck;
        [Tooltip("Capas consideradas suelo y techo.")]
        [SerializeField] private LayerMask groundMask = Layers.Masks.SolidGeometry | Layers.Masks.Absorbable;

        // ------------------------------------------------------------------ estado
        private CharacterController controller;
        private InputReader input;
        private LocomotionState state = LocomotionState.Grounded;

        /// <summary>Velocidad completa en m/s (ver convención en la documentación de la clase).</summary>
        private Vector3 velocity;

        private float pitch;
        private float cameraHeight;
        private float standingHeight;
        private Vector3 standingCenter;

        private bool isGrounded;
        private Vector3 groundNormal = Vector3.up;
        private float lastGroundedTime = float.NegativeInfinity;
        private bool jumpedSinceGrounded;
        private float jumpBufferTimer;
        private bool crouchPressedThisFrame;
        private Vector2 moveInput;
        private bool sprinting;

        private float slideTimer;
        private float slideEntrySpeed;
        private Vector3 slideDirection;

        private IGrappleTether tether;
        private float grappleBlockedTime;
        private bool releaseMomentumPending;
        private float grappleRetention = 1f;
        private float grappleAirControl;

        /// <summary>Si es verdadero, los choques eliminan la componente de la velocidad contra la superficie.</summary>
        private bool projectCollisions;

        // ------------------------------------------------------------------ propiedades públicas
        /// <summary>Parámetros activos.</summary>
        public PlayerSettings Settings => settings;

        /// <summary>Cámara del jugador.</summary>
        public Transform PlayerCamera => playerCamera;

        /// <summary>Controlador de personaje.</summary>
        public CharacterController Controller => controller;

        /// <summary>Centro de la cápsula en el mundo.</summary>
        public Vector3 CapsuleCenter => transform.TransformPoint(controller.center);

        /// <inheritdoc />
        public LocomotionState State => state;

        /// <inheritdoc />
        public Vector3 Position => transform.position;

        /// <inheritdoc />
        public Vector3 Velocity => isGrounded && state != LocomotionState.Grappling
            ? new Vector3(velocity.x, 0f, velocity.z)
            : velocity;

        /// <inheritdoc />
        public float HorizontalSpeed => new Vector2(velocity.x, velocity.z).magnitude;

        /// <inheritdoc />
        public bool IsGrounded => isGrounded;

        /// <inheritdoc />
        public bool IsSprinting => sprinting;

        /// <inheritdoc />
        public Vector2 MoveInput => moveInput;

        /// <inheritdoc />
        public float SlideProgress01 => state == LocomotionState.Sliding
            ? Mathf.Clamp01(slideTimer / Mathf.Max(0.01f, settings.slideDuration))
            : 0f;

        /// <inheritdoc />
        public float WalkSpeed => settings.walkSpeed;

        /// <inheritdoc />
        public float SprintSpeed => settings.sprintSpeed;

        /// <summary>Normal del último suelo tocado.</summary>
        public Vector3 GroundNormal => groundNormal;

        /// <summary>Indica si el gancho está remolcando al jugador.</summary>
        public bool IsGrappling => state == LocomotionState.Grappling;

        // ------------------------------------------------------------------ ciclo de vida
        /// <summary>Cachea componentes y alturas de referencia.</summary>
        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (settings == null)
            {
                // Respaldo: se crea un asset temporal con los valores del prototipo. Ocurre una sola
                // vez y solo si el prefab no tiene asignado el asset.
                Debug.LogWarning("[PlayerMotor] Falta PlayerSettings; se usan los valores por defecto.", this);
                settings = ScriptableObject.CreateInstance<PlayerSettings>();
            }

            standingHeight = controller.height;
            standingCenter = controller.center;
            cameraHeight = settings.standingCameraHeight;
            if (cameraParent != null) pitch = NormalizeAngle(cameraParent.localEulerAngles.x);
        }

        /// <summary>Se registra como estado de locomoción y se suscribe a la entrada.</summary>
        private void OnEnable()
        {
            ServiceLocator.Register<IPlayerMotionState>(this);
            ServiceLocator.Register(this);
            if (ServiceLocator.TryGet(out input))
            {
                input.JumpPressed += OnJumpPressed;
                input.CrouchPressed += OnCrouchPressed;
            }
        }

        /// <summary>Cancela suscripciones y suelta el gancho.</summary>
        private void OnDisable()
        {
            if (input != null)
            {
                input.JumpPressed -= OnJumpPressed;
                input.CrouchPressed -= OnCrouchPressed;
            }
            CancelGrapple();
            ServiceLocator.Unregister<IPlayerMotionState>(this);
            ServiceLocator.Unregister(this);
        }

        /// <summary>Bucle principal: entrada, suelo, mirada, estado y alturas.</summary>
        private void Update()
        {
            float dt = Time.deltaTime;
            // Con la escala de tiempo en cero (pausa) no se integra nada.
            if (dt <= 0f || !controller.enabled) return;

            ReadFrameInput();
            RefreshGrounded();
            ApplyLook();

            switch (state)
            {
                case LocomotionState.Grounded: TickGrounded(dt); break;
                case LocomotionState.Crouching: TickCrouching(dt); break;
                case LocomotionState.Sliding: TickSliding(dt); break;
                case LocomotionState.Airborne: TickAirborne(dt); break;
                case LocomotionState.Grappling: TickGrappling(dt); break;
            }

            UpdateCapsule(dt);
            UpdateCameraHeight(dt);

            crouchPressedThisFrame = false;
            if (jumpBufferTimer > 0f) jumpBufferTimer -= dt;
        }

        // ------------------------------------------------------------------ entrada
        private void OnJumpPressed() => jumpBufferTimer = Mathf.Max(settings.jumpBufferTime, 0.0001f);

        private void OnCrouchPressed() => crouchPressedThisFrame = true;

        /// <summary>Indica si hay un salto pendiente en el búfer.</summary>
        private bool JumpBuffered => jumpBufferTimer > 0f;

        /// <summary>Lee los valores continuos de este frame.</summary>
        private void ReadFrameInput()
        {
            moveInput = input != null ? input.Move : Vector2.zero;
        }

        /// <summary>
        /// Aplica la mirada: guiñada sobre la raíz y cabeceo sobre el pivote de cámara, limitado
        /// a ±<c>maxPitch</c>.
        /// </summary>
        private void ApplyLook()
        {
            if (input == null) return;
            Vector2 look = input.LookDegrees;
            if (look.x != 0f) transform.Rotate(0f, look.x, 0f, Space.Self);
            pitch = Mathf.Clamp(pitch - look.y, -settings.maxPitch, settings.maxPitch);
            if (cameraParent != null) cameraParent.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // ------------------------------------------------------------------ suelo y techo
        /// <summary>
        /// Comprueba el suelo combinando el resultado del último <c>Move</c> con una esfera en los
        /// pies de radio 0,9·r (como el controlador original). Registra el instante para el
        /// <i>coyote time</i>.
        /// </summary>
        private void RefreshGrounded()
        {
            isGrounded = controller.isGrounded ||
                         (groundCheck != null && Physics.CheckSphere(groundCheck.position, controller.radius * 0.9f,
                             groundMask, QueryTriggerInteraction.Ignore));
            if (isGrounded && state != LocomotionState.Grappling)
            {
                lastGroundedTime = Time.time;
                if (velocity.y <= 0f) jumpedSinceGrounded = false;
            }
        }

        /// <summary>
        /// Comprueba si hay un techo que impida ponerse de pie: un <c>SphereCast</c> hacia arriba
        /// desde la parte superior de la cápsula actual con longitud igual a la diferencia de
        /// alturas más un margen de 0,1 m.
        /// </summary>
        public bool HasCeiling()
        {
            float radius = controller.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (controller.height - radius);
            float distance = standingHeight - controller.height + 0.1f;
            if (distance <= 0f) return false;
            return Physics.SphereCast(origin, radius, Vector3.up, out _, distance, groundMask, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------ estados
        /// <summary>Caminar y correr con aceleración y desaceleración.</summary>
        private void TickGrounded(float dt)
        {
            sprinting = input != null && input.SprintHeld && moveInput.y > 0f;

            if (JumpBuffered && isGrounded)
            {
                DoJump();
                TickAirborne(dt);
                return;
            }

            if (crouchPressedThisFrame && sprinting && HorizontalSpeed >= settings.slideMinSpeed)
            {
                EnterSlide();
                return;
            }

            if (!isGrounded)
            {
                velocity.y = LeaveGroundVerticalSpeed;
                ChangeState(LocomotionState.Airborne);
                return;
            }

            if (input != null && input.CrouchHeld)
            {
                ChangeState(LocomotionState.Crouching);
                return;
            }

            float speed = sprinting ? settings.sprintSpeed : settings.walkSpeed;
            MoveOnGround(speed, dt, GroundStickSpeed);
        }

        /// <summary>Movimiento agachado; se levanta al soltar si no hay techo.</summary>
        private void TickCrouching(float dt)
        {
            sprinting = false;
            if (!isGrounded)
            {
                velocity.y = LeaveGroundVerticalSpeed;
                ChangeState(LocomotionState.Airborne);
                return;
            }

            bool crouchHeld = input != null && input.CrouchHeld;
            if (!crouchHeld && !HasCeiling())
            {
                ChangeState(LocomotionState.Grounded);
                return;
            }

            MoveOnGround(settings.crouchSpeed, dt, 10f);
        }

        /// <summary>
        /// Integra el movimiento en el suelo: <c>v_h ← MoveTowards(v_h, ŵ·vmáx, a·Δt)</c>, donde
        /// <c>a</c> es la aceleración si hay entrada y la desaceleración si no la hay. La entrada
        /// diagonal se limita a magnitud 1 para no avanzar un 41 % más rápido en diagonal.
        /// </summary>
        private void MoveOnGround(float maxSpeed, float dt, float stickSpeed)
        {
            Vector3 wish = Vector3.ClampMagnitude(transform.right * moveInput.x + transform.forward * moveInput.y, 1f);
            Vector3 target = wish * maxSpeed;
            float rate = moveInput.sqrMagnitude > 0.01f ? settings.groundAcceleration : settings.groundDeceleration;

            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            horizontal = Vector3.MoveTowards(horizontal, target, rate * dt);
            velocity.x = horizontal.x;
            velocity.z = horizontal.z;
            velocity.y = LeaveGroundVerticalSpeed;

            projectCollisions = false;
            controller.Move(new Vector3(horizontal.x, -stickSpeed, horizontal.z) * dt);
        }

        /// <summary>Inicia el salto conservando la velocidad horizontal actual.</summary>
        private void DoJump()
        {
            jumpBufferTimer = 0f;
            jumpedSinceGrounded = true;
            velocity.y = settings.jumpSpeed;
            isGrounded = false;
            EventBus<PlayerJumped>.Raise(new PlayerJumped(transform.position, velocity));
            ChangeState(LocomotionState.Airborne);
        }

        /// <summary>Gravedad, control aéreo con conservación de impulso y aterrizaje.</summary>
        private void TickAirborne(float dt)
        {
            sprinting = false;

            // Salto con tiempo de gracia (coyote time) tras caminar fuera de un borde.
            bool coyote = !jumpedSinceGrounded && Time.time - lastGroundedTime <= settings.coyoteTime;
            if (JumpBuffered && coyote) DoJump();

            velocity.y -= settings.gravity * dt;
            ApplyAirControl(dt);

            float verticalBeforeMove = velocity.y;
            projectCollisions = true;
            CollisionFlags flags = controller.Move(velocity * dt);
            projectCollisions = false;

            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;

            RefreshGrounded();
            if (verticalBeforeMove <= 0f && (isGrounded || (flags & CollisionFlags.Below) != 0))
            {
                Land(-verticalBeforeMove);
            }
        }

        /// <summary>
        /// Control aéreo con conservación de impulso.
        /// </summary>
        /// <remarks>
        /// Con la dirección deseada <c>ŵ</c> y su intensidad <c>|w|</c> (≤ 1):
        /// <code>
        /// margen = maxAirSpeed·|w| − v_h·ŵ
        /// v_h ← v_h + ŵ · min(a_aire·|w|·Δt, max(0, margen))
        /// |v_h| ≤ max(maxAirSpeed, |v_h previa|)
        /// </code>
        /// Solo se añade velocidad en la dirección de la entrada mientras su proyección no supere
        /// el máximo; empujar en sentido contrario frena porque la proyección es negativa. Después,
        /// el exceso sobre <c>maxAirSpeed</c> decae con <c>e^(−k·Δt)</c> (arrastre aéreo bajo).
        /// </remarks>
        private void ApplyAirControl(float dt)
        {
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float speedBefore = horizontal.magnitude;

            Vector3 wish = Vector3.ClampMagnitude(transform.right * moveInput.x + transform.forward * moveInput.y, 1f);
            float wishMagnitude = wish.magnitude;
            if (wishMagnitude > 0.001f)
            {
                Vector3 direction = wish / wishMagnitude;
                float room = settings.maxAirSpeed * wishMagnitude - Vector3.Dot(horizontal, direction);
                if (room > 0f)
                {
                    horizontal += direction * Mathf.Min(settings.airAcceleration * wishMagnitude * dt, room);
                    horizontal = Vector3.ClampMagnitude(horizontal, Mathf.Max(settings.maxAirSpeed, speedBefore));
                }
            }

            float speed = horizontal.magnitude;
            if (speed > settings.maxAirSpeed && settings.airDrag > 0f)
            {
                // Solo el exceso decae: v = vmáx + (v − vmáx)·e^(−k·Δt).
                float excess = (speed - settings.maxAirSpeed) * Mathf.Exp(-settings.airDrag * dt);
                horizontal *= (settings.maxAirSpeed + excess) / speed;
            }

            velocity.x = horizontal.x;
            velocity.z = horizontal.z;
        }

        /// <summary>Aterrizaje: publica el impacto y vuelve al suelo.</summary>
        /// <param name="impactSpeed">Rapidez vertical de impacto, en m/s.</param>
        private void Land(float impactSpeed)
        {
            velocity.y = LeaveGroundVerticalSpeed;
            jumpedSinceGrounded = false;
            lastGroundedTime = Time.time;
            EventBus<PlayerLanded>.Raise(new PlayerLanded(transform.position, Mathf.Max(0f, impactSpeed)));
            ChangeState(LocomotionState.Grounded);
        }

        /// <summary>Entrada al deslizamiento desde la carrera.</summary>
        private void EnterSlide()
        {
            slideTimer = settings.slideDuration;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            // Se conserva la rapidez de carrera si supera a la del deslizamiento (conservación de impulso).
            slideEntrySpeed = Mathf.Max(settings.slideSpeed, horizontal.magnitude);
            slideDirection = transform.forward;
            ChangeState(LocomotionState.Sliding);
            EventBus<SlideStarted>.Raise(new SlideStarted(transform.position, horizontal));
        }

        /// <summary>
        /// Deslizamiento con inercia, pendiente, dirección lateral y detección de choque frontal.
        /// </summary>
        /// <remarks>
        /// Con el progreso restante <c>p = t / T</c> (1 → 0), la rapidez sigue
        /// <c>v = v₀ · lerp(0,5; 1; √p)</c>: cae lentamente al principio y más rápido al final.
        /// En pendiente, la dirección se proyecta sobre el plano del suelo; cuesta abajo el
        /// temporizador se recupera (deslizamiento continuo) y cuesta arriba se consume
        /// <c>slideUphillFriction</c> veces más rápido.
        /// </remarks>
        private void TickSliding(float dt)
        {
            if (JumpBuffered && isGrounded && !HasCeiling())
            {
                ExitSlide(false, Vector3.zero);
                DoJump();
                TickAirborne(dt);
                return;
            }

            if (!isGrounded)
            {
                ExitSlide(false, Vector3.zero);
                velocity.y = LeaveGroundVerticalSpeed;
                ChangeState(LocomotionState.Airborne);
                return;
            }

            UpdateSlideTimer(dt);

            if (slideTimer <= 0f)
            {
                ExitSlide(false, Vector3.zero);
                bool stayLow = HasCeiling() || (input != null && input.CrouchHeld);
                ChangeState(stayLow ? LocomotionState.Crouching : LocomotionState.Grounded);
                return;
            }

            float progress = Mathf.Clamp01(slideTimer / settings.slideDuration);
            float speed = slideEntrySpeed * Mathf.Lerp(0.5f, 1f, Mathf.Sqrt(progress));
            Vector3 steer = transform.right * (moveInput.x * settings.slideSteerControl);
            Vector3 move = slideDirection * speed + steer;

            velocity.x = move.x;
            velocity.z = move.z;
            velocity.y = LeaveGroundVerticalSpeed;

            projectCollisions = false;
            controller.Move(new Vector3(move.x, move.y - SlideStickSpeed, move.z) * dt);

            // Choque frontal: la rapidez real tras Move es mucho menor que la prevista.
            Vector3 actual = controller.velocity;
            actual.y = 0f;
            Vector3 intended = new Vector3(move.x, 0f, move.z);
            float intendedSpeed = intended.magnitude;
            if (intendedSpeed > SlideCrashMinSpeed && actual.magnitude < intendedSpeed * SlideCrashRatio)
            {
                Vector3 localImpact = transform.InverseTransformDirection(intended - actual);
                velocity.x = actual.x;
                velocity.z = actual.z;
                ExitSlide(true, localImpact.normalized);
                ChangeState(LocomotionState.Crouching);
            }
        }

        /// <summary>Consume o recupera el temporizador del deslizamiento según la pendiente.</summary>
        private void UpdateSlideTimer(float dt)
        {
            if (!settings.enableSlopeSliding ||
                !Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 2f,
                    groundMask, QueryTriggerInteraction.Ignore))
            {
                slideTimer -= dt;
                return;
            }

            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            Vector3 projected = Vector3.ProjectOnPlane(slideDirection, hit.normal).normalized;

            if (slopeAngle > 5f && projected.y < -0.05f)
                slideTimer = Mathf.Min(slideTimer + dt, settings.slideDuration);   // cuesta abajo
            else if (slopeAngle > 5f && projected.y > 0.05f)
                slideTimer -= dt * settings.slideUphillFriction;                    // cuesta arriba
            else
                slideTimer -= dt;                                                    // plano

            if (projected.sqrMagnitude > 0.0001f) slideDirection = projected;
        }

        /// <summary>Publica el fin del deslizamiento.</summary>
        private void ExitSlide(bool crashed, Vector3 localImpactDirection)
        {
            EventBus<SlideEnded>.Raise(new SlideEnded(transform.position,
                new Vector3(velocity.x, 0f, velocity.z), crashed, localImpactDirection));
        }

        // ------------------------------------------------------------------ gancho
        /// <summary>
        /// Entra en el estado de remolque. Lo llama <c>GrappleTool</c> al anclarse.
        /// </summary>
        /// <param name="newTether">Cable que remolca al jugador.</param>
        /// <param name="launchLift">Impulso vertical inicial si se dispara desde el suelo, en m/s.</param>
        public void BeginGrapple(IGrappleTether newTether, float launchLift)
        {
            if (newTether == null) return;

            if (state != LocomotionState.Grappling)
            {
                // Desde el suelo se descarta la velocidad de adherencia (negativa) y se añade el impulso.
                if (isGrounded) velocity.y = Mathf.Max(0f, velocity.y) + launchLift;
                if (state == LocomotionState.Sliding) ExitSlide(false, Vector3.zero);
            }

            if (tether != null && !ReferenceEquals(tether, newTether)) tether.Release();
            tether = newTether;
            grappleAirControl = newTether.AirControl;
            grappleRetention = Mathf.Clamp01(newTether.ReleaseMomentumRetention);
            releaseMomentumPending = true;
            grappleBlockedTime = 0f;
            isGrounded = false;
            ChangeState(LocomotionState.Grappling);
        }

        /// <summary>Suelta el gancho si está activo (portal, reaparición, desactivación).</summary>
        public void CancelGrapple()
        {
            tether?.Release();
        }

        /// <summary>
        /// Remolque del gancho integrado en subpasos de frecuencia fija (≈ 90 Hz).
        /// </summary>
        /// <remarks>
        /// Subdividir Δt en <c>n = ⌈Δt · f⌉</c> pasos (máximo 32) limita el desplazamiento por paso a
        /// <c>v / f</c>: a 24 m/s y 90 Hz son 0,27 m, menos que el radio de la cápsula, de modo que
        /// las colisiones y la detección de llegada son estables aunque el juego corra a 30 FPS.
        /// En cada subpaso: tracción del cable → retención única al soltar → gravedad (solo sin
        /// cable) → control aéreo limitado → <c>Move</c> → llegada/rebase → detección de bloqueo.
        /// </remarks>
        private void TickGrappling(float dt)
        {
            sprinting = false;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt * settings.grappleSubstepRate), 1, 32);
            float step = dt / steps;

            for (int i = 0; i < steps; i++)
            {
                bool pulling = tether != null && tether.IsAttached;
                if (pulling) velocity = tether.Accelerate(velocity, step, settings.gravity);
                ApplyReleaseMomentum();
                if (tether == null || !tether.IsAttached) velocity.y -= settings.gravity * step;

                ApplyGrappleAirControl(step);

                Vector3 previousPosition = transform.position;
                Vector3 previousCenter = CapsuleCenter;
                projectCollisions = true;
                CollisionFlags flags = controller.Move(velocity * step);
                projectCollisions = false;
                tether?.CheckArrival(previousCenter, CapsuleCenter);

                // Bloqueo: hay contacto y el desplazamiento real es menor al 20 % de 1 m/s·Δt.
                if (pulling && flags != CollisionFlags.None &&
                    (transform.position - previousPosition).sqrMagnitude < step * step * 0.04f)
                    grappleBlockedTime += step;
                else
                    grappleBlockedTime = 0f;
                if (grappleBlockedTime > settings.grappleBlockedRelease) tether?.Release();
                ApplyReleaseMomentum();

                bool attached = tether != null && tether.IsAttached;
                if (!attached && (flags & CollisionFlags.Below) != 0 && velocity.y <= 0.01f)
                {
                    float impact = -velocity.y;
                    tether = null;
                    Land(impact);
                    return;
                }
            }

            if (tether == null || !tether.IsAttached)
            {
                // Se soltó en el aire: el vuelo continúa como estado aéreo con conservación de impulso.
                tether = null;
                ChangeState(LocomotionState.Airborne);
            }
        }

        /// <summary>
        /// Control aéreo durante el remolque (física del prototipo): ignora el retroceso para no
        /// frenar con «atrás», no frena sin entrada y no supera <c>max(sprint, |v_h|)</c>.
        /// </summary>
        private void ApplyGrappleAirControl(float step)
        {
            float forward = Mathf.Max(0f, moveInput.y);
            Vector3 wish = Vector3.ClampMagnitude(transform.right * moveInput.x + transform.forward * forward, 1f);
            if (wish.sqrMagnitude <= 0.001f) return;

            Vector3 direction = wish.normalized;
            float room = Mathf.Max(0f, settings.sprintSpeed - Vector3.Dot(velocity, direction));
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float limit = Mathf.Max(settings.sprintSpeed, horizontal.magnitude);
            horizontal += direction * Mathf.Min(grappleAirControl * wish.magnitude * step, room);
            horizontal = Vector3.ClampMagnitude(horizontal, limit);
            velocity.x = horizontal.x;
            velocity.z = horizontal.z;
        }

        /// <summary>Reduce la velocidad una sola vez al soltar (sin frenado acumulativo por frame).</summary>
        private void ApplyReleaseMomentum()
        {
            if (!releaseMomentumPending || (tether != null && tether.IsAttached)) return;
            velocity *= grappleRetention;
            releaseMomentumPending = false;
        }

        // ------------------------------------------------------------------ colisiones
        /// <summary>
        /// Elimina solo la componente de la velocidad dirigida contra la superficie golpeada,
        /// conservando el deslizamiento tangencial: <c>v ← v − n·min(0, v·n)</c>.
        /// </summary>
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.7f) groundNormal = hit.normal;
            if (!projectCollisions) return;
            float intoSurface = Vector3.Dot(velocity, hit.normal);
            if (intoSurface < 0f) velocity -= hit.normal * intoSurface;
        }

        // ------------------------------------------------------------------ cápsula y cámara
        /// <summary>
        /// Interpola la altura de la cápsula hacia la de pie o agachado a velocidad constante
        /// (<c>MoveTowards</c>), manteniendo los pies fijos: centro y = altura / 2.
        /// </summary>
        private void UpdateCapsule(float dt)
        {
            bool low = state == LocomotionState.Crouching || state == LocomotionState.Sliding;
            float targetHeight = low ? settings.crouchingCapsuleHeight : standingHeight;
            if (Mathf.Approximately(controller.height, targetHeight)) return;

            float height = Mathf.MoveTowards(controller.height, targetHeight, settings.crouchTransitionSpeed * dt);
            controller.height = height;
            controller.center = new Vector3(standingCenter.x, standingCenter.y - (standingHeight - height) * 0.5f, standingCenter.z);
        }

        /// <summary>
        /// Suaviza la altura del pivote de cámara con un filtro exponencial independiente de la
        /// tasa de frames: <c>h ← h + (h* − h)·(1 − e^(−k·Δt))</c>.
        /// </summary>
        private void UpdateCameraHeight(float dt)
        {
            if (cameraParent == null) return;
            bool low = state == LocomotionState.Crouching || state == LocomotionState.Sliding;
            float target = low ? settings.crouchingCameraHeight : settings.standingCameraHeight;
            cameraHeight += (target - cameraHeight) * (1f - Mathf.Exp(-settings.cameraHeightSharpness * dt));
            Vector3 local = cameraParent.localPosition;
            local.y = cameraHeight;
            cameraParent.localPosition = local;
        }

        // ------------------------------------------------------------------ transiciones
        /// <summary>Cambia de estado y publica la transición.</summary>
        private void ChangeState(LocomotionState next)
        {
            if (next == state) return;
            LocomotionState previous = state;
            state = next;
            EventBus<LocomotionStateChanged>.Raise(new LocomotionStateChanged(previous, next));
        }

        // ------------------------------------------------------------------ API para otros sistemas
        /// <summary>
        /// Coloca al jugador en una posición y orientación sin pasar por la física. Desactiva el
        /// <see cref="CharacterController"/> durante el cambio, porque de lo contrario sobrescribe
        /// la posición en su siguiente actualización.
        /// </summary>
        /// <param name="position">Posición de los pies.</param>
        /// <param name="rotation">Orientación; solo se usa su giro horizontal.</param>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            CancelGrapple();
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            Quaternion yaw = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, Vector3.up) : transform.rotation;

            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, yaw);
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Impulso externo (burbuja trampolín, resorte): la velocidad vertical pasa a
        /// <c>max(v_y, verticalSpeed)</c>, se suma el empuje horizontal y el jugador queda en el
        /// aire. Durante el gancho no hace nada: el cable tiene prioridad sobre el movimiento.
        /// </summary>
        /// <param name="verticalSpeed">Velocidad vertical mínima tras el impulso, en m/s.</param>
        /// <param name="horizontalBoost">Velocidad horizontal que se suma, en m/s (se ignora y).</param>
        public void Launch(float verticalSpeed, Vector3 horizontalBoost)
        {
            if (state == LocomotionState.Grappling) return;
            if (state == LocomotionState.Sliding) ExitSlide(false, Vector3.zero);
            velocity.y = Mathf.Max(velocity.y, verticalSpeed);
            velocity.x += horizontalBoost.x;
            velocity.z += horizontalBoost.z;
            isGrounded = false;
            jumpedSinceGrounded = true;   // Sin salto de "coyote time" después del rebote.
            jumpBufferTimer = 0f;
            ChangeState(LocomotionState.Airborne);
        }

        /// <summary>Sustituye la velocidad (por ejemplo, rotada por un portal).</summary>
        public void SetVelocity(Vector3 newVelocity) => velocity = newVelocity;

        /// <summary>Velocidad interna completa (incluye la componente vertical).</summary>
        public Vector3 RawVelocity => velocity;

        /// <summary>
        /// Restablece el motor para una reaparición: reubica, anula velocidades, endereza la
        /// cápsula y la cámara y vuelve al estado <see cref="LocomotionState.Grounded"/>.
        /// </summary>
        public void ResetForRespawn(Vector3 position, Quaternion rotation)
        {
            Teleport(position, rotation);
            tether = null;
            velocity = Vector3.zero;
            jumpBufferTimer = 0f;
            slideTimer = 0f;
            pitch = 0f;
            controller.height = standingHeight;
            controller.center = standingCenter;
            cameraHeight = settings.standingCameraHeight;
            if (cameraParent != null) cameraParent.localRotation = Quaternion.identity;
            ChangeState(LocomotionState.Grounded);
        }

        /// <summary>Convierte un ángulo de [0, 360) a (−180, 180].</summary>
        private static float NormalizeAngle(float degrees) => degrees > 180f ? degrees - 360f : degrees;
    }
}

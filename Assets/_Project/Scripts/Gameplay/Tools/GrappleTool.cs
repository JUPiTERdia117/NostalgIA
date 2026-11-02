// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: GrappleTool.cs
// Propósito: gancho con la física de tracción validada en el prototipo,
// parametrizada en GrappleStats (GDD 2.3 B, TDD §6.4, tarea F1-A10).
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Core.Events;
using NostalgIA.Core.Services;
using NostalgIA.Core.State;
using NostalgIA.Gameplay.Aim;
using NostalgIA.Gameplay.Player;
using UnityEngine;

namespace NostalgIA.Gameplay.Tools
{
    /// <summary>
    /// Gancho: se ancla a superficies de la capa <c>Grapple</c> y remolca al jugador.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Solo engancha la capa <c>Grapple</c> (el prototipo usaba la máscara 507, que incluía el
    /// nivel, el agua, la interfaz y al propio jugador). Una superficie incompatible o fuera de
    /// alcance produce una retracción sin impulso: se publica <see cref="GrappleFired"/> con
    /// <c>Hit = false</c>. Los enemigos no pertenecen a esa capa, así que nunca se enganchan.
    /// </para>
    /// <para>
    /// <b>Tracción (por subpaso, la integra <see cref="PlayerMotor"/>).</b> Con <c>d̂</c> la dirección
    /// del centro del jugador al ancla y <c>t</c> el tiempo enganchado:
    /// <code>
    /// a(t) = aTracción · clamp01(t / tRampa)                 (rampa de aceleración)
    /// Δv∥ = min(a·Δt, max(0, vLímite − v·d̂))                 (solo se añade hacia el ancla)
    /// v ← v + d̂·Δv∥ − ŷ·g'·Δt,  g' = g_gancho ó 0,35·g_jugador
    /// </code>
    /// Limitar únicamente la componente añadida hacia el ancla conserva el impulso previo. El
    /// control del jugador mientras jala (desviarse a los lados del cable, y enderezarse solo si
    /// suelta el control) lo resuelve <see cref="PlayerMotor"/> con <see cref="SteerAcceleration"/>,
    /// <see cref="SteerMaxSpeed"/> y <see cref="LateralDamping"/>.
    /// </para>
    /// <para>
    /// <b>Salto con gancho.</b> Saltar mientras el cable jala lo suelta sin perder impulso y da un
    /// empujón hacia arriba y hacia donde miras (lo resuelve <see cref="PlayerMotor"/> con
    /// <see cref="JumpLaunchSpeed"/> y <see cref="JumpForwardBoost"/>).
    /// </para>
    /// <para>
    /// <b>Fin del enganche.</b> Se suelta por: cancelación del jugador (misma acción), llegada
    /// (distancia mínima del segmento recorrido al ancla ≤ radio de llegada), rebase (ver abajo),
    /// límite de duración, ancla desactivada o bloqueo contra
    /// una superficie. Al soltar, el motor aplica una sola vez la retención de impulso y
    /// <c>ToolController</c> inicia el enfriamiento compartido al recibir <see cref="GrappleReleased"/>.
    /// </para>
    /// <para>
    /// <b>Rebase.</b> Si el jugador ya venía acercándose al ancla (<c>v·d̂ ≥ 3 m/s</c> en algún
    /// momento) y ahora <c>v·d̂ ≤ 0</c>, significa que pasó su punto más cercano: ya se pasó del
    /// objetivo. En ese instante el cable se suelta «limpio»: no jala, no aplica la retención de
    /// impulso ni el brinco de llegada, así que el jugador sigue exactamente con la velocidad que
    /// traía. Antes el cable seguía jalando hacia atrás y lo hacía orbitar alrededor del ancla.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class GrappleTool : MonoBehaviour, ITool, IGrappleTether, IGrappleState
    {
        [Tooltip("Parámetros del gancho. Si falta, se usan los valores por defecto del prototipo.")]
        [SerializeField] private GrappleStats stats;

        [Tooltip("Boquilla del gancho (origen del cable). Por defecto, este transform.")]
        [SerializeField] private Transform nozzle;

        [Tooltip("Motor del jugador. Si falta, se busca en los padres.")]
        [SerializeField] private PlayerMotor motor;

        private bool equipped;
        private Collider anchorCollider;
        private Vector3 localAnchor;
        private Vector3 approachDirection;
        private Vector3 anchorNormal;
        private float pendingReleaseLift;
        private bool hasApproached;
        private bool pendingFreeRelease;

        /// <summary>Rapidez hacia el ancla a partir de la cual cuento que el jugador ya se acercaba, en m/s.</summary>
        private const float ApproachSpeed = 3f;
        private float elapsed;
        private float currentRange;

        /// <inheritdoc />
        public ToolId Id => ToolId.Grapple;

        /// <inheritdoc cref="ITool.Range" />
        public float Range => currentRange;

        /// <inheritdoc />
        public float MaxRange => currentRange;

        /// <summary>Parámetros activos.</summary>
        public GrappleStats Stats => stats;

        /// <summary>Duración del enfriamiento compartido tras soltar, en segundos.</summary>
        public float CooldownDuration => stats.cooldown;

        /// <inheritdoc cref="IGrappleTether.IsAttached" />
        public bool IsAttached { get; private set; }

        /// <summary>Alias de compatibilidad con la retroalimentación visual heredada.</summary>
        public bool IsGrappling => IsAttached;

        /// <inheritdoc />
        public Vector3 AnchorPoint => anchorCollider != null ? anchorCollider.transform.TransformPoint(localAnchor) : Vector3.zero;

        /// <inheritdoc />
        public float AirControl => stats.airControl;

        /// <inheritdoc />
        public float ReleaseMomentumRetention => stats.releaseMomentumRetention;

        /// <inheritdoc />
        public float SteerAcceleration => stats.steerAcceleration;

        /// <inheritdoc />
        public float SteerMaxSpeed => stats.steerMaxSpeed;

        /// <inheritdoc />
        public float LateralDamping => stats.lateralDamping;

        /// <inheritdoc />
        public float ReleaseAirAcceleration => stats.releaseAirAcceleration;

        /// <inheritdoc />
        public float ReleaseAirControlTime => stats.releaseAirControlTime;

        /// <inheritdoc />
        public float JumpLaunchSpeed => stats.jumpLaunchSpeed;

        /// <inheritdoc />
        public float JumpForwardBoost => stats.jumpForwardBoost;

        /// <summary>Posición de la boquilla.</summary>
        public Vector3 NozzlePosition => nozzle.position;

        /// <summary>
        /// Distancia de llegada: el mayor entre el radio configurado y el tamaño de la cápsula
        /// (semialtura o radio, más el ancho de piel y 0,15 m), para no «llegar» dentro de la pared.
        /// </summary>
        private float ArrivalDistance
        {
            get
            {
                CharacterController cc = motor.Controller;
                return Mathf.Max(stats.arrivalRadius,
                    Mathf.Max(cc.radius, cc.height * 0.5f) + cc.skinWidth + 0.15f);
            }
        }

        /// <summary>Resuelve datos y referencias.</summary>
        private void Awake()
        {
            if (stats == null)
            {
                Debug.LogWarning("[GrappleTool] Falta GrappleStats; se usan los valores por defecto.", this);
                stats = ScriptableObject.CreateInstance<GrappleStats>();
            }
            if (nozzle == null) nozzle = transform;
            if (motor == null) motor = GetComponentInParent<PlayerMotor>();
            currentRange = stats.maxRange;
        }

        /// <summary>Se registra como estado del gancho.</summary>
        private void OnEnable() => ServiceLocator.Register<IGrappleState>(this);

        /// <summary>Suelta el cable y retira el registro.</summary>
        private void OnDisable()
        {
            Release();
            ServiceLocator.Unregister<IGrappleState>(this);
        }

        // ------------------------------------------------------------------ ITool
        /// <inheritdoc />
        public void Equip() => equipped = true;

        /// <inheritdoc />
        public void Unequip()
        {
            equipped = false;
            Release();
        }

        /// <inheritdoc />
        public void ResetTool() => Release();

        /// <inheritdoc />
        public void Tick(in ToolInput input, in AimSample aim, float deltaTime)
        {
            if (!equipped || !input.GrapplePressed) return;
            if (IsAttached)
            {
                Release();    // Cancelar: volver a pulsar la misma acción.
                return;
            }
            TryFire(in aim);
        }

        /// <summary>Intenta anclarse en el objetivo de la retícula.</summary>
        private void TryFire(in AimSample aim)
        {
            if (motor == null || !motor.isActiveAndEnabled || !motor.Controller.enabled || !aim.IsValid) return;

            Vector3 origin = NozzlePosition;
            bool valid = aim.HasHit && aim.Collider != null &&
                         aim.Collider.gameObject.layer == Layers.Grapple &&
                         aim.Distance <= currentRange &&
                         Vector3.Distance(motor.CapsuleCenter, aim.Point) > ArrivalDistance;

            if (!valid)
            {
                EventBus<GrappleFired>.Raise(new GrappleFired(origin, aim.EndPoint(currentRange), false));
                return;
            }

            anchorCollider = aim.Collider;
            localAnchor = anchorCollider.transform.InverseTransformPoint(aim.Point);
            approachDirection = (aim.Point - motor.CapsuleCenter).normalized;
            anchorNormal = aim.Normal;
            pendingReleaseLift = 0f;
            pendingFreeRelease = false;
            hasApproached = false;
            elapsed = 0f;
            IsAttached = true;

            EventBus<GrappleFired>.Raise(new GrappleFired(origin, aim.Point, true));
            EventBus<GrappleAttached>.Raise(new GrappleAttached(aim.Point, aim.Normal));
            motor.BeginGrapple(this, stats.launchLift);
        }

        // ------------------------------------------------------------------ IGrappleTether
        /// <inheritdoc />
        public Vector3 Accelerate(Vector3 velocity, float deltaTime, float playerGravity)
        {
            if (!IsAttached) return velocity;

            elapsed += deltaTime;
            if (elapsed >= Mathf.Max(0.1f, stats.maxPullDuration) ||
                anchorCollider == null || !anchorCollider.enabled || !anchorCollider.gameObject.activeInHierarchy)
            {
                Release();
                return velocity;
            }

            Vector3 offset = AnchorPoint - motor.CapsuleCenter;
            float distance = offset.magnitude;
            if (distance <= ArrivalDistance)
            {
                ReleaseArrived();
                return velocity;
            }

            Vector3 direction = offset / distance;

            // Rebase por velocidad: ya se acercaba y ahora se aleja (o va de lado) → se pasó del
            // objetivo. Suelto sin tocar la velocidad (ni tracción ni gravedad del cable).
            float towardAnchor = Vector3.Dot(velocity, direction);
            if (towardAnchor >= ApproachSpeed) hasApproached = true;
            else if (hasApproached && towardAnchor <= 0f)
            {
                ReleaseFree();
                return velocity;
            }

            float acceleration = stats.pullAcceleration * Mathf.Clamp01(elapsed / Mathf.Max(0.01f, stats.pullRampTime));
            float speedToAdd = Mathf.Min(acceleration * deltaTime, Mathf.Max(0f, stats.pullSpeed - Vector3.Dot(velocity, direction)));
            float speedBefore = velocity.magnitude;
            velocity += direction * speedToAdd;
            // Si el jugador se desvía, el cable gira y lo que era velocidad lateral deja de contar
            // «hacia el ancla»; sin este tope la tracción la volvía a sumar y se salía disparado.
            // La tracción nunca lleva la rapidez total arriba de vLímite (salvo que ya viniera más rápido).
            velocity = Vector3.ClampMagnitude(velocity, Mathf.Max(speedBefore, stats.pullSpeed));
            float gravity = stats.gravity > 0f ? stats.gravity : playerGravity * 0.35f;
            velocity += Vector3.down * (gravity * deltaTime);
            return velocity;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Distancia mínima del segmento recorrido <c>[p₀, p₁]</c> al ancla <c>A</c>:
        /// <c>t = clamp01(((A − p₀)·Δp) / |Δp|²)</c> y <c>d = |p₀ + t·Δp − A|</c>. Comprobar el
        /// segmento y no solo la posición final evita atravesar el destino a gran velocidad.
        /// </remarks>
        public void CheckArrival(Vector3 previousCenter, Vector3 currentCenter)
        {
            if (!IsAttached || anchorCollider == null) return;

            Vector3 anchor = AnchorPoint;
            Vector3 travel = currentCenter - previousCenter;
            float t = travel.sqrMagnitude > 0.000001f
                ? Mathf.Clamp01(Vector3.Dot(anchor - previousCenter, travel) / travel.sqrMagnitude)
                : 0f;
            if (Vector3.Distance(previousCenter + travel * t, anchor) <= ArrivalDistance)
            {
                ReleaseArrived();
                return;
            }

            // Respaldo del rebase por posición: cuánto se pasó el ancla en la dirección inicial,
            // aunque sea por un costado. También suelta limpio.
            float pastAnchor = Vector3.Dot(currentCenter - anchor, approachDirection);
            if (pastAnchor >= Mathf.Max(0.1f, stats.overshootDistance)) ReleaseFree();
        }

        /// <summary>Suelta por rebase: sin retención de impulso ni brinco de llegada.</summary>
        private void ReleaseFree()
        {
            if (!IsAttached) return;
            Release();
            pendingFreeRelease = true;
        }

        /// <inheritdoc />
        public bool ConsumeFreeRelease()
        {
            bool free = pendingFreeRelease;
            pendingFreeRelease = false;
            return free;
        }

        /// <summary>
        /// Suelta por llegada. Si el ancla está en una pared o en una esfera (normal que no apunta
        /// hacia arriba) dejo pendiente un pequeño brinco para que el jugador alcance a subir al
        /// borde, como el «mantle» al final del Grappleshot. Si el ancla es un piso no hace falta.
        /// </summary>
        private void ReleaseArrived()
        {
            if (!IsAttached) return;
            float lift = anchorNormal.y < 0.6f ? stats.arrivalLift : 0f;
            Release();
            pendingReleaseLift = lift;
        }

        /// <inheritdoc />
        public float ConsumeReleaseLift()
        {
            float lift = pendingReleaseLift;
            pendingReleaseLift = 0f;
            return lift;
        }

        /// <inheritdoc />
        public void Release()
        {
            if (!IsAttached) return;
            pendingReleaseLift = 0f;
            pendingFreeRelease = false;
            IsAttached = false;
            anchorCollider = null;
            Vector3 velocity = motor != null ? motor.RawVelocity : Vector3.zero;
            EventBus<GrappleReleased>.Raise(new GrappleReleased(velocity));
        }

        /// <summary>Aplica una mejora de alcance (Fase 2).</summary>
        public void SetRange(float range) => currentRange = Mathf.Max(1f, range);
    }
}

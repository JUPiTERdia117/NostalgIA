// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: PlayerCameraFeedback.cs
// Propósito: retroalimentación de cámara del jugador (FOV, balanceo, inclinación
// y sacudida por choque) leída del estado de locomoción (TDD §4.1 y §5.1).
//
// Atribución: las curvas de FOV, balanceo, inclinación y sacudida se derivan de
// «EasyPeasy First Person Controller» (ver CREDITS.md), separadas del motor por
// el equipo de NostalgIA para cumplir el principio P1 del TDD.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Core.Services;
using NostalgIA.Core.State;
using UnityEngine;

namespace NostalgIA.Presentation.CameraFX
{
    /// <summary>
    /// Efectos de cámara en primera persona que no afectan a la jugabilidad.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Se coloca en la cámara, hija del pivote que controla <c>PlayerMotor</c> (altura y cabeceo).
    /// Este componente solo escribe la posición local de la cámara (balanceo y sacudida), su giro
    /// local en Z (inclinación) y el campo de visión. Lee el estado continuo por
    /// <see cref="IPlayerMotionState"/> cada frame y el choque del deslizamiento por evento.
    /// </para>
    /// <para>
    /// Los suavizados usan <see cref="Mathf.SmoothDamp(float, float, ref float, float)"/>, un
    /// amortiguador crítico de segundo orden que converge sin oscilar y es independiente de la tasa
    /// de frames; no asigna memoria.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class PlayerCameraFeedback : MonoBehaviour
    {
        [Header("Campo de visión (grados)")]
        [SerializeField] private bool useFovKick = true;
        [SerializeField, Range(30f, 110f)] private float normalFov = 60f;
        [SerializeField, Range(30f, 120f)] private float sprintFov = 70f;
        [SerializeField, Min(0f)] private float slideFovBoost = 5f;
        [Tooltip("Velocidad a la que el FOV alcanza el máximo durante el gancho, en m/s.")]
        [SerializeField, Min(1f)] private float grappleFovSpeed = 24f;
        [Tooltip("Inverso del tiempo de suavizado del FOV, en 1/s.")]
        [SerializeField, Min(0.1f)] private float fovChangeSpeed = 5f;

        [Header("Balanceo al caminar")]
        [SerializeField] private bool useHeadBob = true;
        [SerializeField, Min(0f)] private float bobAmount = 0.03f;
        [SerializeField, Min(0f)] private float bobSpeed = 12f;

        [Header("Inclinación")]
        [SerializeField] private bool useCameraTilt = true;
        [Tooltip("Inclinación lateral al desplazarse de lado, en grados.")]
        [SerializeField, Min(0f)] private float strafeTiltAmount = 1.5f;
        [Tooltip("Inclinación máxima durante el deslizamiento, en grados.")]
        [SerializeField] private float slideTiltAmount = -5f;

        [Header("Sacudida por choque del deslizamiento")]
        [SerializeField, Min(0f)] private float crashShakeIntensity = 0.15f;
        [SerializeField, Min(0.05f)] private float crashShakeDuration = 0.4f;

        private Camera cam;
        private IPlayerMotionState motion;
        private Vector3 baseLocalPosition;

        private float fovVelocity;
        private float bobTimer;
        private float tilt;
        private float tiltVelocity;

        private float shakeTimer;
        private float shakeIntensity;
        private Vector3 shakeDirection;

        /// <summary>Cachea la cámara y la posición local de referencia.</summary>
        private void Awake()
        {
            cam = GetComponent<Camera>();
            baseLocalPosition = transform.localPosition;
        }

        /// <summary>Suscripción al fin del deslizamiento.</summary>
        private void OnEnable() => EventBus<SlideEnded>.Subscribe(OnSlideEnded);

        /// <summary>Cancela la suscripción y restablece la cámara.</summary>
        private void OnDisable()
        {
            EventBus<SlideEnded>.Unsubscribe(OnSlideEnded);
            transform.localPosition = baseLocalPosition;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>Inicia la sacudida direccional si el deslizamiento terminó en choque.</summary>
        private void OnSlideEnded(in SlideEnded evt)
        {
            if (!evt.Crashed) return;
            shakeIntensity = crashShakeIntensity;
            shakeTimer = crashShakeDuration;
            shakeDirection = evt.LocalImpactDirection;
        }

        /// <summary>Se ejecuta después del motor para usar el estado final del frame.</summary>
        private void LateUpdate()
        {
            if (motion == null && !ServiceLocator.TryGet(out motion)) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            LocomotionState state = motion.State;
            UpdateFov(state, dt);

            float offsetY = UpdateBob(state, dt);
            float roll = UpdateTilt(state);
            ApplyShake(dt, ref offsetY, ref roll);

            transform.localPosition = baseLocalPosition + new Vector3(0f, offsetY, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        }

        /// <summary>
        /// FOV objetivo por estado: correr → <c>sprintFov</c>; deslizar → <c>sprintFov + boost·p</c>;
        /// gancho o caída rápida → <c>lerp(normal, sprint, InverseLerp(vCaminar, vGancho, |v|))</c>.
        /// </summary>
        private void UpdateFov(LocomotionState state, float dt)
        {
            float target = normalFov;
            if (useFovKick)
            {
                switch (state)
                {
                    case LocomotionState.Grounded:
                        if (motion.IsSprinting) target = sprintFov;
                        break;
                    case LocomotionState.Sliding:
                        target = sprintFov + slideFovBoost * motion.SlideProgress01;
                        break;
                    case LocomotionState.Grappling:
                    case LocomotionState.Airborne:
                        target = Mathf.Lerp(normalFov, sprintFov,
                            Mathf.InverseLerp(motion.WalkSpeed, grappleFovSpeed, motion.Velocity.magnitude));
                        break;
                }
            }
            cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, target, ref fovVelocity, 1f / fovChangeSpeed, Mathf.Infinity, dt);
        }

        /// <summary>
        /// Balanceo vertical: <c>y = A·sin(φ)</c> con <c>φ += ω·Δt</c>; al correr, A × 1,5 y ω × 1,3.
        /// Sin movimiento, la fase vuelve suavemente a cero para no saltar al reanudar.
        /// </summary>
        private float UpdateBob(LocomotionState state, float dt)
        {
            bool walking = useHeadBob && state == LocomotionState.Grounded && motion.IsGrounded && motion.HorizontalSpeed > 0.1f;
            if (!walking)
            {
                bobTimer = Mathf.Lerp(bobTimer, 0f, dt * 10f);
                return 0f;
            }
            bool sprint = motion.IsSprinting;
            bobTimer += dt * bobSpeed * (sprint ? 1.3f : 1f);
            return Mathf.Sin(bobTimer) * bobAmount * (sprint ? 1.5f : 1f);
        }

        /// <summary>Inclinación lateral: <c>−x·k</c> al desplazarse de lado, más <c>k_slide·p</c> al deslizar.</summary>
        private float UpdateTilt(LocomotionState state)
        {
            float target = 0f;
            if (useCameraTilt)
            {
                target = -motion.MoveInput.x * strafeTiltAmount;
                if (state == LocomotionState.Sliding) target += slideTiltAmount * motion.SlideProgress01;
            }
            tilt = Mathf.SmoothDamp(tilt, target, ref tiltVelocity, 0.1f);
            return tilt;
        }

        /// <summary>
        /// Sacudida direccional (derivada del controlador original): con <c>s = (t / T)³</c>
        /// (caída cúbica), la cámara baja <c>I·s·(|dz| + 0,5)</c> por el golpe frontal y rueda
        /// <c>15·I·s·dx</c> hacia el lado del impacto, más un traqueteo de ruido Perlin.
        /// </summary>
        private void ApplyShake(float dt, ref float offsetY, ref float roll)
        {
            if (shakeTimer <= 0f) return;
            shakeTimer -= dt;
            float normalized = Mathf.Clamp01(shakeTimer / crashShakeDuration);
            float factor = normalized * normalized * normalized;

            float dip = -shakeIntensity * factor * (Mathf.Abs(shakeDirection.z) + 0.5f);
            float side = shakeDirection.x;
            float rollKick = Mathf.Abs(side) < 0.1f
                ? shakeIntensity * 5f * factor * (Mathf.PerlinNoise(Time.time, 0f) > 0.5f ? 1f : -1f)
                : shakeIntensity * 15f * side * factor;
            float rattle = (Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f) * (shakeIntensity * 0.2f) * factor;

            offsetY += dip + rattle;
            roll += rollKick + rattle * 5f;
        }
    }
}

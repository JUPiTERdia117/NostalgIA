using UnityEngine;

namespace EasyPeasyFirstPersonController
{
    public partial class FirstPersonController
    {
        private GrapleController activeGrapple;
        private Vector3 grappleVelocity;
        private bool grappleFlight;
        private bool movingGrapple;
        private float grappleAirControl;
        private float grappleBlockedTime;
        private float grappleMomentumRetention;
        private bool releaseMomentumPending;

        public void BeginGrappleMotion(GrapleController grapple, float lift)
        {
            if (!grappleFlight)
            {
                grappleVelocity = currentVelocity;
                grappleVelocity.y = isGrounded ? Mathf.Max(0f, moveDirection.y) : moveDirection.y;
                if (isGrounded) grappleVelocity.y += lift;
                currentState.ExitState();
                currentState = states.Fall();
                currentState.EnterState();
            }
            if (activeGrapple != null && activeGrapple != grapple) activeGrapple.Release();
            activeGrapple = grapple;
            grappleAirControl = grapple.AirControl;
            grappleMomentumRetention = grapple.ReleaseMomentumRetention;
            releaseMomentumPending = true;
            grappleBlockedTime = 0f;
            grappleFlight = true;
        }

        // Este modo es el único que mueve al jugador durante el gancho y su vuelo posterior.
        private bool UpdateGrappleMotion()
        {
            if (!grappleFlight) return false;
            if (!characterController.enabled || isInWater)
            {
                FinishGrappleFlight();
                return false;
            }

            // Subpasos limitan los cambios de trayectoria y las colisiones a baja frecuencia.
            int steps = Mathf.Clamp(Mathf.CeilToInt(Time.deltaTime / (1f / 90f)), 1, 32);
            float dt = Time.deltaTime / steps;
            for (int i = 0; i < steps; i++)
            {
                bool pulling = activeGrapple != null && activeGrapple.IsGrappling;
                if (pulling) grappleVelocity = activeGrapple.Accelerate(grappleVelocity, dt);
                ApplyReleaseMomentum();
                if (activeGrapple == null || !activeGrapple.IsGrappling)
                    grappleVelocity += Vector3.down * gravity * dt;

                // Ignora el retroceso durante el gancho y el vuelo posterior para evitar frenar con S.
                float forwardInput = Mathf.Max(0f, input.moveInput.y);
                Vector3 wish = Vector3.ClampMagnitude(
                    transform.right * input.moveInput.x + transform.forward * forwardInput, 1f);
                // Sin input no hay frenado aéreo. El control no puede añadir velocidad sin límite.
                if (wish.sqrMagnitude > 0.001f)
                {
                    Vector3 direction = wish.normalized;
                    float room = Mathf.Max(0f, sprintSpeed - Vector3.Dot(grappleVelocity, direction));
                    Vector3 horizontal = new Vector3(grappleVelocity.x, 0f, grappleVelocity.z);
                    float speedLimit = Mathf.Max(sprintSpeed, horizontal.magnitude);
                    horizontal += direction * Mathf.Min(grappleAirControl * wish.magnitude * dt, room);
                    horizontal = Vector3.ClampMagnitude(horizontal, speedLimit);
                    grappleVelocity.x = horizontal.x;
                    grappleVelocity.z = horizontal.z;
                }

                Vector3 previousPosition = transform.position;
                Vector3 previousCenter = transform.TransformPoint(characterController.center);
                movingGrapple = true;
                CollisionFlags flags = characterController.Move(grappleVelocity * dt);
                movingGrapple = false;
                if (activeGrapple != null) activeGrapple.CheckArrival(previousCenter);

                if (pulling && flags != CollisionFlags.None &&
                    (transform.position - previousPosition).sqrMagnitude < dt * dt * 0.04f)
                    grappleBlockedTime += dt;
                else grappleBlockedTime = 0f;
                if (grappleBlockedTime > 0.25f && activeGrapple != null) activeGrapple.Release();
                ApplyReleaseMomentum();

                currentVelocity = new Vector3(grappleVelocity.x, 0f, grappleVelocity.z);
                moveDirection.y = grappleVelocity.y;
                bool attached = activeGrapple != null && activeGrapple.IsGrappling;
                if (!attached && (flags & CollisionFlags.Below) != 0 && grappleVelocity.y <= 0.01f)
                {
                    isGrounded = true;
                    FinishGrappleFlight();
                    // La fricción normal empieza en el siguiente frame, sin un segundo Move.
                    return true;
                }
            }
            isGrounded = characterController.isGrounded;
            currentBobIntensity = 0f;
            targetTilt = 0f;
            targetFov = Mathf.Lerp(normalFov, sprintFov,
                Mathf.InverseLerp(walkSpeed, 24f, grappleVelocity.magnitude));
            return true;
        }

        private void ApplyReleaseMomentum()
        {
            if (!releaseMomentumPending || (activeGrapple != null && activeGrapple.IsGrappling)) return;
            // Reduce la velocidad una sola vez al soltar, sin frenado acumulativo por frame.
            grappleVelocity *= grappleMomentumRetention;
            currentVelocity = new Vector3(grappleVelocity.x, 0f, grappleVelocity.z);
            moveDirection.y = grappleVelocity.y;
            releaseMomentumPending = false;
        }

        private void FinishGrappleFlight()
        {
            if (activeGrapple != null) activeGrapple.Release();
            ApplyReleaseMomentum();
            activeGrapple = null;
            grappleFlight = false;
            currentState.ExitState();
            currentState = isInWater ? states.Swimming() : (isGrounded ? states.Grounded() : states.Fall());
            currentState.EnterState();
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!movingGrapple) return;
            // Elimina solo la velocidad contra la superficie, conservando el deslizamiento lateral.
            float intoSurface = Vector3.Dot(grappleVelocity, hit.normal);
            if (intoSurface < 0f) grappleVelocity -= hit.normal * intoSurface;
        }
    }
}

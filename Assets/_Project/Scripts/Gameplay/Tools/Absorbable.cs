// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: Absorbable.cs
// Propósito: objeto que la aspiradora puede absorber, almacenar y disparar, con
// registro cinemático para el daño por impacto F = m·a.
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Core.Damage;
using NostalgIA.Core.Events;
using NostalgIA.Core.Lifecycle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.Gameplay.Tools
{
    /// <summary>
    /// Objeto absorbible: trayectoria de absorción en espiral, almacenamiento, lanzamiento y
    /// rotura en fragmentos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sustituye a <c>PoolableController</c> (reutiliza su GUID, por lo que las instancias de las
    /// escenas conservan sus valores) y absorbe la funcionalidad de <c>ThrowableDamage</c>, que
    /// duplicaba el mismo registro cinemático.
    /// </para>
    /// <para>
    /// <b>Trayectoria.</b> Con progreso <c>t ∈ [0, 1]</c> que avanza con el tiempo
    /// (<c>t += Δt / duración</c>), la posición es
    /// <code>
    /// p(t) = lerp(p₀, boquilla, t) + (û·cos θ + v̂·sin θ) · R·sin(π·t),   θ = 2π·vueltas·t
    /// </code>
    /// donde <c>û, v̂</c> son perpendiculares a la dirección inicial. El factor <c>sin(π·t)</c> hace
    /// que el radio nazca y muera en cero, sin saltos en los extremos. La escala se reduce como
    /// <c>s(t) = s₀·(1 − t)</c>. Durante la trayectoria el cuerpo es cinemático para que la física
    /// no compita con el movimiento por código.
    /// </para>
    /// <para>
    /// <b>Cinemática para el daño.</b> Cada paso de física se estima <c>a = |Δv| / Δt</c>
    /// (más la gravedad en caída libre) y se conserva un pico durante 0,25 s; los enemigos usan
    /// ese valor para calcular <c>F = m·a</c> al recibir el impacto. Al lanzarse se registra la
    /// aceleración equivalente del impulso: <c>a = (J / Δt) / m</c>.
    /// </para>
    /// <para>
    /// <b>Capas.</b> En vuelo tras un disparo, los colliders pasan a <c>PlayerProjectile</c> (que
    /// no colisiona con el jugador) y vuelven a <c>Absorbable</c> al detenerse.
    /// </para>
    /// <para>
    /// <b>Reinicio.</b> Al romperse se desactiva en lugar de destruirse; al reaparecer el jugador
    /// (<see cref="IResettable"/>) vuelve a su posición inicial, de modo que los acertijos de la
    /// estación no quedan sin objetos.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Absorbable : MonoBehaviour, IResettable
    {
        /// <summary>Tiempo mínimo tras el lanzamiento antes de considerar que el objeto se detuvo, en segundos.</summary>
        private const float ThrownSettleDelay = 0.6f;

        /// <summary>Rapidez² por debajo de la cual un objeto lanzado se considera detenido, en (m/s)².</summary>
        private const float ThrownSettleSpeedSqr = 0.25f;

        [Header("Combate (F = m·a)")]
        [Tooltip("Daño mínimo garantizado cuando el jugador lanza este objeto contra un enemigo.")]
        [SerializeField, Min(1)] private int impactDamage = 1;
        [Tooltip("Multiplicador de la fuerza de impacto.")]
        [SerializeField, Min(0.1f)] private float forceMultiplier = 1f;
        [Tooltip("Si está activo, el objeto se rompe en fragmentos al golpear a un enemigo.")]
        [SerializeField] private bool destroyOnEnemyHit = true;
        [Tooltip("Fragmentos emitidos al romperse.")]
        [SerializeField, Min(0)] private int debrisCount = 6;
        [Tooltip("Velocidad mínima de impacto para dañar cuando no fue lanzado por el jugador, en m/s.")]
        [SerializeField, Min(0f)] private float minImpactVelocity = 1.8f;

        // ------------------------------------------------------------------ referencias cacheadas
        private Rigidbody body;
        private Collider[] colliders;
        private int[] originalLayers;
        private Renderer mainRenderer;

        // ------------------------------------------------------------------ estado inicial (reinicio)
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 originalScale;
        private bool initialKinematic;

        // ------------------------------------------------------------------ absorción
        private VacuumTool puller;
        private Vector3 startPosition;
        private Vector3 pullStartScale;
        private Vector3 restoreStartScale;
        private Vector3 spiralRight;
        private Vector3 spiralUp;
        private float progress;
        private float restoreProgress = 1f;
        private float restoreDuration = 0.2f;
        private bool kinematicBeforePull;

        // ------------------------------------------------------------------ almacenamiento
        private Bounds storedBounds;

        // ------------------------------------------------------------------ cinemática
        private Vector3 previousVelocity;
        private float currentAcceleration;
        private float peakAcceleration;
        private float peakTimer;
        private float launchAcceleration;
        private float thrownAt = float.NegativeInfinity;

        // ------------------------------------------------------------------ propiedades
        /// <summary>Daño mínimo garantizado al ser lanzado por el jugador.</summary>
        public int ImpactDamage => Mathf.Max(1, impactDamage);

        /// <summary>Multiplicador de fuerza.</summary>
        public float ForceMultiplier => Mathf.Max(0.1f, forceMultiplier);

        /// <summary>Indica si se rompe al golpear a un enemigo.</summary>
        public bool DestroyOnEnemyHit => destroyOnEnemyHit;

        /// <summary>Velocidad mínima de impacto sin lanzamiento.</summary>
        public float MinImpactVelocity => minImpactVelocity;

        /// <summary>Indica si está roto (desactivado hasta el reinicio).</summary>
        public bool IsDestroyed { get; private set; }

        /// <summary>Indica si viaja tras un disparo del jugador.</summary>
        public bool IsThrown { get; private set; }

        /// <summary>Indica si recorre una trayectoria de absorción.</summary>
        public bool IsBeingPulled => puller != null;

        /// <summary>Indica si está guardado en el depósito.</summary>
        public bool IsStored { get; private set; }

        /// <summary>Aceleración reciente (con pico retenido), en m/s².</summary>
        public float CurrentAcceleration => Mathf.Max(currentAcceleration, peakAcceleration);

        /// <summary>Aceleración equivalente del último lanzamiento, en m/s².</summary>
        public float LaunchAcceleration => launchAcceleration;

        /// <summary>Velocidad del paso de física anterior (antes del impacto), en m/s.</summary>
        public Vector3 PreImpactVelocity => previousVelocity;

        /// <summary>Cuerpo rígido.</summary>
        public Rigidbody Body => body;

        /// <summary>Caja de colisión capturada al almacenarlo (centro relativo al pivote).</summary>
        public Bounds StoredBounds => storedBounds;

        /// <summary>Centro de masa en el mundo (o el pivote si no hay cuerpo rígido).</summary>
        public Vector3 Center => body != null ? body.worldCenterOfMass : transform.position;

        // ------------------------------------------------------------------ ciclo de vida
        /// <summary>Cachea componentes y el estado inicial; se registra para el reinicio.</summary>
        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            // Arreglos creados una sola vez por objeto, durante la carga.
            colliders = GetComponentsInChildren<Collider>(true);
            originalLayers = new int[colliders.Length];
            for (int i = 0; i < colliders.Length; i++) originalLayers[i] = colliders[i].gameObject.layer;
            mainRenderer = GetComponentInChildren<Renderer>(true);

            initialPosition = transform.position;
            initialRotation = transform.rotation;
            originalScale = transform.localScale;
            initialKinematic = body != null && body.isKinematic;

            ResettableRegistry.Register(this);
        }

        /// <summary>Retira el registro.</summary>
        private void OnDestroy() => ResettableRegistry.Unregister(this);

        /// <summary>
        /// Saca el objeto almacenado de su nivel de origen y lo pasa a <paramref name="scene"/>.
        /// Se separa de su padre (<see cref="SceneManager.MoveGameObjectToScene"/> solo acepta
        /// raíces) y se retira del registro de reinicio: su posición inicial pertenece a un nivel
        /// que ya no existe, por lo que no debe volver a ella al reaparecer.
        /// </summary>
        /// <param name="scene">Escena del nivel nuevo.</param>
        internal void DetachToScene(Scene scene)
        {
            if (gameObject.scene == scene) return;
            transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            ResettableRegistry.Unregister(this);
        }

        /// <summary>Restablece el estado transitorio al desactivarse (almacenado o roto).</summary>
        private void OnDisable()
        {
            if (puller != null) CancelPull(false);
            transform.localScale = originalScale;
            restoreProgress = 1f;
            previousVelocity = Vector3.zero;
            currentAcceleration = 0f;
            peakAcceleration = 0f;
            launchAcceleration = 0f;
            SetThrown(false);
        }

        /// <summary>Paso de física: cinemática, trayectoria de absorción o recuperación de escala.</summary>
        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            TrackKinematics(dt);

            if (puller != null)
            {
                if (!puller.IsSuctioning)
                {
                    CancelPull(true);
                    return;
                }
                StepPull(dt);
                return;
            }

            if (restoreProgress < 1f)
            {
                restoreProgress = Mathf.Clamp01(restoreProgress + dt / restoreDuration);
                transform.localScale = Vector3.Lerp(restoreStartScale, originalScale, restoreProgress);
            }
        }

        // ------------------------------------------------------------------ cinemática
        /// <summary>
        /// Estima la aceleración del paso: <c>a = |v − v_prev| / Δt</c>; en caída libre con
        /// gravedad se toma al menos <c>g + 1,5·|v_y|</c> (heurística del prototipo para el
        /// aplastamiento). Se retiene el pico 0,25 s para que el impacto lo encuentre.
        /// </summary>
        private void TrackKinematics(float dt)
        {
            if (body == null || body.isKinematic || body.IsSleeping())
            {
                previousVelocity = Vector3.zero;
                return;
            }

            dt = Mathf.Max(0.0001f, dt);
            Vector3 velocity = body.linearVelocity;
            float acceleration = (velocity - previousVelocity).magnitude / dt;
            if (velocity.y < -0.5f && body.useGravity)
                acceleration = Mathf.Max(acceleration, Physics.gravity.magnitude + Mathf.Abs(velocity.y) * 1.5f);

            currentAcceleration = acceleration;
            if (acceleration >= peakAcceleration)
            {
                peakAcceleration = acceleration;
                peakTimer = 0.25f;
            }
            else if (peakTimer > 0f) peakTimer -= dt;
            else peakAcceleration = acceleration;

            previousVelocity = velocity;

            if (IsThrown && Time.time - thrownAt > ThrownSettleDelay && velocity.sqrMagnitude < ThrownSettleSpeedSqr)
            {
                SetThrown(false);
                launchAcceleration = 0f;
            }
        }

        // ------------------------------------------------------------------ absorción
        /// <summary>
        /// Inicia la trayectoria hacia la boquilla de <paramref name="vacuum"/>.
        /// </summary>
        /// <returns><c>false</c> si el objeto no puede absorberse ahora.</returns>
        public bool BeginPull(VacuumTool vacuum)
        {
            if (vacuum == null || puller != null || IsDestroyed || IsStored || !isActiveAndEnabled) return false;

            puller = vacuum;
            startPosition = transform.position;
            pullStartScale = transform.localScale;
            progress = 0f;

            if (body != null)
            {
                kinematicBeforePull = body.isKinematic;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
            }

            Vector3 direction = vacuum.NozzlePosition - startPosition;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            // Si la dirección es casi vertical se usa otro eje de referencia para evitar un producto cruzado nulo.
            Vector3 reference = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
            spiralRight = Vector3.Cross(direction, reference).normalized;
            spiralUp = Vector3.Cross(direction, spiralRight).normalized;
            return true;
        }

        /// <summary>Avanza la trayectoria un paso de física.</summary>
        private void StepPull(float dt)
        {
            VacuumStats stats = puller.Stats;
            Vector3 destination = puller.NozzlePosition;
            float arriveRadius = Mathf.Max(0f, stats.absorptionDistance);

            progress = Mathf.Clamp01(progress + dt / Mathf.Max(0.01f, stats.absorptionDuration));
            if (progress >= 1f || (transform.position - destination).sqrMagnitude <= arriveRadius * arriveRadius)
            {
                FinishPull();
                return;
            }

            float angle = progress * stats.spiralTurns * Mathf.PI * 2f;
            float radius = stats.spiralRadius * Mathf.Sin(progress * Mathf.PI);
            Vector3 offset = (spiralRight * Mathf.Cos(angle) + spiralUp * Mathf.Sin(angle)) * radius;

            // El destino se reevalúa cada paso para seguir a la boquilla aunque el jugador se mueva.
            transform.position = Vector3.Lerp(startPosition, destination, progress) + offset;
            transform.localScale = pullStartScale * (1f - progress);
        }

        /// <summary>Llega a la boquilla: la aspiradora decide si lo guarda.</summary>
        private void FinishPull()
        {
            VacuumTool vacuum = puller;
            transform.localScale = originalScale;
            if (!vacuum.TryStore(this)) CancelPull(true);
        }

        /// <summary>Interrumpe la trayectoria y deja caer el objeto donde está.</summary>
        /// <param name="restoreScaleGradually">Recupera la escala con una transición breve.</param>
        public void CancelPull(bool restoreScaleGradually)
        {
            VacuumTool vacuum = puller;
            puller = null;
            if (body != null) body.isKinematic = kinematicBeforePull;
            if (restoreScaleGradually && vacuum != null)
            {
                restoreDuration = Mathf.Max(0.01f, vacuum.Stats.scaleRestoreDuration);
                restoreStartScale = transform.localScale;
                restoreProgress = 0f;
            }
            else
            {
                transform.localScale = originalScale;
                restoreProgress = 1f;
            }
            vacuum?.NotifyPullEnded(this);
        }

        /// <summary>
        /// Guarda el objeto: captura su caja de colisión a tamaño completo y lo desactiva.
        /// Lo llama <see cref="VacuumTool"/>.
        /// </summary>
        internal void StoreInside()
        {
            VacuumTool vacuum = puller;
            puller = null;
            transform.localScale = originalScale;
            if (body != null) body.isKinematic = kinematicBeforePull;

            // Se sincronizan las transformaciones para que los bounds reflejen la escala restaurada.
            Physics.SyncTransforms();
            var bounds = new Bounds(transform.position, Vector3.zero);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c != null && c.enabled && !c.isTrigger) bounds.Encapsulate(c.bounds);
            }
            // Se guarda el centro relativo al pivote para recolocarlo al disparar.
            storedBounds = new Bounds(bounds.center - transform.position, bounds.size);

            IsStored = true;
            gameObject.SetActive(false);
            vacuum?.NotifyPullEnded(this);
        }

        // ------------------------------------------------------------------ lanzamiento
        /// <summary>
        /// Saca el objeto del depósito en <paramref name="spawnCenter"/> y aplica el impulso y el giro.
        /// </summary>
        /// <param name="spawnCenter">Centro de la caja en el punto de aparición.</param>
        /// <param name="direction">Dirección normalizada de salida.</param>
        /// <param name="impulse">Impulso en N·s; Δv = J / m.</param>
        /// <param name="angularVelocity">Velocidad angular inicial, en rad/s.</param>
        internal void LaunchFromStorage(Vector3 spawnCenter, Vector3 direction, float impulse, Vector3 angularVelocity)
        {
            IsStored = false;
            transform.position = spawnCenter - storedBounds.center;
            gameObject.SetActive(true);

            if (body != null)
            {
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
                body.AddForce(direction * Mathf.Max(0f, impulse), ForceMode.Impulse);
                body.maxAngularVelocity = Mathf.Max(body.maxAngularVelocity, angularVelocity.magnitude);
                body.angularVelocity = angularVelocity;
            }

            MarkThrownByPlayer(impulse);
        }

        /// <summary>
        /// Registra el lanzamiento: <c>a = (J / Δt) / m</c> con el paso de física como Δt.
        /// </summary>
        public void MarkThrownByPlayer(float impulse)
        {
            float mass = body != null ? Mathf.Max(0.1f, body.mass) : 1f;
            float dt = Mathf.Max(0.005f, Time.fixedDeltaTime);
            launchAcceleration = impulse / dt / mass;
            peakAcceleration = Mathf.Max(peakAcceleration, launchAcceleration);
            peakTimer = 0.5f;
            thrownAt = Time.time;
            SetThrown(true);
        }

        /// <summary>Cambia el estado de proyectil y la capa de sus colliders.</summary>
        private void SetThrown(bool thrown)
        {
            IsThrown = thrown;
            if (colliders == null) return;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;
                colliders[i].gameObject.layer = thrown ? Layers.PlayerProjectile : originalLayers[i];
            }
        }

        // ------------------------------------------------------------------ impactos
        /// <summary>Publica el impacto de un objeto lanzado y notifica a los receptores de impacto.</summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (!IsThrown || IsDestroyed) return;

            float mass = body != null ? body.mass : 1f;
            float force = mass * CurrentAcceleration * ForceMultiplier;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Vector3 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector3.up;
            EventBus<ProjectileImpact>.Raise(new ProjectileImpact(point, normal, collision.gameObject.layer, force));

            IImpactReceiver receiver = collision.collider.GetComponentInParent<IImpactReceiver>();
            receiver?.ReceiveImpact(point, force);
        }

        /// <summary>Rompe el objeto en el punto indicado.</summary>
        public void BreakApart(Vector3 contactPoint)
        {
            if (IsDestroyed) return;
            IsDestroyed = true;

            Material material = mainRenderer != null ? mainRenderer.sharedMaterial : null;
            Vector3 size = transform.lossyScale;
            float characteristicSize = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            EventBus<BreakableDestroyed>.Raise(new BreakableDestroyed(contactPoint, debrisCount, material, characteristicSize));

            gameObject.SetActive(false);
        }

        /// <summary>Rompe el objeto en su posición.</summary>
        public void BreakApart() => BreakApart(transform.position);

        /// <inheritdoc />
        /// <remarks>
        /// Solo los objetos rotos vuelven a su lugar: los que el jugador movió se quedan donde
        /// están y los almacenados siguen en el depósito.
        /// </remarks>
        public void ResetState()
        {
            if (!IsDestroyed) return;
            IsDestroyed = false;
            IsStored = false;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            transform.localScale = originalScale;
            if (body != null)
            {
                body.isKinematic = initialKinematic;
                if (!initialKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
            gameObject.SetActive(true);
        }

        /// <summary>Configura los valores de combate desde el constructor de escena (migración).</summary>
        public void ConfigureCombat(int damage, float multiplier, bool breakOnEnemy, int fragments, float minVelocity)
        {
            impactDamage = Mathf.Max(1, damage);
            forceMultiplier = Mathf.Max(0.1f, multiplier);
            destroyOnEnemyHit = breakOnEnemy;
            debrisCount = Mathf.Max(0, fragments);
            minImpactVelocity = Mathf.Max(0f, minVelocity);
        }
    }
}

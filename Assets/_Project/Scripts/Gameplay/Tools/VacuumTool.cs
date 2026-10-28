// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: VacuumTool.cs
// Propósito: Aspiradora Lanzadora: succión en cono, almacenamiento FIFO y disparo
// con verificación de salida (GDD 2.3 A, TDD §6.3, tarea F1-A09).
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Core.Events;
using NostalgIA.Core.Services;
using NostalgIA.Core.State;
using NostalgIA.Gameplay.Aim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.Gameplay.Tools
{
    /// <summary>
    /// Aspiradora Lanzadora.
    /// </summary>
    /// <remarks>
    /// <para>Estados (TDD §6.3): Idle → Suctioning (Absorb mantenido y capacidad disponible) →
    /// Idle al soltar o al llenarse; Fire con objetos almacenados lanza el frente de la cola.</para>
    /// <para>
    /// <b>Detección en cono.</b> En cada paso de física con succión activa se ejecuta
    /// <c>Physics.OverlapSphereNonAlloc</c> sobre la capa <c>Absorbable</c> con radio igual al
    /// alcance, escribiendo en un arreglo reservado una sola vez. Cada candidato se filtra por:
    /// </para>
    /// <list type="number">
    /// <item>Ángulo: <c>(c − o)/|c − o| · d̂ ≥ cos(α)</c>, con <c>o</c> el ojo, <c>d̂</c> la mirada y
    /// <c>α</c> el semiángulo. Comparar cosenos evita calcular <c>acos</c>.</item>
    /// <item>Distancia: <c>|c − o| ≤ alcance</c> (la esfera admite candidatos cuyo collider toca el
    /// borde aunque su centro esté fuera).</item>
    /// <item>Línea de visión: un <c>Physics.Raycast</c> contra la geometría sólida no debe golpear
    /// nada entre el ojo y el centro del objeto.</item>
    /// </list>
    /// <para>
    /// Hasta <c>maxSimultaneousPulls</c> objetos recorren su trayectoria a la vez, lo que permite
    /// barrer una zona girando la cámara (GDD 2.2). Los enemigos no están en la capa
    /// <c>Absorbable</c> y, además, se exige el componente <see cref="Absorbable"/>.
    /// </para>
    /// <para>
    /// <b>Disparo.</b> Se consulta el frente de la cola, se calcula el punto de aparición a una
    /// distancia igual a la proyección de la caja del objeto sobre la dirección de salida más una
    /// holgura, y un rayo desde la cámara hasta el borde delantero de esa caja comprueba que la
    /// salida esté libre. Solo si lo está se retira el objeto de la cola.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class VacuumTool : MonoBehaviour, ITool, IVacuumState
    {
        /// <summary>Capacidad del arreglo de candidatos del cono.</summary>
        private const int CandidateBufferSize = 64;

        /// <summary>Capas que bloquean la línea de visión de la succión.</summary>
        private const int LineOfSightMask = Layers.Masks.SolidGeometry;

        /// <summary>Capas que impiden un lanzamiento (todo lo sólido salvo el jugador y lo ignorado por la puntería).</summary>
        private const int LaunchObstacleMask = AimService.AimMask;

        [Tooltip("Parámetros de la aspiradora. Si falta, se usan los valores por defecto del prototipo.")]
        [SerializeField] private VacuumStats stats;

        [Tooltip("Boquilla: destino de la absorción y origen del disparo. Por defecto, este transform.")]
        [SerializeField] private Transform nozzle;

        private readonly Collider[] candidates = new Collider[CandidateBufferSize];
        private Absorbable[] activePulls;
        private int activePullCount;

        private VacuumStorage storage;
        private bool equipped;
        private bool suctioning;
        private Vector3 eyePosition;
        private Vector3 eyeDirection = Vector3.forward;
        private bool hasAim;

        /// <inheritdoc />
        public ToolId Id => ToolId.Vacuum;

        /// <inheritdoc />
        public float Range => stats.range;

        /// <summary>Parámetros activos.</summary>
        public VacuumStats Stats => stats;

        /// <summary>Posición de la boquilla en el mundo.</summary>
        public Vector3 NozzlePosition => nozzle.position;

        /// <inheritdoc />
        public int StoredCount => storage.Count;

        /// <inheritdoc />
        public int Capacity => storage.Capacity;

        /// <inheritdoc />
        public float Fill01 => storage.Fill01;

        /// <inheritdoc />
        public bool IsSuctioning => suctioning;

        /// <summary>Objetos en trayectoria de absorción.</summary>
        public int PullsInFlight => activePullCount;

        /// <summary>Indica si el depósito más las absorciones en curso alcanzan la capacidad.</summary>
        private bool CapacityReached => storage.Count + activePullCount >= storage.Capacity;

        /// <summary>Resuelve datos y reserva los arreglos.</summary>
        private void Awake()
        {
            if (stats == null)
            {
                Debug.LogWarning("[VacuumTool] Falta VacuumStats; se usan los valores por defecto.", this);
                stats = ScriptableObject.CreateInstance<VacuumStats>();
            }
            if (nozzle == null) nozzle = transform;
            storage = new VacuumStorage(stats.baseCapacity);
            activePulls = new Absorbable[Mathf.Max(1, stats.maxSimultaneousPulls)];
        }

        /// <summary>Se registra como estado de la aspiradora.</summary>
        private void OnEnable() => ServiceLocator.Register<IVacuumState>(this);

        /// <summary>Detiene la succión y retira el registro.</summary>
        private void OnDisable()
        {
            StopSuction();
            ServiceLocator.Unregister<IVacuumState>(this);
        }

        // ------------------------------------------------------------------ ITool
        /// <inheritdoc />
        public void Equip() => equipped = true;

        /// <inheritdoc />
        public void Unequip()
        {
            equipped = false;
            StopSuction();
        }

        /// <inheritdoc />
        public void ResetTool() => StopSuction();

        /// <inheritdoc />
        public void Tick(in ToolInput input, in AimSample aim, float deltaTime)
        {
            hasAim = aim.IsValid;
            if (hasAim)
            {
                eyePosition = aim.Ray.origin;
                eyeDirection = aim.Ray.direction;
            }

            if (!equipped) return;

            // La succión se mantiene mientras el depósito no esté lleno; las absorciones en curso
            // ya cuentan contra la capacidad en ScanCone, de modo que no se inician de más.
            if (input.AbsorbHeld && hasAim && !storage.IsFull) StartSuction();
            else if (!input.AbsorbHeld || storage.IsFull) StopSuction();

            if (input.FirePressed) TryLaunch(in aim);
        }

        // ------------------------------------------------------------------ succión
        /// <summary>Activa la succión y publica el cambio.</summary>
        private void StartSuction()
        {
            if (suctioning) return;
            suctioning = true;
            EventBus<SuctionStateChanged>.Raise(new SuctionStateChanged(true, NozzlePosition, eyeDirection));
        }

        /// <summary>Detiene la succión; las trayectorias en curso se cancelan solas en su siguiente paso.</summary>
        private void StopSuction()
        {
            if (!suctioning) return;
            suctioning = false;
            EventBus<SuctionStateChanged>.Raise(new SuctionStateChanged(false, NozzlePosition, eyeDirection));
        }

        /// <summary>Busca candidatos en el cono durante cada paso de física con succión activa.</summary>
        private void FixedUpdate()
        {
            if (!suctioning || !hasAim) return;
            ScanCone();
        }

        /// <summary>Selecciona hasta llenar las trayectorias simultáneas permitidas.</summary>
        private void ScanCone()
        {
            float range = stats.range;
            float cosHalfAngle = Mathf.Cos(stats.coneHalfAngle * Mathf.Deg2Rad);
            int count = Physics.OverlapSphereNonAlloc(eyePosition, range, candidates, Layers.Masks.Absorbable,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count && activePullCount < activePulls.Length && !CapacityReached; i++)
            {
                Collider candidate = candidates[i];
                candidates[i] = null;
                if (candidate == null) continue;

                Absorbable absorbable = ResolveAbsorbable(candidate);
                if (absorbable == null || absorbable.IsBeingPulled || absorbable.IsDestroyed || absorbable.IsStored) continue;

                Vector3 toTarget = absorbable.Center - eyePosition;
                float distance = toTarget.magnitude;
                if (distance > range || distance < 0.0001f) continue;

                Vector3 direction = toTarget / distance;
                if (Vector3.Dot(direction, eyeDirection) < cosHalfAngle) continue;

                if (Physics.Raycast(eyePosition, direction, distance, LineOfSightMask, QueryTriggerInteraction.Ignore)) continue;

                if (absorbable.BeginPull(this)) activePulls[activePullCount++] = absorbable;
            }

            // Se limpian las posiciones restantes para no retener referencias a colliders destruidos.
            for (int i = 0; i < count; i++) candidates[i] = null;
        }

        /// <summary>Obtiene el <see cref="Absorbable"/> del cuerpo rígido o de los padres del collider.</summary>
        private static Absorbable ResolveAbsorbable(Collider collider)
        {
            Rigidbody attached = collider.attachedRigidbody;
            if (attached != null && attached.TryGetComponent(out Absorbable fromBody)) return fromBody;
            return collider.GetComponentInParent<Absorbable>();
        }

        /// <summary>Un absorbible terminó o canceló su trayectoria: libera su posición.</summary>
        internal void NotifyPullEnded(Absorbable absorbable)
        {
            for (int i = 0; i < activePullCount; i++)
            {
                if (!ReferenceEquals(activePulls[i], absorbable)) continue;
                // Eliminación por intercambio con el último: O(1).
                activePullCount--;
                activePulls[i] = activePulls[activePullCount];
                activePulls[activePullCount] = null;
                return;
            }
        }

        /// <summary>
        /// Un absorbible llegó a la boquilla: se almacena si hay espacio.
        /// </summary>
        /// <returns><c>false</c> si el depósito está lleno.</returns>
        internal bool TryStore(Absorbable absorbable)
        {
            if (!storage.TryStore(absorbable)) return false;
            absorbable.StoreInside();
            EventBus<ObjectAbsorbed>.Raise(new ObjectAbsorbed(storage.Count, storage.Capacity, absorbable.transform));

            if (storage.IsFull)
            {
                StopSuction();
                EventBus<VacuumFull>.Raise(new VacuumFull(storage.Capacity));
            }
            return true;
        }

        // ------------------------------------------------------------------ disparo
        /// <summary>Intenta lanzar el frente de la cola hacia el punto de la retícula.</summary>
        private void TryLaunch(in AimSample aim)
        {
            storage.PurgeDestroyedFront();
            if (!storage.TryPeek(out Absorbable front))
            {
                EventBus<VacuumEmptyTrigger>.Raise(new VacuumEmptyTrigger());
                return;
            }
            if (!aim.IsValid) return;

            Vector3 origin = NozzlePosition;
            Vector3 direction = aim.EndPoint(stats.launchAimDistance) - origin;
            if (direction.sqrMagnitude < 0.0001f) direction = aim.Ray.direction;
            direction.Normalize();

            // Semidimensiones proyectadas sobre la dirección: e = |d.x|·ex + |d.y|·ey + |d.z|·ez.
            Bounds bounds = front.StoredBounds;
            Vector3 extents = Vector3.Max(bounds.extents, Vector3.one * 0.01f);
            float forwardExtent = Mathf.Abs(direction.x) * extents.x +
                                  Mathf.Abs(direction.y) * extents.y +
                                  Mathf.Abs(direction.z) * extents.z;
            float spawnDistance = forwardExtent + stats.launchClearance;
            Vector3 spawnCenter = origin + direction * spawnDistance;

            // La salida se comprueba desde el ojo (siempre dentro de la cápsula, nunca dentro de un muro)
            // hasta el borde delantero de la caja en el punto de aparición.
            Vector3 frontEdge = spawnCenter + direction * forwardExtent;
            Vector3 toEdge = frontEdge - aim.Ray.origin;
            float edgeDistance = toEdge.magnitude;
            if (edgeDistance > 0.0001f &&
                Physics.Raycast(aim.Ray.origin, toEdge / edgeDistance, edgeDistance, LaunchObstacleMask, QueryTriggerInteraction.Ignore))
                return;

            storage.Dequeue();

            // Giro inicial inversamente proporcional al tamaño, con un máximo, y eje ligeramente
            // inclinado al azar para variar cada disparo.
            float size = Mathf.Max(0.1f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
            float spin = Mathf.Min(stats.maxLaunchSpin, stats.launchSpin / size);
            Vector3 spinAxis = (nozzle.right + nozzle.up * Random.Range(-0.35f, 0.35f)).normalized;

            front.LaunchFromStorage(spawnCenter, direction, stats.launchImpulse, spinAxis * spin);
            float mass = front.Body != null ? front.Body.mass : 1f;
            EventBus<ObjectLaunched>.Raise(new ObjectLaunched(spawnCenter, direction, storage.Count, storage.Capacity, mass));
        }

        /// <summary>Aplica una mejora de capacidad (Fase 2).</summary>
        public void SetCapacity(int capacity) => storage.SetCapacity(capacity);

        /// <summary>
        /// Lleva los objetos almacenados a la escena del nivel nuevo al cruzar una puerta de nivel;
        /// así el jugador conserva lo que absorbió aunque el nivel anterior se descargue.
        /// </summary>
        /// <param name="scene">Escena del nivel nuevo.</param>
        public void MoveStoredItemsToScene(Scene scene) => storage?.MoveItemsToScene(scene);
    }
}

// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: GameEvents.cs
// Propósito: catálogo de eventos del juego.
// -----------------------------------------------------------------------------
//
// Convenciones del catálogo:
// - Cada evento es un `readonly struct` con campos públicos de solo lectura y un
//   constructor que los inicializa todos. Así la carga útil es inmutable y se
//   construye en la pila del publicador sin asignar memoria.
// - Las referencias a objetos de Unity (Transform, Material) se guardan como
//   referencias: copiar una referencia dentro de un struct no crea objetos.
// - El estado continuo de alta frecuencia (velocidad del jugador, llenado del
//   depósito, enfriamiento restante) NO se publica por frame: se consulta en las
//   interfaces de `NostalgIA.Core.State` registradas en el ServiceLocator.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.Core.Events
{
    // ===================================================================== Estado global

    /// <summary>Cambio de estado global. Publica <c>GameStateMachine</c>.</summary>
    public readonly struct GameStateChanged : IGameEvent
    {
        /// <summary>Estado anterior.</summary>
        public readonly GameState Previous;
        /// <summary>Estado nuevo.</summary>
        public readonly GameState Current;

        /// <summary>Crea el evento.</summary>
        public GameStateChanged(GameState previous, GameState current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>Cambio del esquema de control activo. Publica <c>InputSchemeResolver</c>.</summary>
    public readonly struct InputSchemeChanged : IGameEvent
    {
        /// <summary>Esquema anterior.</summary>
        public readonly InputScheme Previous;
        /// <summary>Esquema nuevo.</summary>
        public readonly InputScheme Current;

        /// <summary>Crea el evento.</summary>
        public InputSchemeChanged(InputScheme previous, InputScheme current)
        {
            Previous = previous;
            Current = current;
        }
    }

    // ===================================================================== Locomoción

    /// <summary>Transición de la máquina de locomoción. Publica <c>PlayerMotor</c>.</summary>
    public readonly struct LocomotionStateChanged : IGameEvent
    {
        /// <summary>Estado anterior.</summary>
        public readonly LocomotionState Previous;
        /// <summary>Estado nuevo.</summary>
        public readonly LocomotionState Current;

        /// <summary>Crea el evento.</summary>
        public LocomotionStateChanged(LocomotionState previous, LocomotionState current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>Inicio de un salto. Publica <c>PlayerMotor</c>.</summary>
    public readonly struct PlayerJumped : IGameEvent
    {
        /// <summary>Posición de los pies al despegar.</summary>
        public readonly Vector3 Position;
        /// <summary>Velocidad total al despegar, en m/s.</summary>
        public readonly Vector3 Velocity;

        /// <summary>Crea el evento.</summary>
        public PlayerJumped(Vector3 position, Vector3 velocity)
        {
            Position = position;
            Velocity = velocity;
        }
    }

    /// <summary>Aterrizaje tras estar en el aire. Publica <c>PlayerMotor</c>.</summary>
    public readonly struct PlayerLanded : IGameEvent
    {
        /// <summary>Posición de los pies al tocar el suelo.</summary>
        public readonly Vector3 Position;
        /// <summary>Rapidez vertical de impacto (positiva hacia abajo), en m/s.</summary>
        public readonly float ImpactSpeed;

        /// <summary>Crea el evento.</summary>
        public PlayerLanded(Vector3 position, float impactSpeed)
        {
            Position = position;
            ImpactSpeed = impactSpeed;
        }
    }

    /// <summary>Inicio de un deslizamiento. Publica <c>PlayerMotor</c>.</summary>
    public readonly struct SlideStarted : IGameEvent
    {
        /// <summary>Posición de los pies.</summary>
        public readonly Vector3 Position;
        /// <summary>Velocidad horizontal al iniciar, en m/s.</summary>
        public readonly Vector3 Velocity;

        /// <summary>Crea el evento.</summary>
        public SlideStarted(Vector3 position, Vector3 velocity)
        {
            Position = position;
            Velocity = velocity;
        }
    }

    /// <summary>Fin de un deslizamiento. Publica <c>PlayerMotor</c>.</summary>
    public readonly struct SlideEnded : IGameEvent
    {
        /// <summary>Posición de los pies.</summary>
        public readonly Vector3 Position;
        /// <summary>Velocidad horizontal al terminar, en m/s.</summary>
        public readonly Vector3 Velocity;
        /// <summary>Indica si el deslizamiento terminó por un choque frontal.</summary>
        public readonly bool Crashed;
        /// <summary>Dirección del choque en el espacio local del jugador (cero si no hubo choque).</summary>
        public readonly Vector3 LocalImpactDirection;

        /// <summary>Crea el evento.</summary>
        public SlideEnded(Vector3 position, Vector3 velocity, bool crashed, Vector3 localImpactDirection)
        {
            Position = position;
            Velocity = velocity;
            Crashed = crashed;
            LocalImpactDirection = localImpactDirection;
        }
    }

    // ===================================================================== Herramientas

    /// <summary>Cambio de herramienta activa. Publica <c>ToolController</c>.</summary>
    public readonly struct ToolSwitched : IGameEvent
    {
        /// <summary>Herramienta que queda activa.</summary>
        public readonly ToolId Tool;

        /// <summary>Crea el evento.</summary>
        public ToolSwitched(ToolId tool) => Tool = tool;
    }

    /// <summary>
    /// Inicio o fin del enfriamiento compartido. Publica <c>ToolController</c>.
    /// </summary>
    /// <remarks>
    /// Se publica solo al empezar (<see cref="Remaining"/> = duración) y al terminar (cero). El
    /// valor intermedio se lee cada frame en <c>IToolState</c> para no publicar eventos por frame.
    /// </remarks>
    public readonly struct ToolCooldownChanged : IGameEvent
    {
        /// <summary>Segundos restantes.</summary>
        public readonly float Remaining;
        /// <summary>Fracción restante entre 0 y 1.</summary>
        public readonly float Normalized;

        /// <summary>Crea el evento.</summary>
        public ToolCooldownChanged(float remaining, float normalized)
        {
            Remaining = remaining;
            Normalized = normalized;
        }
    }

    /// <summary>Inicio o fin de la succión. Publica <c>VacuumTool</c>.</summary>
    public readonly struct SuctionStateChanged : IGameEvent
    {
        /// <summary>Indica si la succión quedó activa.</summary>
        public readonly bool Active;
        /// <summary>Origen del cono (boquilla).</summary>
        public readonly Vector3 Origin;
        /// <summary>Dirección del cono, normalizada.</summary>
        public readonly Vector3 Direction;

        /// <summary>Crea el evento.</summary>
        public SuctionStateChanged(bool active, Vector3 origin, Vector3 direction)
        {
            Active = active;
            Origin = origin;
            Direction = direction;
        }
    }

    /// <summary>Un objeto terminó su trayectoria y quedó almacenado. Publica <c>VacuumTool</c>.</summary>
    public readonly struct ObjectAbsorbed : IGameEvent
    {
        /// <summary>Objetos almacenados tras la absorción.</summary>
        public readonly int Stored;
        /// <summary>Capacidad actual del depósito.</summary>
        public readonly int Capacity;
        /// <summary>Transform del objeto absorbido.</summary>
        public readonly Transform Source;

        /// <summary>Crea el evento.</summary>
        public ObjectAbsorbed(int stored, int capacity, Transform source)
        {
            Stored = stored;
            Capacity = capacity;
            Source = source;
        }
    }

    /// <summary>Se disparó un objeto almacenado. Publica <c>VacuumTool</c>.</summary>
    public readonly struct ObjectLaunched : IGameEvent
    {
        /// <summary>Posición de aparición del objeto.</summary>
        public readonly Vector3 Origin;
        /// <summary>Dirección de lanzamiento, normalizada.</summary>
        public readonly Vector3 Direction;
        /// <summary>Objetos que quedan almacenados.</summary>
        public readonly int Stored;
        /// <summary>Capacidad actual del depósito.</summary>
        public readonly int Capacity;
        /// <summary>Masa del objeto lanzado, en kg.</summary>
        public readonly float Mass;

        /// <summary>Crea el evento.</summary>
        public ObjectLaunched(Vector3 origin, Vector3 direction, int stored, int capacity, float mass)
        {
            Origin = origin;
            Direction = direction;
            Stored = stored;
            Capacity = capacity;
            Mass = mass;
        }
    }

    /// <summary>El depósito alcanzó su capacidad. Publica <c>VacuumTool</c>.</summary>
    public readonly struct VacuumFull : IGameEvent
    {
        /// <summary>Capacidad alcanzada.</summary>
        public readonly int Capacity;

        /// <summary>Crea el evento.</summary>
        public VacuumFull(int capacity) => Capacity = capacity;
    }

    /// <summary>Se intentó disparar con el depósito vacío. Publica <c>VacuumTool</c>.</summary>
    public readonly struct VacuumEmptyTrigger : IGameEvent { }

    /// <summary>Disparo del gancho. Publica <c>GrappleTool</c>.</summary>
    public readonly struct GrappleFired : IGameEvent
    {
        /// <summary>Origen del disparo (boquilla).</summary>
        public readonly Vector3 Origin;
        /// <summary>Punto objetivo bajo la retícula.</summary>
        public readonly Vector3 Target;
        /// <summary>Indica si el gancho encontró una superficie de anclaje válida.</summary>
        public readonly bool Hit;

        /// <summary>Crea el evento.</summary>
        public GrappleFired(Vector3 origin, Vector3 target, bool hit)
        {
            Origin = origin;
            Target = target;
            Hit = hit;
        }
    }

    /// <summary>El gancho quedó anclado. Publica <c>GrappleTool</c>.</summary>
    public readonly struct GrappleAttached : IGameEvent
    {
        /// <summary>Punto de anclaje en el mundo.</summary>
        public readonly Vector3 Point;
        /// <summary>Normal de la superficie de anclaje.</summary>
        public readonly Vector3 Normal;

        /// <summary>Crea el evento.</summary>
        public GrappleAttached(Vector3 point, Vector3 normal)
        {
            Point = point;
            Normal = normal;
        }
    }

    /// <summary>El gancho se soltó (cancelación, llegada, bloqueo o límite). Publica <c>GrappleTool</c>.</summary>
    public readonly struct GrappleReleased : IGameEvent
    {
        /// <summary>Velocidad del jugador conservada tras soltar, en m/s.</summary>
        public readonly Vector3 Velocity;

        /// <summary>Crea el evento.</summary>
        public GrappleReleased(Vector3 velocity) => Velocity = velocity;
    }

    /// <summary>Cambió el tipo de objetivo bajo la retícula. Publica <c>AimService</c>.</summary>
    public readonly struct AimTargetChanged : IGameEvent
    {
        /// <summary>Tipo anterior.</summary>
        public readonly AimTargetKind Previous;
        /// <summary>Tipo nuevo.</summary>
        public readonly AimTargetKind Current;

        /// <summary>Crea el evento.</summary>
        public AimTargetChanged(AimTargetKind previous, AimTargetKind current)
        {
            Previous = previous;
            Current = current;
        }
    }

    // ===================================================================== Física y objetos

    /// <summary>Impacto de un proyectil (absorbible lanzado o proyectil enemigo).</summary>
    public readonly struct ProjectileImpact : IGameEvent
    {
        /// <summary>Punto de contacto.</summary>
        public readonly Vector3 Point;
        /// <summary>Normal de la superficie golpeada.</summary>
        public readonly Vector3 Normal;
        /// <summary>Capa física de la superficie golpeada.</summary>
        public readonly int SurfaceLayer;
        /// <summary>Fuerza estimada del impacto, en newtons (F = m·a).</summary>
        public readonly float Force;

        /// <summary>Crea el evento.</summary>
        public ProjectileImpact(Vector3 point, Vector3 normal, int surfaceLayer, float force)
        {
            Point = point;
            Normal = normal;
            SurfaceLayer = surfaceLayer;
            Force = force;
        }
    }

    /// <summary>Un objeto rompible se destruyó en fragmentos. Consumido por los VFX.</summary>
    public readonly struct BreakableDestroyed : IGameEvent
    {
        /// <summary>Centro del estallido.</summary>
        public readonly Vector3 Position;
        /// <summary>Cantidad de fragmentos a emitir.</summary>
        public readonly int FragmentCount;
        /// <summary>Material compartido del objeto, para que los fragmentos lo hereden.</summary>
        public readonly Material Material;
        /// <summary>Tamaño característico del objeto, en metros.</summary>
        public readonly float Size;

        /// <summary>Crea el evento.</summary>
        public BreakableDestroyed(Vector3 position, int fragmentCount, Material material, float size)
        {
            Position = position;
            FragmentCount = fragmentCount;
            Material = material;
            Size = size;
        }
    }

    // ===================================================================== Jugador

    /// <summary>El jugador recibió daño. Publica <c>PlayerHealth</c>.</summary>
    public readonly struct PlayerDamaged : IGameEvent
    {
        /// <summary>Corazones restados.</summary>
        public readonly int Amount;
        /// <summary>Corazones restantes.</summary>
        public readonly int Current;
        /// <summary>Corazones máximos.</summary>
        public readonly int Max;
        /// <summary>Posición del origen del daño, para indicadores direccionales.</summary>
        public readonly Vector3 SourcePosition;

        /// <summary>Crea el evento.</summary>
        public PlayerDamaged(int amount, int current, int max, Vector3 sourcePosition)
        {
            Amount = amount;
            Current = current;
            Max = max;
            SourcePosition = sourcePosition;
        }
    }

    /// <summary>El jugador murió. Publica <c>PlayerHealth</c>.</summary>
    public readonly struct PlayerDied : IGameEvent
    {
        /// <summary>Causa de la muerte.</summary>
        public readonly DeathCause Cause;
        /// <summary>Posición del jugador al morir.</summary>
        public readonly Vector3 Position;

        /// <summary>Crea el evento.</summary>
        public PlayerDied(DeathCause cause, Vector3 position)
        {
            Cause = cause;
            Position = position;
        }
    }

    /// <summary>El jugador reapareció. Publica <c>RespawnService</c>.</summary>
    public readonly struct PlayerRespawned : IGameEvent
    {
        /// <summary>Índice del checkpoint usado (−1 = punto inicial de la escena).</summary>
        public readonly int CheckpointIndex;
        /// <summary>Posición de reaparición.</summary>
        public readonly Vector3 Position;
        /// <summary>Corazones al reaparecer.</summary>
        public readonly int Hearts;

        /// <summary>Crea el evento.</summary>
        public PlayerRespawned(int checkpointIndex, Vector3 position, int hearts)
        {
            CheckpointIndex = checkpointIndex;
            Position = position;
            Hearts = hearts;
        }
    }

    /// <summary>Se activó un checkpoint. Publica <c>Checkpoint</c>.</summary>
    public readonly struct CheckpointActivated : IGameEvent
    {
        /// <summary>Índice del checkpoint dentro de la estación.</summary>
        public readonly int Index;
        /// <summary>Posición de reaparición.</summary>
        public readonly Vector3 SpawnPosition;
        /// <summary>Orientación de reaparición (solo giro horizontal).</summary>
        public readonly Quaternion SpawnRotation;
        /// <summary>Texto descriptivo para la notificación del HUD.</summary>
        public readonly string Label;

        /// <summary>Crea el evento.</summary>
        public CheckpointActivated(int index, Vector3 spawnPosition, Quaternion spawnRotation, string label)
        {
            Index = index;
            SpawnPosition = spawnPosition;
            SpawnRotation = spawnRotation;
            Label = label;
        }
    }

    /// <summary>Se recogió una estrella. Publica la estrella coleccionable.</summary>
    public readonly struct StarCollected : IGameEvent
    {
        /// <summary>Índice de la estrella (0–2).</summary>
        public readonly int Index;
        /// <summary>Posición de la estrella.</summary>
        public readonly Vector3 Position;

        /// <summary>Crea el evento.</summary>
        public StarCollected(int index, Vector3 position)
        {
            Index = index;
            Position = position;
        }
    }

    // ===================================================================== Enemigos

    /// <summary>Apareció un enemigo. Publica <c>EnemyBase</c>.</summary>
    public readonly struct EnemySpawned : IGameEvent
    {
        /// <summary>Tipo de enemigo.</summary>
        public readonly EnemyKind Kind;
        /// <summary>Posición de aparición.</summary>
        public readonly Vector3 Position;
        /// <summary>Indica si el generador ya reprodujo su clip de audio localmente.</summary>
        public readonly bool PlayedLocally;

        /// <summary>Crea el evento.</summary>
        public EnemySpawned(EnemyKind kind, Vector3 position, bool playedLocally = false)
        {
            Kind = kind;
            Position = position;
            PlayedLocally = playedLocally;
        }
    }

    /// <summary>Un enemigo recibió daño. Publica <c>EnemyBase</c>.</summary>
    public readonly struct EnemyDamaged : IGameEvent
    {
        /// <summary>Tipo de enemigo.</summary>
        public readonly EnemyKind Kind;
        /// <summary>Posición del enemigo.</summary>
        public readonly Vector3 Position;
        /// <summary>Daño recibido.</summary>
        public readonly int Damage;
        /// <summary>Vida restante.</summary>
        public readonly int Remaining;

        /// <summary>Crea el evento.</summary>
        public EnemyDamaged(EnemyKind kind, Vector3 position, int damage, int remaining)
        {
            Kind = kind;
            Position = position;
            Damage = damage;
            Remaining = remaining;
        }
    }

    /// <summary>Murió un enemigo. Publica <c>EnemyBase</c>.</summary>
    public readonly struct EnemyKilled : IGameEvent
    {
        /// <summary>Tipo de enemigo.</summary>
        public readonly EnemyKind Kind;
        /// <summary>Posición del enemigo.</summary>
        public readonly Vector3 Position;

        /// <summary>Crea el evento.</summary>
        public EnemyKilled(EnemyKind kind, Vector3 position)
        {
            Kind = kind;
            Position = position;
        }
    }

    /// <summary>Un enemigo ejecutó su ataque (disparo, mordida o salto). Publican los controladores de enemigos.</summary>
    public readonly struct EnemyAttacked : IGameEvent
    {
        /// <summary>Tipo de enemigo que realizó el ataque.</summary>
        public readonly EnemyKind Kind;
        /// <summary>Posición donde ocurrió el ataque.</summary>
        public readonly Vector3 Position;
        /// <summary>Indica si el propio enemigo ya reprodujo su clip de audio localmente.</summary>
        public readonly bool PlayedLocally;

        /// <summary>Crea el evento.</summary>
        public EnemyAttacked(EnemyKind kind, Vector3 position, bool playedLocally = false)
        {
            Kind = kind;
            Position = position;
            PlayedLocally = playedLocally;
        }
    }

    /// <summary>Golpe de área del Antivirus. Publica su comportamiento.</summary>
    public readonly struct AntivirusSlam : IGameEvent
    {
        /// <summary>Centro del golpe.</summary>
        public readonly Vector3 Position;
        /// <summary>Radio letal, en metros.</summary>
        public readonly float Radius;

        /// <summary>Crea el evento.</summary>
        public AntivirusSlam(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    // ===================================================================== Estación y progresión

    /// <summary>Se completó una estación. Publica <c>StationController</c>.</summary>
    public readonly struct StationCompleted : IGameEvent
    {
        /// <summary>Índice de la estación (0 = Primavera).</summary>
        public readonly int Station;
        /// <summary>Estrellas obtenidas (0–3).</summary>
        public readonly int Stars;
        /// <summary>Tiempo de la estación, en segundos.</summary>
        public readonly float TimeSeconds;

        /// <summary>Crea el evento.</summary>
        public StationCompleted(int station, int stars, float timeSeconds)
        {
            Station = station;
            Stars = stars;
            TimeSeconds = timeSeconds;
        }
    }

    /// <summary>Se aplicó una mejora permanente. Publica <c>ProgressionService</c>.</summary>
    public readonly struct UpgradeApplied : IGameEvent
    {
        /// <summary>Herramienta mejorada.</summary>
        public readonly ToolId Tool;
        /// <summary>Valor nuevo (alcance en metros o capacidad en objetos).</summary>
        public readonly float Value;

        /// <summary>Crea el evento.</summary>
        public UpgradeApplied(ToolId tool, float value)
        {
            Tool = tool;
            Value = value;
        }
    }

    /// <summary>Cambio del nivel de corrupción (Invierno). Publica <c>StationController</c>.</summary>
    public readonly struct CorruptionLevelChanged : IGameEvent
    {
        /// <summary>Nivel entre 0 y 1.</summary>
        public readonly float Level;

        /// <summary>Crea el evento.</summary>
        public CorruptionLevelChanged(float level) => Level = level;
    }

    // ===================================================================== Interfaz y tutorial

    /// <summary>
    /// Solicitud de una notificación breve en el HUD (por ejemplo, al regresar por el portal).
    /// </summary>
    /// <remarks>
    /// Se recomienda publicar cadenas constantes o almacenadas en campos para no generar basura.
    /// </remarks>
    public readonly struct HudMessageRequested : IGameEvent
    {
        /// <summary>Texto a mostrar (admite etiquetas de TextMeshPro).</summary>
        public readonly string Message;

        /// <summary>Crea el evento.</summary>
        public HudMessageRequested(string message) => Message = message;
    }

    /// <summary>El jugador cruzó un portal dimensional. Publica la puerta del Tutorial.</summary>
    public readonly struct DimensionalPortalCrossed : IGameEvent
    {
        /// <summary><c>true</c> al entrar a la dimensión enlazada; <c>false</c> al regresar.</summary>
        public readonly bool EnteredLinkedDimension;

        /// <summary>Crea el evento.</summary>
        public DimensionalPortalCrossed(bool enteredLinkedDimension) => EnteredLinkedDimension = enteredLinkedDimension;
    }

    // ===================================================================== Entorno (rama experimental Frutiger Aero)

    /// <summary>
    /// El jugador rebotó en una burbuja trampolín. Publica <c>BounceBubble</c>; lo escucha la
    /// presentación para deformar la burbuja y emitir partículas.
    /// </summary>
    public readonly struct BubbleBounced : IGameEvent
    {
        /// <summary>Burbuja que recibió el rebote.</summary>
        public readonly Transform Source;
        /// <summary>Punto de contacto aproximado (posición del jugador).</summary>
        public readonly Vector3 Position;
        /// <summary>Velocidad vertical aplicada, en m/s.</summary>
        public readonly float LaunchSpeed;

        /// <summary>Crea el evento.</summary>
        public BubbleBounced(Transform source, Vector3 position, float launchSpeed)
        {
            Source = source;
            Position = position;
            LaunchSpeed = launchSpeed;
        }
    }

    /// <summary>
    /// Un obstáculo dinámico (como las aspas giratorias de Verano) golpeó y empujó al jugador.
    /// Lo escucha la capa de presentación para disparar sonido y efectos sin acoplar ensamblados.
    /// </summary>
    public readonly struct HazardTriggered : IGameEvent
    {
        /// <summary>Transform del obstáculo que produjo el golpe.</summary>
        public readonly Transform Source;
        /// <summary>Punto de impacto aproximado en el mundo.</summary>
        public readonly Vector3 Position;

        /// <summary>Crea el evento.</summary>
        public HazardTriggered(Transform source, Vector3 position)
        {
            Source = source;
            Position = position;
        }
    }

    /// <summary>
    /// Se abrió una barrera del nivel tras derrotar a los enemigos requeridos (ej. el muro central de Verano).
    /// </summary>
    public readonly struct ArenaGateOpened : IGameEvent
    {
        /// <summary>Transform del controlador o barrera desbloqueada.</summary>
        public readonly Transform Source;
        /// <summary>Posición del muro o recompensa liberada.</summary>
        public readonly Vector3 Position;

        /// <summary>Crea el evento.</summary>
        public ArenaGateOpened(Transform source, Vector3 position)
        {
            Source = source;
            Position = position;
        }
    }

    /// <summary>
    /// El jugador entró al agua poco profunda desde el aire. Sirve para disparar salpicadura y sonido.
    /// </summary>
    public readonly struct ShallowWaterEntered : IGameEvent
    {
        /// <summary>Punto de entrada sobre la superficie del agua.</summary>
        public readonly Vector3 Position;
        /// <summary>Rapidez vertical previa al amortiguamiento, en m/s.</summary>
        public readonly float ImpactSpeed;

        /// <summary>Crea el evento.</summary>
        public ShallowWaterEntered(Vector3 position, float impactSpeed)
        {
            Position = position;
            ImpactSpeed = impactSpeed;
        }
    }

    /// <summary>
    /// Cambio de presencia del jugador dentro del volumen de agua poco profunda. Publica <c>WaterFallCushion</c>.
    /// </summary>
    public readonly struct ShallowWaterPresenceChanged : IGameEvent
    {
        /// <summary><c>true</c> mientras los pies del jugador están en el agua poco profunda.</summary>
        public readonly bool InWater;

        /// <summary>Crea el evento.</summary>
        public ShallowWaterPresenceChanged(bool inWater) => InWater = inWater;
    }

    // ===================================================================== Puertas entre niveles

    /// <summary>
    /// La escena del nivel siguiente terminó de cargarse en segundo plano y quedó dormida.
    /// Publica <c>LevelGate</c>; lo escucha la presentación para preparar la iluminación del
    /// otro nivel y la vista del portal.
    /// </summary>
    public readonly struct LevelGatePrepared : IGameEvent
    {
        /// <summary>Marco de la puerta en el nivel actual.</summary>
        public readonly Transform Gate;
        /// <summary>Marco de llegada en el nivel siguiente.</summary>
        public readonly Transform Exit;
        /// <summary>Escena del nivel actual.</summary>
        public readonly Scene Source;
        /// <summary>Escena del nivel siguiente.</summary>
        public readonly Scene Destination;
        /// <summary>Ancho y alto de la abertura, en metros.</summary>
        public readonly Vector2 Aperture;

        /// <summary>Crea el evento.</summary>
        public LevelGatePrepared(Transform gate, Transform exit, Scene source, Scene destination, Vector2 aperture)
        {
            Gate = gate;
            Exit = exit;
            Source = source;
            Destination = destination;
            Aperture = aperture;
        }
    }

    /// <summary>
    /// El nivel siguiente terminó de despertar (todos sus objetos activos) o volvió a dormir.
    /// La presentación solo dibuja la vista del portal mientras está despierto.
    /// </summary>
    public readonly struct LevelGateAwakeChanged : IGameEvent
    {
        /// <summary>Marco de la puerta.</summary>
        public readonly Transform Gate;
        /// <summary><c>true</c> si el nivel siguiente está despierto.</summary>
        public readonly bool Awake;

        /// <summary>Crea el evento.</summary>
        public LevelGateAwakeChanged(Transform gate, bool awake)
        {
            Gate = gate;
            Awake = awake;
        }
    }

    /// <summary>
    /// El jugador atravesó la puerta: ya está en el nivel siguiente, que es la escena activa.
    /// Lo escuchan la presentación (cambio de iluminación y disolución del nivel anterior) y la
    /// reaparición (nuevo punto de partida).
    /// </summary>
    public readonly struct LevelGateCrossed : IGameEvent
    {
        /// <summary>Marco de la puerta en el nivel anterior.</summary>
        public readonly Transform Gate;
        /// <summary>Marco de llegada en el nivel nuevo.</summary>
        public readonly Transform Exit;
        /// <summary>Escena del nivel anterior.</summary>
        public readonly Scene Source;
        /// <summary>Escena del nivel nuevo.</summary>
        public readonly Scene Destination;
        /// <summary>Punto de reaparición inicial del nivel nuevo.</summary>
        public readonly Vector3 SpawnPosition;
        /// <summary>Orientación de reaparición inicial.</summary>
        public readonly Quaternion SpawnRotation;
        /// <summary>Duración de la disolución del nivel anterior, en segundos.</summary>
        public readonly float FadeDuration;

        /// <summary>Crea el evento.</summary>
        public LevelGateCrossed(Transform gate, Transform exit, Scene source, Scene destination,
                                Vector3 spawnPosition, Quaternion spawnRotation, float fadeDuration)
        {
            Gate = gate;
            Exit = exit;
            Source = source;
            Destination = destination;
            SpawnPosition = spawnPosition;
            SpawnRotation = spawnRotation;
            FadeDuration = fadeDuration;
        }
    }

    /// <summary>
    /// La disolución terminó y el nivel anterior está a punto de descargarse. La presentación
    /// suelta en este momento las referencias a objetos de esa escena.
    /// </summary>
    public readonly struct LevelGateClosed : IGameEvent
    {
        /// <summary>Marco de la puerta en el nivel que se descarga.</summary>
        public readonly Transform Gate;

        /// <summary>Crea el evento.</summary>
        public LevelGateClosed(Transform gate) => Gate = gate;
    }

    // ===================================================================== Pantalla de carga entre niveles

    /// <summary>
    /// Empieza el cambio de nivel con pantalla de carga. Publica <c>LevelTransitionRunner</c>; la
    /// pantalla de carga se oscurece en <see cref="FadeOutDuration"/> segundos (tiempo real).
    /// </summary>
    public readonly struct LevelTransitionStarted : IGameEvent
    {
        /// <summary>Nombre de la escena que se va a cargar.</summary>
        public readonly string DestinationName;
        /// <summary>Segundos que tarda la pantalla en cubrir la imagen.</summary>
        public readonly float FadeOutDuration;

        /// <summary>Crea el evento.</summary>
        public LevelTransitionStarted(string destinationName, float fadeOutDuration)
        {
            DestinationName = destinationName;
            FadeOutDuration = fadeOutDuration;
        }
    }

    /// <summary>Avance de la carga entre 0 y 1. Publica <c>LevelTransitionRunner</c> cada frame.</summary>
    public readonly struct LevelTransitionProgress : IGameEvent
    {
        /// <summary>Avance entre 0 y 1.</summary>
        public readonly float Progress;

        /// <summary>Crea el evento.</summary>
        public LevelTransitionProgress(float progress) => Progress = progress;
    }

    /// <summary>
    /// El nivel nuevo ya está cargado y es la escena activa, el jugador está en su entrada y el
    /// nivel anterior se descargó, todo detrás de la pantalla de carga. La presentación aprovecha
    /// para precalentar los shaders del nivel y responde con <see cref="LevelTransitionWarmupDone"/>.
    /// </summary>
    public readonly struct LevelTransitionArrived : IGameEvent
    {
        /// <summary>Escena del nivel nuevo.</summary>
        public readonly Scene Destination;

        /// <summary>Crea el evento.</summary>
        public LevelTransitionArrived(Scene destination) => Destination = destination;
    }

    /// <summary>La presentación terminó de precalentar los shaders del nivel nuevo.</summary>
    public readonly struct LevelTransitionWarmupDone : IGameEvent { }

    /// <summary>
    /// Termina el cambio de nivel: la pantalla de carga se aclara en <see cref="FadeInDuration"/>
    /// segundos y el jugador recupera el control. Publica <c>LevelTransitionRunner</c>.
    /// </summary>
    public readonly struct LevelTransitionFinished : IGameEvent
    {
        /// <summary>Segundos que tarda la pantalla en desaparecer.</summary>
        public readonly float FadeInDuration;

        /// <summary>Crea el evento.</summary>
        public LevelTransitionFinished(float fadeInDuration) => FadeInDuration = fadeInDuration;
    }

    /// <summary>
    /// Modo de funcionamiento para un volumen de aviso o guía (<c>PromptZone</c>).
    /// </summary>
    public enum PromptZoneMode
    {
        /// <summary>Muestra el aviso al entrar a la zona.</summary>
        Activate = 0,
        /// <summary>Oculta / descarta el aviso activo al entrar.</summary>
        Complete = 1,
        /// <summary>Muestra el aviso al entrar y lo oculta automáticamente al salir de la zona.</summary>
        ActivateAndCompleteOnExit = 2
    }

    /// <summary>
    /// Evento publicado al entrar o salir de una zona de aviso (<c>PromptZone</c>).
    /// Permite mostrar avisos, descripciones y controles dinámicos en cualquier nivel.
    /// </summary>
    public readonly struct PromptTriggered : IGameEvent
    {
        /// <summary>Título o encabezado principal del aviso.</summary>
        public readonly string Title;
        /// <summary>Descripción detallada o instrucción secundaria.</summary>
        public readonly string Description;
        /// <summary>Controles o teclas recomendadas.</summary>
        public readonly string Controls;
        /// <summary>Modo de activación del volumen.</summary>
        public readonly PromptZoneMode Mode;
        /// <summary><c>true</c> si el volumen solicita ocultar el aviso activo.</summary>
        public readonly bool Hide;

        public PromptTriggered(string title, string description = null, string controls = null, PromptZoneMode mode = PromptZoneMode.Activate, bool hide = false)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Controls = controls ?? string.Empty;
            Mode = mode;
            Hide = hide;
        }
    }

    /// <summary>
    /// Evento publicado al entrar o salir de un volumen de tutorial (<c>TutorialPromptZone</c>).
    /// </summary>
    [System.Obsolete("Usa PromptTriggered.")]
    public readonly struct TutorialStepTriggered : IGameEvent
    {
        /// <summary>Índice del paso del tutorial (0 a 6).</summary>
        public readonly int StepIndex;
        /// <summary><c>true</c> si el volumen marca el paso como completado en lugar de activarlo.</summary>
        public readonly bool MarkCompleted;
        /// <summary>Título personalizado opcional para la guía.</summary>
        public readonly string CustomTitle;
        /// <summary>Controles personalizados opcionales para la guía.</summary>
        public readonly string CustomControls;

        public TutorialStepTriggered(int stepIndex, bool markCompleted = false, string customTitle = null, string customControls = null)
        {
            StepIndex = stepIndex;
            MarkCompleted = markCompleted;
            CustomTitle = customTitle ?? string.Empty;
            CustomControls = customControls ?? string.Empty;
        }
    }

    // ===================================================================== Progreso

    /// <summary>
    /// Se terminó el juego (meta final): la pantalla de resultados lo muestra. Publica
    /// <c>LevelProgressService</c>. Cruzar la puerta del Tutorial no lo publica.
    /// </summary>
    public readonly struct LevelCompleted : IGameEvent
    {
        /// <summary>Nombre del nivel que ve el jugador.</summary>
        public readonly string Title;
        /// <summary>Tiempo que tomó terminar este nivel, en segundos.</summary>
        public readonly float LevelSeconds;
        /// <summary>Tiempo de la partida completa desde el primer nivel; negativo si no aplica.</summary>
        public readonly float RunSeconds;
        /// <summary>Estrellas recogidas en todo el juego.</summary>
        public readonly int GameStars;
        /// <summary>Estrellas que hay en todo el juego.</summary>
        public readonly int GameTotalStars;

        public LevelCompleted(string title, float levelSeconds, float runSeconds, int gameStars, int gameTotalStars)
        {
            Title = title;
            LevelSeconds = levelSeconds;
            RunSeconds = runSeconds;
            GameStars = gameStars;
            GameTotalStars = gameTotalStars;
        }

        /// <summary><c>true</c> si se jugó la partida completa desde el primer nivel.</summary>
        public bool HasRunTime => RunSeconds >= 0f;
    }

    /// <summary>
    /// El jugador pulsó «Reaparecer» en la pantalla de muerte. Lo atiende <c>RespawnService</c>.
    /// </summary>
    public readonly struct RespawnRequested : IGameEvent { }
}

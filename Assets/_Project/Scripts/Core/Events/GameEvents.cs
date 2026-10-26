// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: GameEvents.cs
// Propósito: catálogo de eventos del juego (TDD §4.2, tarea F1-A01).
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

    // ===================================================================== Enemigos (Fase 2)

    /// <summary>Apareció un enemigo. Publica <c>EnemyBase</c>.</summary>
    public readonly struct EnemySpawned : IGameEvent
    {
        /// <summary>Tipo de enemigo.</summary>
        public readonly EnemyKind Kind;
        /// <summary>Posición de aparición.</summary>
        public readonly Vector3 Position;

        /// <summary>Crea el evento.</summary>
        public EnemySpawned(EnemyKind kind, Vector3 position)
        {
            Kind = kind;
            Position = position;
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
}

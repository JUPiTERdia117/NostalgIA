// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: GameEnums.cs
// Propósito: enumeraciones compartidas por la carga útil de los eventos y por las
// interfaces de estado (TDD §3.2, §4.2, §5, §6 y §7).
// -----------------------------------------------------------------------------

namespace NostalgIA.Core.Events
{
    /// <summary>Estados globales de la partida (TDD §3.2).</summary>
    public enum GameState
    {
        /// <summary>Arranque: servicios en inicialización.</summary>
        Boot,
        /// <summary>Menú principal.</summary>
        MainMenu,
        /// <summary>Selección de estación.</summary>
        StationSelect,
        /// <summary>Carga asíncrona de una escena.</summary>
        Loading,
        /// <summary>Juego en curso.</summary>
        Playing,
        /// <summary>Pausa: escala de tiempo cero y mapa de interfaz.</summary>
        Paused,
        /// <summary>Secuencia de muerte y reaparición (≈ 0,8 s).</summary>
        Respawning,
        /// <summary>Pantalla de resultados de la estación.</summary>
        Results
    }

    /// <summary>Esquema de control activo (TDD §7.4).</summary>
    public enum InputScheme
    {
        /// <summary>Teclado y ratón.</summary>
        KeyboardMouse,
        /// <summary>Mando físico (Xbox u otro compatible).</summary>
        Gamepad,
        /// <summary>Pantalla táctil y sus controles virtuales.</summary>
        Touch
    }

    /// <summary>Estados de la máquina de locomoción del jugador (TDD §5.1).</summary>
    public enum LocomotionState
    {
        /// <summary>En el suelo: caminar o correr.</summary>
        Grounded,
        /// <summary>Agachado: cápsula y cámara reducidas.</summary>
        Crouching,
        /// <summary>Deslizándose con inercia.</summary>
        Sliding,
        /// <summary>En el aire: salto o caída.</summary>
        Airborne,
        /// <summary>Remolcado por el gancho.</summary>
        Grappling
    }

    /// <summary>Herramientas intercambiables del dron (GDD 2.3).</summary>
    public enum ToolId
    {
        /// <summary>Aspiradora Lanzadora.</summary>
        Vacuum,
        /// <summary>Gancho.</summary>
        Grapple
    }

    /// <summary>Tipo de objetivo bajo la retícula (TDD §6.2).</summary>
    public enum AimTargetKind
    {
        /// <summary>Nada utilizable por la herramienta activa.</summary>
        None,
        /// <summary>Objeto absorbible al alcance de la aspiradora.</summary>
        Absorbable,
        /// <summary>Superficie de anclaje al alcance del gancho.</summary>
        Anchor,
        /// <summary>Objetivo válido, pero fuera del alcance de la herramienta activa.</summary>
        OutOfRange
    }

    /// <summary>Tipo de daño (TDD §5.2).</summary>
    public enum DamageType
    {
        /// <summary>Resta corazones y respeta la invulnerabilidad temporal.</summary>
        Normal,
        /// <summary>Muerte inmediata; ignora corazones e invulnerabilidad.</summary>
        Instakill
    }

    /// <summary>Causa de muerte del jugador (TDD §4.2, evento <c>PlayerDied</c>).</summary>
    public enum DeathCause
    {
        /// <summary>Se agotaron los corazones.</summary>
        Hearts,
        /// <summary>Caída al vacío o desde una altura extrema.</summary>
        Void,
        /// <summary>Contacto con un peligro de muerte instantánea.</summary>
        Instakill
    }

    /// <summary>Tipos de enemigo del bestiario (GDD 3.3).</summary>
    public enum EnemyKind
    {
        /// <summary>Correo de Spam: atacante a distancia.</summary>
        SpamMail,
        /// <summary>Bug: hostigador terrestre.</summary>
        Bug,
        /// <summary>Troyano: generador de enemigos.</summary>
        Trojan,
        /// <summary>Antivirus: amenaza de muerte instantánea.</summary>
        Antivirus
    }
}

// -----------------------------------------------------------------------------
// NostalgIA — Entrada (NostalgIA.Input)
// Archivo: InputActionNames.cs
// Propósito: nombres de mapas y acciones del asset InputSystem_Actions
// (TDD §7.1, tarea F1-A02).
// -----------------------------------------------------------------------------

namespace NostalgIA.Input
{
    /// <summary>
    /// Nombres de los mapas, acciones y esquemas del asset de acciones.
    /// </summary>
    /// <remarks>
    /// Las cadenas se usan una sola vez, al inicializar <see cref="InputReader"/>, para obtener
    /// y cachear las referencias a cada <c>InputAction</c>. Durante el juego solo se trabaja con
    /// esas referencias. Centralizarlas evita errores tipográficos y permite que las pruebas
    /// comprueben que el asset contiene exactamente estas acciones.
    /// </remarks>
    public static class InputActionNames
    {
        /// <summary>Mapa de juego.</summary>
        public const string GameplayMap = "Gameplay";

        /// <summary>Mapa de interfaz (lo usa <c>InputSystemUIInputModule</c>).</summary>
        public const string UIMap = "UI";

        /// <summary>Movimiento (Vector2).</summary>
        public const string Move = "Move";
        /// <summary>Mirada por delta de ratón (Vector2, <i>pass-through</i>).</summary>
        public const string LookDelta = "LookDelta";
        /// <summary>Mirada por stick derecho (Vector2).</summary>
        public const string LookStick = "LookStick";
        /// <summary>Salto.</summary>
        public const string Jump = "Jump";
        /// <summary>Correr (mantener).</summary>
        public const string Sprint = "Sprint";
        /// <summary>Agacharse; deslizarse si se corre.</summary>
        public const string Crouch = "Crouch";
        /// <summary>Cambiar de herramienta.</summary>
        public const string SwitchTool = "SwitchTool";
        /// <summary>Absorber con la aspiradora (mantener).</summary>
        public const string Absorb = "Absorb";
        /// <summary>Disparar un objeto almacenado.</summary>
        public const string Fire = "Fire";
        /// <summary>Disparar o cancelar el gancho.</summary>
        public const string Grapple = "Grapple";
        /// <summary>Pausa.</summary>
        public const string Pause = "Pause";

        /// <summary>Acciones del mapa de juego, en el orden del asset.</summary>
        public static readonly string[] GameplayActions =
        {
            Move, LookDelta, LookStick, Jump, Sprint, Crouch, SwitchTool, Absorb, Fire, Grapple, Pause
        };

        /// <summary>Acciones del mapa de interfaz.</summary>
        public static readonly string[] UIActions =
        {
            "Navigate", "Submit", "Cancel", "Point", "Click", "ScrollWheel"
        };

        /// <summary>Esquemas de control conservados (TDD §7.1).</summary>
        public static readonly string[] ControlSchemes = { "KeyboardMouse", "Gamepad", "Touch" };
    }
}

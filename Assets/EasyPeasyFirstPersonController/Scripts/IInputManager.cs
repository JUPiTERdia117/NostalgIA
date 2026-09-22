using UnityEngine;

namespace EasyPeasyFirstPersonController
{
    // Optional contract for inputs that supply a sustained stick value instead of a mouse delta.
    public interface IAnalogLookInputManager
    {
        bool UsesAnalogLook { get; }
    }

    public interface IInputManager
    {
        Vector2 moveInput { get; }
        Vector2 lookInput { get; }
        bool jump { get; }
        bool sprint { get; }
        bool crouch { get; }
        bool slide { get; }
    }
}
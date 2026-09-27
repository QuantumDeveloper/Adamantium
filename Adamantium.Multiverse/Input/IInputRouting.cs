namespace Adamantium.Multiverse.Input;

/// <summary>
/// Which output's input the universe's actions read: keys and gamepads come from the output that has the keyboard,
/// the mouse from the one under the pointer.
/// </summary>
public interface IInputRouting
{
    /// <summary>The input of the output that has the keyboard, or null.</summary>
    InputWormhole KeyboardInput { get; }

    /// <summary>The input of the output under the pointer, or null.</summary>
    InputWormhole PointerInput { get; }
}

namespace Adamantium.Multiverse.Input;

/// <summary>
/// What an action's value is.
/// </summary>
public enum InputActionType
{
    /// <summary>Down or up; an analog control counts as down past half its travel.</summary>
    Button,

    /// <summary>One number: -1..1 from bounded controls, unbounded from mouse motion and wheel.</summary>
    Axis,

    /// <summary>Two numbers, X to the right and Y up.</summary>
    Vector
}

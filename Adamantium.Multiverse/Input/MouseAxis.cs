namespace Adamantium.Multiverse.Input;

/// <summary>
/// The mouse's motion and wheel, as axes. Their values have no bounds: the motion is in the raw units the platform
/// reports, the wheel in notches.
/// </summary>
public enum MouseAxis
{
    DeltaX,
    DeltaY,
    Wheel
}

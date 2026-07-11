namespace Adamantium.UI.Core.Media.Animation;

/// <summary>What a finished animation leaves behind (WPF's FillBehavior). <see cref="HoldEnd"/> keeps the final value
/// applied at Animation priority (a flip stays flipped). <see cref="Stop"/> CLEARS the Animation-priority value on
/// completion, releasing the property back to its underlying value AND to direct sets - without it, a finished
/// ease-back animation held the property forever and masked every later local write (a tilt that worked exactly once).</summary>
public enum FillBehavior
{
    HoldEnd,
    Stop
}

using Adamantium.Mathematics;

namespace Adamantium.Multiverse.Input;

public record struct MouseInput
{
    public MouseButton Button;

    public InputType InputType;

    public int WheelDelta;

    public Vector2F Delta;

    /// <summary>For a press, how many clicks in a row it makes, as the host counts them: 2 for a double click.</summary>
    public int ClickCount;
}

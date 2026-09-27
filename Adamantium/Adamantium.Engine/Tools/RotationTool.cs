namespace Adamantium.Engine.Tools;

/// <summary>
/// Turns the selected entity about its pivot: about an axis, about the view, or freely.
/// </summary>
public class RotationTool : ToolProcessor
{
    public RotationTool()
        : base(new RotationHandles())
    {
    }
}

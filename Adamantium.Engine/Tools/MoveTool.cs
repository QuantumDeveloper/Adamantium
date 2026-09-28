namespace Adamantium.Engine.Tools;

/// <summary>
/// Moves the selected entity along an axis, across a plane, or across the view.
/// </summary>
public class MoveTool : ToolProcessor
{
    public MoveTool()
        : base(new MoveHandles())
    {
    }
}

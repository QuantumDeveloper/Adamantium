namespace Adamantium.Engine.Tools;

/// <summary>
/// Scales the selected entity about its pivot, along its own axes or evenly.
/// </summary>
public class ScaleTool : ToolProcessor
{
    public ScaleTool()
        : base(new ScaleHandles())
    {
    }
}

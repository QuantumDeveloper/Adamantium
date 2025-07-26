namespace Adamantium.UI.Core.Graphics;

public interface IDrawingContext
{
    IDrawingSession ForControl(IUIComponent component);
}

internal interface IDrawingContextInternal
{
    void Clear();
    IReadOnlyList<IDrawCommand> GetDrawCommands();
}
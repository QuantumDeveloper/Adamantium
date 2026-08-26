namespace Adamantium.UI.Core.Graphics;

public interface IDrawingContext
{
    IDrawingSession ForControl(IUIComponent component);
}

internal interface IDrawingContextInternal
{
    void Clear();
    IReadOnlyList<IDrawCommand> GetDrawCommands();

    /// <summary>A new RECORD frame begins - drop anything memoised for the previous one. <see cref="Clear"/> is per
    /// COMPONENT (it empties the command list before each one renders), so it is not the place for frame-scoped state.</summary>
    void BeginRecordFrame();
}
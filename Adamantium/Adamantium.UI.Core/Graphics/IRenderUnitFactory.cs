namespace Adamantium.UI.Core.Graphics;

public interface IRenderUnitFactory
{
    void RegisterFactory<T>(Func<IDrawCommand, IRenderUnit> factory);
    
    IRenderUnit CreateRenderUnitFromCommand(IDrawCommand command);
}
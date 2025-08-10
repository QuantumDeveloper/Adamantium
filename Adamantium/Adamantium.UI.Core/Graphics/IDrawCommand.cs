namespace Adamantium.UI.Core.Graphics;

public interface IDrawCommand
{
    IUIComponent Component { get; }
    Guid Id { get; }
    
    RenderData RenderData { get; }
    
    Object Payload { get; }
}
namespace Adamantium.UI.Core.Graphics;

public interface IDrawCommand
{
    Guid Id { get; }
    
    RenderData RenderData { get; }
    
    Object Payload { get; }
}
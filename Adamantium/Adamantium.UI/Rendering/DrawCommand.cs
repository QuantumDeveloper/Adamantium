using System;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Rendering;

public class DrawCommand : IDrawCommand
{
    public DrawCommand(Guid id, Object payload, RenderData renderData)
    {
        Id = id;
        Payload = payload;
        RenderData = renderData;
    }
    
    public Guid Id { get; }
    public object Payload { get; }
    
    public RenderData RenderData { get; }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Payload, RenderData);
    }
}
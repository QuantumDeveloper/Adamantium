using System;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Rendering;

public class DrawCommand : IDrawCommand
{
    public DrawCommand(IUIComponent component, Guid id, Object payload, RenderData renderData)
    {
        Component = component;
        Id = id;
        Payload = payload;
        RenderData = renderData;
    }

    public IUIComponent Component { get; }
    public Guid Id { get; }
    public object Payload { get; }
    
    public RenderData RenderData { get; }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Payload, RenderData);
    }
}
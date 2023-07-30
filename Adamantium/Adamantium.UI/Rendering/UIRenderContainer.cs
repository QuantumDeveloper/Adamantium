using System.Collections.Generic;
using Adamantium.Engine.Graphics;

namespace Adamantium.UI.Rendering;

internal class UIRenderContainer
{
    public List<RenderUnit> ChildUnits { get; }

    public UIRenderContainer()
    {
        ChildUnits = new List<RenderUnit>();
    }

    public void AddItem(RenderUnit item)
    {
        ChildUnits.Add(item);
    }
        
    public void DisposeAndClearItems()
    {
        for (int i = 0; i < ChildUnits.Count; i++)
        {
            ChildUnits[i].Dispose();
        }
        ChildUnits.Clear();
    }

    public void Draw(GraphicsDevice device, IUIComponent component, Matrix4x4F projectionMatrix)
    {
        foreach (var renderUnit in ChildUnits)
        {
            renderUnit.Draw(device, component, projectionMatrix);
        }
    }
}
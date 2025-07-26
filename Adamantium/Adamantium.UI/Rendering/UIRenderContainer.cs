using System.Collections.Generic;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Rendering;

public class UIRenderContainer
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

    public void Draw(IGraphicsDevice device, IUIComponent component, Matrix4x4F projectionMatrix)
    {
        foreach (var renderUnit in ChildUnits)
        {
            renderUnit.Draw(device, component, projectionMatrix);
        }
    }
}
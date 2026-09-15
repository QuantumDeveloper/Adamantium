using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Controls;

/// <summary>Draws the items of an <see cref="InfiniteCanvas"/> that belong IN FRONT of its hosted controls - the
/// <see cref="CanvasBand.Over"/> band.
/// <para>It exists because a layer is ONE place in paint order. A control on the plane has to be a real child of a
/// layer to be laid out, drawn and clicked at all, and everything the canvas draws itself is therefore behind every
/// one of them - so the only way for an item to be in front of a control is for something standing in front of that
/// control to draw it. This is that something, and it sits after the element layer in the template.</para>
/// <para>It draws and nothing else: it takes no part in hit-testing, so what is under it stays clickable. An arrow
/// pointing at a node must not be the thing the pointer finds instead of the node.</para></summary>
public class CanvasFrontLayer : MeasurableUIComponent
{
    /// <summary>The canvas whose items this draws. Written by the canvas when it takes the part.</summary>
    public InfiniteCanvas Owner { get; set; }

    public CanvasFrontLayer()
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(IDrawingContext context)
    {
        base.OnRender(context);

        if (Owner == null) return;

        Owner.DrawBand(context.ForControl(this), CanvasBand.Over);
    }
}

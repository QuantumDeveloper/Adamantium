using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>THE GLASS: what a gesture is making and what is said ABOUT the drawing, both over everything on the plane.
/// <para>What is ON the plane is drawn by the stack of layers cut from the scene's own order - see
/// <see cref="CanvasDrawLayer"/>. What is not on it yet is the half-made thing under the pen, and it belongs here
/// because that is where it is going: a new thing is put at the top of the order, so a gesture showing itself
/// underneath would show one thing and mean another.</para>
/// <para>The frame round what is selected is NOT drawn here as a rule: it stands where its object stands, drawn by the
/// layer that holds it, because where a thing is in the order is the one question somebody moving it is asking. It
/// falls to the glass only when nothing on the plane is drawn over what is held. The pointer's own marks - the snap
/// mark, the plate saying where it is - are always here: they say where the hand is, not where anything stands.</para>
/// <para>It draws and nothing else: it takes no part in hit-testing, so what is under it stays clickable.</para>
/// </summary>
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

        var session = context.ForControl(this);

        Owner.DrawInProgress(session);

        // The frame only reaches here when nothing on the plane is drawn over what is held - otherwise it is drawn by
        // the layer that holds it, which is what puts it in the order with its object.
        if (Owner.ChromeGoesOnGlass) Owner.DrawChrome(session);

        // The pointer's own marks always: they say where the HAND is, not where anything stands, and a plate read
        // through the drawing it is measuring is no plate at all.
        Owner.DrawPointer(session);
    }
}

using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Controls;

/// <summary>A real CONTROL on the plane - a button, a text box, a panel of them - at a place and a size in the world.
/// <para>The bridge the other items are not: a control has a template, takes input and edits itself, and none of that
/// has to be rebuilt here to put one on a canvas. It costs a node in the visual tree and a property store, which is
/// exactly why ink is NOT one of these - a drawing holds tens of thousands of strokes and a board holds dozens of
/// controls.</para>
/// <para>Zoom costs it nothing: it is laid out once per size change, and panning is a new arrange of a rectangle, not a
/// re-measure of what is inside it.</para></summary>
public class ElementItem : ICanvasItem
{
    public ElementItem(IUIComponent element, Rect world)
    {
        Element = element;
        World = world;
    }

    /// <summary>The control itself. It lives in the canvas's own visual tree while it is on screen, so it draws, takes
    /// input and animates the way it would anywhere else.</summary>
    public IUIComponent Element { get; }

    /// <summary>Where it sits on the plane, in WORLD units - so it grows with the zoom like everything else drawn on the
    /// canvas, rather than staying a fixed number of pixels the way the grips do.</summary>
    public Rect World { get; set; }

    public Rect Bounds => World;

    public void Move(Vector2 worldDelta) =>
        World = new Rect(World.X + worldDelta.X, World.Y + worldDelta.Y, World.Width, World.Height);

    public void Resize(Rect world)
    {
        if (world.Width <= 0 || world.Height <= 0) return;

        World = world;
    }

    public bool HitTest(Vector2 world, double tolerance) =>
        world.X >= World.X - tolerance && world.X <= World.X + World.Width + tolerance &&
        world.Y >= World.Y - tolerance && world.Y <= World.Y + World.Height + tolerance;

    /// <summary>Nothing: a control draws ITSELF, from its own template, as a child of the canvas. Everything else on the
    /// plane is data and has to be painted here; this one is the case that is not.</summary>
    public void Render(IDrawingSession session, InfiniteCanvas canvas)
    {
    }
}

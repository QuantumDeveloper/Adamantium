using System;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Text;
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

    /// <summary>Where and how big, one number at a time. <see cref="World"/> is a rectangle and a rectangle cannot be
    /// half-written, so an inspector line that edits only the X of one has nothing to bind to - these are that line.
    /// </summary>
    public Double X
    {
        get => World.X;
        set => World = new Rect(value, World.Y, World.Width, World.Height);
    }

    public Double Y
    {
        get => World.Y;
        set => World = new Rect(World.X, value, World.Width, World.Height);
    }

    public Double Width
    {
        get => World.Width;
        set => World = new Rect(World.X, World.Y, Math.Max(1, value), World.Height);
    }

    public Double Height
    {
        get => World.Height;
        set => World = new Rect(World.X, World.Y, World.Width, Math.Max(1, value));
    }

    /// <summary>How round each of the control's corners is, one at a time, and how thick its border is - one number for
    /// all four sides there, because a border of three different widths is not what anybody is reaching for on a plane.
    /// <para>Here for the same reason <see cref="X"/> is: a corner radius and a thickness are STRUCTS, and a struct
    /// cannot be written half at a time, so an inspector line editing one corner has nothing to bind to.</para>
    /// <para>Zero for a control that has no such property at all - a panel has no border to thicken.</para></summary>
    public Double CornerTopLeft
    {
        get => Element is Control control ? control.CornerRadius.TopLeft : 0;
        set => SetCorner(value, Element is Control c ? c.CornerRadius : default, 0);
    }

    public Double CornerTopRight
    {
        get => Element is Control control ? control.CornerRadius.TopRight : 0;
        set => SetCorner(value, Element is Control c ? c.CornerRadius : default, 1);
    }

    public Double CornerBottomRight
    {
        get => Element is Control control ? control.CornerRadius.BottomRight : 0;
        set => SetCorner(value, Element is Control c ? c.CornerRadius : default, 2);
    }

    public Double CornerBottomLeft
    {
        get => Element is Control control ? control.CornerRadius.BottomLeft : 0;
        set => SetCorner(value, Element is Control c ? c.CornerRadius : default, 3);
    }

    public Double BorderWidth
    {
        get => Element is Control control ? control.BorderThickness.Left : 0;
        set
        {
            if (Element is Control control) control.BorderThickness = new Thickness(Math.Max(0, value));
        }
    }

    /// <summary>What the control SAYS - its content if it has content, its text if it is a box.
    /// <para>Here rather than "inspect the control itself", because the thing selected on the plane is this item, and an
    /// inspector line binds against what is selected. A control that says nothing answers with nothing and takes
    /// nothing: a panel has no label and pretending it has one would only offer a line that does not work.</para>
    /// </summary>
    public String Label
    {
        get => Element switch
        {
            TextBox box => box.Text,
            IContentControl content => content.Content?.ToString(),
            _ => null
        };
        set
        {
            switch (Element)
            {
                case TextBox box:
                    box.Text = value;
                    break;

                case IContentControl content:
                    content.Content = value;
                    break;
            }
        }
    }

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

    private void SetCorner(double value, CornerRadius current, int corner)
    {
        if (Element is not Control control) return;

        value = Math.Max(0, value);
        control.CornerRadius = corner switch
        {
            0 => new CornerRadius(value, current.TopRight, current.BottomRight, current.BottomLeft),
            1 => new CornerRadius(current.TopLeft, value, current.BottomRight, current.BottomLeft),
            2 => new CornerRadius(current.TopLeft, current.TopRight, value, current.BottomLeft),
            _ => new CornerRadius(current.TopLeft, current.TopRight, current.BottomRight, value)
        };
    }
}

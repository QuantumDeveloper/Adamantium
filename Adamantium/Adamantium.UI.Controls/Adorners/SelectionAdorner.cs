using System;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Adorners;

/// <summary>
/// A decorated selection frame: an outlined rectangle just INSIDE the adorned element's painted bounds plus small square
/// handles tucked into the four corners. Drawn by the adorner stage on top of everything - this replaces the designer's
/// own host-side frame. The chrome is kept INSIDE the bounds on purpose: the adorner shares the window's framebuffer, so
/// a frame drawn OUTSIDE an edge-touching element's bounds would fall off-window and be clipped away (only stray corner
/// bits survive) - exactly the "no frame, 4 short corner marks" the designer showed for stretched/edge elements.
/// </summary>
public class SelectionAdorner : Adorner
{
    private const double FrameThickness = 4;
    private const double HandleSize = 6.0;    // corner handle square (side length)

    public SelectionAdorner(UIComponent adornedElement) : base(adornedElement)
    {
    }

    /// <summary>Frame + handle outline colour. Default a designer blue.</summary>
    public Brush Stroke { get; set; } = new SolidColorBrush(Colors.CornflowerBlue);

    /// <summary>Corner handle fill. Default white.</summary>
    public Brush HandleFill { get; set; } = new SolidColorBrush(Colors.White);

    protected override void OnRender(IDrawingContext context)
    {
        // Inset the frame by half the stroke so the whole 4px line sits within the bounds (never clipped at a window edge).
        var b = AdornedBounds;
        var half = FrameThickness / 2.0;
        var frame = new Rect(b.X + half, b.Y + half,
            Math.Max(0, b.Width - FrameThickness), Math.Max(0, b.Height - FrameThickness));
        var session = context.ForControl(this);
        var pen = new Pen(Stroke, FrameThickness);

        // Outlined frame (transparent fill = outline only).
        session.DrawRectangle(Brushes.Transparent, frame, pen);

        // Corner handles: small filled squares tucked INTO each inner corner of the frame (so they also stay in bounds).
        var hs = HandleSize;
        var handlePen = new Pen(Stroke, 1.0);
        Rect[] handles =
        [
            new Rect(frame.X, frame.Y, hs, hs),
            new Rect(frame.X + frame.Width - hs, frame.Y, hs, hs),
            new Rect(frame.X, frame.Y + frame.Height - hs, hs, hs),
            new Rect(frame.X + frame.Width - hs, frame.Y + frame.Height - hs, hs, hs)
        ];
        foreach (var h in handles)
            session.DrawRectangle(HandleFill, h, handlePen);
    }
}

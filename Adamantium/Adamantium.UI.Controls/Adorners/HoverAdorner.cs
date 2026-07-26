using System;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Adorners;

/// <summary>
/// A lightweight hover highlight: a thin outlined rectangle just inside the hovered element's painted bounds, with no
/// corner handles - the transient "what's under the cursor" hint, distinct from the persistent
/// <see cref="SelectionAdorner"/> (frame + handles). Drawn by the framework adorner stage so the designer's hover frame
/// is stroke-aware like the selection. Kept INSIDE the bounds so it isn't clipped on an element that touches the window edge.
/// </summary>
public class HoverAdorner : Adorner
{
    private const double FrameThickness = 3.0;

    public HoverAdorner(UIComponent adornedElement) : base(adornedElement)
    {
    }

    /// <summary>Frame outline colour. Default a bright designer blue, distinct from the selection frame.</summary>
    public Brush Stroke { get; set; } = new SolidColorBrush(Colors.DodgerBlue);

    // The theme's HoverAdorner template wraps the whole element; the stage sizes it to the adorned bounds.
    public override bool FillsAdornedBounds => true;

    protected override void OnRender(IDrawingContext context)
    {
        if (Template != null) return;   // a themed template draws the frame - this OnRender is the no-theme fallback

        // Inset by half the stroke so the whole line stays within the bounds (and thus within the window framebuffer).
        var b = AdornedBounds;
        var half = FrameThickness / 2.0;
        var frame = new Rect(b.X + half, b.Y + half,
            Math.Max(0, b.Width - FrameThickness), Math.Max(0, b.Height - FrameThickness));
        context.ForControl(this).DrawRectangle(Brushes.Transparent, frame, new Pen(Stroke, FrameThickness));
    }
}

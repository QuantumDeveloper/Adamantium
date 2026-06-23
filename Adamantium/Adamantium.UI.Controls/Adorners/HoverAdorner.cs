using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Adorners;

/// <summary>
/// A lightweight hover highlight: a thin outlined rectangle just outside the hovered element's painted bounds, with no
/// corner handles - the transient "what's under the cursor" hint, distinct from the persistent
/// <see cref="SelectionAdorner"/> (frame + handles). Drawn by the framework adorner stage so the designer's hover frame
/// is stroke-aware like the selection, instead of a host-side rectangle.
/// </summary>
public class HoverAdorner : Adorner
{
    private const double Margin = 2.0;        // gap between the element's painted bounds and the frame
    private const double FrameThickness = 3.0;

    public HoverAdorner(UIComponent adornedElement) : base(adornedElement)
    {
    }

    /// <summary>Frame outline colour. Default a bright designer blue, distinct from the selection frame.</summary>
    public Brush Stroke { get; set; } = new SolidColorBrush(Colors.DodgerBlue);

    protected override void OnRender(IDrawingContext context)
    {
        var frame = AdornedBounds.Inflate(Margin);
        context.ForControl(this).DrawRectangle(Brushes.Transparent, frame, new Pen(Stroke, FrameThickness));
    }
}

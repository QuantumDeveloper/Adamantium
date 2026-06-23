using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Adorners;

/// <summary>
/// A decorated selection frame: an outlined rectangle a few pixels OUTSIDE the adorned element's painted bounds (so a
/// stroke's edge/AA never touches the line) plus small square handles at the four corners. Drawn by the adorner stage
/// on top of everything - this replaces the designer's own host-side frame.
/// </summary>
public class SelectionAdorner : Adorner
{
    private const double Margin = 2.0;       // gap between the element's painted bounds and the frame
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
        var frame = AdornedBounds.Inflate(Margin);
        var session = context.ForControl(this);
        var pen = new Pen(Stroke, FrameThickness);

        // Outlined frame (transparent fill = outline only).
        session.DrawRectangle(Brushes.Transparent, frame, pen);

        // Corner handles: small filled squares centred on each frame corner.
        var half = HandleSize / 2.0;
        var handlePen = new Pen(Stroke, 1.0);
        Vector2[] corners =
        [
            new Vector2(frame.X, frame.Y),
            new Vector2(frame.X + frame.Width, frame.Y),
            new Vector2(frame.X, frame.Y + frame.Height),
            new Vector2(frame.X + frame.Width, frame.Y + frame.Height)
        ];
        foreach (var c in corners)
            session.DrawRectangle(HandleFill, new Rect(c.X - half, c.Y - half, HandleSize, HandleSize), handlePen);
    }
}

using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core.Media;

namespace Adamantium.UITests.Graph;

/// <summary>What an application says a shape on the plane IS - the description the canvas draws from.</summary>
public class PlacedShape : ICanvasShapeDescription
{
    public PlacedShape(CanvasShape shape) => Shape = shape;

    public CanvasShape Shape { get; }

    public Brush Fill { get; set; }

    public Brush Stroke { get; set; } = Brushes.White;

    public double Thickness { get; set; } = 1;

    public CornerRadius Corner { get; set; }

    public int Sides { get; set; } = 6;

    public CanvasArrowHead StartHead { get; set; } = CanvasArrowHead.None;

    public CanvasArrowHead EndHead { get; set; } = CanvasArrowHead.None;
}

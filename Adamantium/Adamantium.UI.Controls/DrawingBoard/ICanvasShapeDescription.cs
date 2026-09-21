using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>A SHAPE, described. What an application says when it wants the canvas's own drawing rather than a control
/// of its own: which shape, what it is painted with, and the few numbers that particular shapes have.</summary>
public interface ICanvasShapeDescription
{
    CanvasShape Shape { get; }

    Brush Fill { get; }

    Brush Stroke { get; }

    double Thickness { get; }

    /// <summary>Rounded corners, for the shapes that have corners.</summary>
    CornerRadius Corner { get; }

    /// <summary>How many sides a polygon has.</summary>
    int Sides { get; }

    /// <summary>What sits on each end of a line or an arrow.</summary>
    CanvasArrowHead StartHead { get; }

    CanvasArrowHead EndHead { get; }
}

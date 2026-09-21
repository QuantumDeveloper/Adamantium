using Adamantium.MVVM;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Sandbox.DrawingBoard.ViewModels;

/// <summary>A SHAPE the canvas draws, described by the application. Put in an object's
/// <see cref="ICanvasObject.Content"/>, it says "draw this as a rounded rectangle" without the application ever
/// touching what does the drawing.</summary>
[ViewModel]
public partial class CanvasShapeViewModel : ICanvasShapeDescription
{
    public CanvasShapeViewModel(CanvasShape shape) => Shape = shape;

    /// <summary>Which shape it is. Said once, at birth: a rectangle that became an ellipse would be a different thing
    /// wearing the same object.</summary>
    public CanvasShape Shape { get; }

    /// <summary>What fills it, and null for a shape that is drawn as an outline only.</summary>
    [Bindable] private Brush _fill;

    [Bindable] private Brush _stroke;

    [Bindable] private double _thickness = 1;

    /// <summary>How round the corners are - a rectangle's business and nobody else's.</summary>
    [Bindable] private CornerRadius _corner;

    /// <summary>How many sides a polygon has.</summary>
    [Bindable] private int _sides = 6;

    /// <summary>What an arrow wears at each end.</summary>
    [Bindable] private CanvasArrowHead _startHead = CanvasArrowHead.None;

    [Bindable] private CanvasArrowHead _endHead = CanvasArrowHead.Triangle;
}

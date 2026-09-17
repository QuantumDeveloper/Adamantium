namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>A grip on the manipulation frame around what is selected. The eight round the edge resize;
/// <see cref="Body"/> is the inside of the frame, which moves what is selected without resizing it.</summary>
public enum CanvasHandle
{
    None,

    Body,

    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left
}

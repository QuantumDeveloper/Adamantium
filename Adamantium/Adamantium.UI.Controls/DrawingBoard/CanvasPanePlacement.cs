namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Where a <see cref="CanvasPane"/> sits over the plane. The middle of a canvas is the work, so every
/// placement but two names an EDGE or a corner of the viewport - and the two that do not name something the pane
/// follows instead.</summary>
public enum CanvasPanePlacement
{
    TopLeft,

    /// <summary>Along the top, centered - a toolbar rather than a panel.</summary>
    TopCenter,

    TopRight,

    /// <summary>Against the left edge, centered down it - where a tool rail belongs.</summary>
    Left,

    Right,

    BottomLeft,

    BottomCenter,

    BottomRight,

    /// <summary>Wherever it was put: <see cref="CanvasPane.Offset"/> says where, in screen pixels from the canvas's
    /// top-left. Dragging a pane sets this - and because it is a property like any other, an application can save
    /// where the user left it and put it back.</summary>
    Free,

    /// <summary>Following the SELECTION rather than the viewport: the pane rides above the selection frame and moves
    /// with it, and it is shown only while something is selected. A context bar is this and nothing else.</summary>
    Selection
}

namespace Adamantium.UI.Controls;

/// <summary>Where <see cref="InfiniteCanvas.Overlay"/> sits in the viewport. A corner, because that is where a floating
/// panel belongs on a surface whose middle is the work.</summary>
public enum CanvasOverlayPlacement
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,

    /// <summary>Along the top, centred - a toolbar rather than a panel.</summary>
    TopCentre,

    /// <summary>Along the bottom, centred.</summary>
    BottomCentre,

    /// <summary>Wherever it was put: <see cref="InfiniteCanvas.OverlayOffset"/> says where, in screen pixels from the
    /// canvas's top-left. Dragging the panel sets this - and because it is a property like any other, an application
    /// can save where the user left it and put it back.</summary>
    Free
}

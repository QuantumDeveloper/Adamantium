namespace Adamantium.UI.Controls;

/// <summary>
/// Which axes a <see cref="ScrollViewer"/> pans when its content is dragged (touch-style "grab and move"). Lets the
/// app opt into panning per axis instead of it being unconditional - the content can still always be scrolled by the
/// scrollbars / wheel. Mirrors WPF's PanningMode.
/// </summary>
public enum PanningMode
{
    /// <summary>No panning: the content moves only via the scrollbars / wheel (the default).</summary>
    None,

    /// <summary>Dragging the content pans the horizontal axis only.</summary>
    HorizontalOnly,

    /// <summary>Dragging the content pans the vertical axis only.</summary>
    VerticalOnly,

    /// <summary>Dragging the content pans both axes.</summary>
    Both
}

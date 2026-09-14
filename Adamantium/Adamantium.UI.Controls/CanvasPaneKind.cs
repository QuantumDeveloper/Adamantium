namespace Adamantium.UI.Controls;

/// <summary>What SHAPE of panel this is. It picks the template the theme gives the pane, and nothing else - where the
/// pane sits is <see cref="CanvasPanePlacement"/>'s business, and the two are deliberately independent: a bar along
/// the bottom and a bar that follows the selection are the same kind of thing in different places.</summary>
public enum CanvasPaneKind
{
    /// <summary>A panel with a header and a body - the inspector.</summary>
    Sheet,

    /// <summary>A strip of icon buttons, one per item, with no header - the tool rail.</summary>
    Rail,

    /// <summary>A short horizontal row that is all content and no chrome - a context or view bar.</summary>
    Bar
}

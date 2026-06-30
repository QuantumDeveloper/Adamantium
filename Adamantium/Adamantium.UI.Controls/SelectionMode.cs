namespace Adamantium.UI.Controls;

/// <summary>
/// How a <see cref="ListBox"/> lets the user change the selection.
/// </summary>
public enum SelectionMode
{
    /// <summary>One item at a time (the default).</summary>
    Single,

    /// <summary>A plain click toggles an item independently, so several can be selected without a modifier key.</summary>
    Multiple,

    /// <summary>A plain click selects one item; Ctrl+click toggles an item; Shift+click selects the range from the anchor.</summary>
    Extended
}

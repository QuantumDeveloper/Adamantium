namespace Adamantium.UI.Controls.Docking;

/// <summary>Which way a pane's tab label is turned - see <see cref="Pane.LabelRotation"/>.</summary>
public enum PaneLabelRotation
{
    /// <summary>Lying flat, which is every tab in a horizontal strip.</summary>
    None,

    /// <summary>Turned to read UP the left edge - a panel collapsed against the left side.</summary>
    Left,

    /// <summary>Turned to read DOWN the right edge - a panel collapsed against the right side. The two sides turn
    /// opposite ways so the text always faces out of the panel it belongs to, which is how every editor does it.</summary>
    Right
}

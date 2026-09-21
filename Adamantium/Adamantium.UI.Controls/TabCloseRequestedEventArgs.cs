using System;

namespace Adamantium.UI.Controls;

/// <summary>
/// Raised by <see cref="TabControl.TabCloseRequested"/> when a tab's close button is clicked. Set <see cref="Cancel"/> to
/// keep the tab (e.g. to prompt for unsaved changes); otherwise the <see cref="TabControl"/> removes the tab by default.
/// </summary>
public class TabCloseRequestedEventArgs : EventArgs
{
    public TabCloseRequestedEventArgs(TabItem tab, object item)
    {
        Tab = tab;
        Item = item;
    }

    /// <summary>The tab container whose close button was clicked.</summary>
    public TabItem Tab { get; }

    /// <summary>The item behind the tab: the data item for a data-bound tab, or the <see cref="Tab"/> itself when authored.</summary>
    public object Item { get; }

    /// <summary>Set true to veto the default removal (the tab stays).</summary>
    public bool Cancel { get; set; }
}

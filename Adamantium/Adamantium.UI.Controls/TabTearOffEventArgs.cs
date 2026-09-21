using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>A tab dropped away from its strip: it wants a window of its own. The handler decides what that window is
/// and takes the item; leaving <see cref="Handled"/> false puts the tab back where it was.</summary>
public class TabTearOffEventArgs : EventArgs
{
    public TabTearOffEventArgs(object item, TabItem container, PixelPoint screenPosition)
    {
        Item = item;
        Container = container;
        ScreenPosition = screenPosition;
    }

    /// <summary>The data item behind the torn-off tab (the container's own DataContext for a bound control).</summary>
    public object Item { get; }

    /// <summary>The tab container itself - its Header/Content, for a control filled from markup rather than bound.</summary>
    public TabItem Container { get; }

    /// <summary>Where the pointer let go, in SCREEN coordinates - where the new window should appear.</summary>
    public PixelPoint ScreenPosition { get; }

    /// <summary>Set by a handler that took the item; false leaves the tab in place.</summary>
    public bool Handled { get; set; }

    /// <summary>The window the handler put the tab in, if it wants to say. Carrying it under the cursor is the WINDOW's
    /// own business - call <see cref="WindowBase.DragMove"/> on it and the platform's move loop takes the still-held
    /// button, which keeps Aero Snap and edge snapping working. The strip stops tracking the moment the tab leaves it.</summary>
    public WindowBase TornWindow { get; set; }
}

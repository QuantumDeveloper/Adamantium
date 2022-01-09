using System;

namespace Adamantium.UI.RoutedEvents;

public class WindowClosingEventArgs : EventArgs
{
    public bool Cancel { get; set; }
}
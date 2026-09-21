using System;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>The new value and the cause carried by <see cref="ScrollBar.Scroll"/>.</summary>
public sealed class ScrollEventArgs : EventArgs
{
    public ScrollEventArgs(ScrollEventType scrollEventType, double newValue)
    {
        ScrollEventType = scrollEventType;
        NewValue = newValue;
    }

    public ScrollEventType ScrollEventType { get; }
    public double NewValue { get; }
}

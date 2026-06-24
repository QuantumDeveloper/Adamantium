using System;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>The old/new value carried by <see cref="RangeBase.ValueChanged"/>.</summary>
public sealed class ValueChangedEventArgs : EventArgs
{
    public ValueChangedEventArgs(double oldValue, double newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public double OldValue { get; }
    public double NewValue { get; }
}

using System;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>A value change where "no value at all" is one of the states - see <see cref="NumericUpDown.Value"/>.</summary>
public sealed class NullableValueChangedEventArgs : EventArgs
{
    public NullableValueChangedEventArgs(double? oldValue, double? newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public double? OldValue { get; }
    public double? NewValue { get; }
}

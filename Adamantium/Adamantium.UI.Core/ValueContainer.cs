using System;
using System.Collections.Generic;

namespace Adamantium.UI.Core;

internal class ValueContainer
{
    // The number of priority slots, resolved ONCE. Was Enum.GetValues<ValuePriority>() PER container (i.e. per property
    // per component) - a reflection call run tens of thousands of times when a virtualized list realizes a burst of
    // items. Now a plain fixed-size array indexed by (int)priority.
    private static readonly int SlotCount = Enum.GetValues<ValuePriority>().Length;

    private readonly object[] _values;

    public ValueContainer()
    {
        _values = new object[SlotCount];
        Array.Fill(_values, AdamantiumProperty.UnsetValue);
    }

    public IReadOnlyList<object> Values => _values;

    public void SetValue(object value, ValuePriority priority)
    {
        _values[(int)priority] = value;
    }

    public object GetValue(ValuePriority priority)
    {
        return _values[(int)priority];
    }
}

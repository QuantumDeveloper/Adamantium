using System;
using System.Collections.Generic;

namespace Adamantium.UI.Controls;

internal class ValueContainer
{
    private List<object> _values;
    
    public ValueContainer()
    {
        _values = new List<object>();
        var enums = Enum.GetValues<ValuePriority>();
        foreach (var @enum in enums)
        {
            _values.Add(AdamantiumProperty.UnsetValue);
        }
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
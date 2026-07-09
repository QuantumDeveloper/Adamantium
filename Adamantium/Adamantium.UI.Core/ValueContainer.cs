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

    // Cached effective value (the highest-priority set slot) + dirty flag. GetValue is the hottest call in the engine
    // (measure/arrange read Margin/alignment/min-max on every node - hundreds of thousands of reads per scroll frame) and
    // used to scan all source slots on EVERY read. The value only changes on a Set, so cache it and re-scan lazily on the
    // next read after a change - moving the scan off the read path. Read+write both run under AdamantiumComponent's
    // `values` lock, so no extra synchronisation is needed.
    private object _effective = AdamantiumProperty.UnsetValue;
    private bool _effectiveDirty = true;

    public ValueContainer()
    {
        _values = new object[SlotCount];
        Array.Fill(_values, AdamantiumProperty.UnsetValue);
    }

    public IReadOnlyList<object> Values => _values;

    public void SetValue(object value, ValuePriority priority)
    {
        _values[(int)priority] = value;
        _effectiveDirty = true;
    }

    public object GetValue(ValuePriority priority)
    {
        return _values[(int)priority];
    }

    /// <summary>The effective value: the first SET slot scanning source priorities 0..<paramref name="maxPriority"/>
    /// (highest priority wins). O(1) when clean; re-scans only after a Set changed a slot.</summary>
    public object GetEffective(int maxPriority)
    {
        if (_effectiveDirty)
        {
            _effective = AdamantiumProperty.UnsetValue;
            for (var i = 0; i <= maxPriority; i++)
                if (_values[i] != AdamantiumProperty.UnsetValue) { _effective = _values[i]; break; }
            _effectiveDirty = false;
        }
        return _effective;
    }
}

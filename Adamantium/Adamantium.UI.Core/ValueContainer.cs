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

    // The effective value (the highest-priority set slot), computed WHEN A SLOT IS WRITTEN. Reads outnumber writes by
    // orders of magnitude - measure/arrange read Margin/alignment/min-max on every node, hundreds of thousands of times
    // a scroll frame - so the scan belongs on the rare side.
    //
    // It used to be computed lazily ON READ (a dirty flag re-scanned on the next GetEffective), which made READING a
    // WRITE: two threads reading the same property could tear it - one clearing the flag before the other saw the new
    // value - and that is why a read had to take the component's lock at all. Now a read is a single volatile field
    // read, so it can never observe a half-finished update and needs no lock of its own.
    private volatile object _effective = AdamantiumProperty.UnsetValue;

    // The lowest-priority SOURCE slot; everything below it is the computed tail (see ValuePriority).
    private const int LastSourcePriority = (int)ValuePriority.Default;

    public ValueContainer()
    {
        _values = new object[SlotCount];
        Array.Fill(_values, AdamantiumProperty.UnsetValue);
    }

    public IReadOnlyList<object> Values => _values;

    // Which slot the effective value currently comes from. Kept so the common Set does NOT rescan: writing a value at or
    // above the winning priority simply becomes the new winner. Only a write that could UNCOVER a lower slot (clearing
    // the winner, or writing below it) has to look, and those are rare.
    private int _effectiveFrom = LastSourcePriority + 1;

    public void SetValue(object value, ValuePriority priority)
    {
        var slot = (int)priority;
        _values[slot] = value;

        // Published as ONE reference write: a reader sees the value from before this Set or the one after it, never a
        // container caught mid-rescan.
        if (value != AdamantiumProperty.UnsetValue && slot <= _effectiveFrom)
        {
            _effectiveFrom = slot;
            _effective = value;
            return;
        }

        if (slot > _effectiveFrom) return;   // written under the winner - it changes nothing that is visible

        _effectiveFrom = LastSourcePriority + 1;
        _effective = Scan();
    }

    public object GetValue(ValuePriority priority)
    {
        return _values[(int)priority];
    }

    /// <summary>The effective value: the first SET slot, highest priority first. A plain field read - no scan, no
    /// bookkeeping, nothing to synchronise.</summary>
    public object Effective => _effective;

    private object Scan()
    {
        for (var i = 0; i <= LastSourcePriority; i++)
        {
            if (_values[i] == AdamantiumProperty.UnsetValue) continue;

            _effectiveFrom = i;
            return _values[i];
        }

        return AdamantiumProperty.UnsetValue;
    }
}

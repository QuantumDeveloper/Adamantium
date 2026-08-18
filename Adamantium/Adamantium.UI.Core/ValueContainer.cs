using System;

namespace Adamantium.UI.Core;

internal class ValueContainer
{
    // The number of priority slots, resolved ONCE. Was Enum.GetValues<ValuePriority>() PER container (i.e. per property
    // per component) - a reflection call run tens of thousands of times when a virtualized list realizes a burst of
    // items. Now a plain fixed-size array indexed by (int)priority.
    private static readonly int SlotCount = Enum.GetValues<ValuePriority>().Length;

    // The slots, allocated ON THE FIRST REAL WRITE. A component seeds a container for every property its type registers
    // and then writes a handful of them: an element carries about 58 and sets maybe five. Allocating the array up front
    // cost one object[9] per property per element - 615 000 arrays to build one tab, measured - for slots that stay
    // empty for the object's whole life. Until something is written, the DEFAULT alone describes the container, and it
    // lives in a field.
    private object[] _values;

    private object _defaultValue = AdamantiumProperty.UnsetValue;

    // The effective value (the highest-priority set slot), computed WHEN A SLOT IS WRITTEN. Reads outnumber writes by
    // orders of magnitude - measure/arrange read Margin/alignment/min-max on every node, hundreds of thousands of times
    // a scroll frame - so the scan belongs on the rare side.
    //
    // It used to be computed lazily ON READ (a dirty flag re-scanned on the next GetEffective), which made READING a
    // WRITE: two threads reading the same property could tear it - one clearing the flag before the other saw the new
    // value - and that is why a read had to take the component's lock at all. Now a read is a single volatile field
    // read, so it can never observe a half-finished update and needs no lock of its own.
    private volatile object _effective = AdamantiumProperty.UnsetValue;

    // The winning slot's value BEFORE coercion - what was actually asked for. Coercion is a mapping from this to the
    // effective value, not a rewrite of the request: keeping the request means it can be mapped AGAIN when the things
    // the coercion depends on change. Without it a value that had to be clamped is gone for good - a lower bound of 20
    // clamped to 1 because the upper bound had not arrived yet could never come back to 20 once it did.
    // Only read/written under the container's lock (writes and re-coercions), so it needs no volatile of its own.
    private object _base = AdamantiumProperty.UnsetValue;

    // The lowest-priority SOURCE slot; everything below it is the computed tail (see ValuePriority).
    private const int LastSourcePriority = (int)ValuePriority.Default;

    // Which slot the effective value currently comes from. Kept so the common Set does NOT rescan: writing a value at or
    // above the winning priority simply becomes the new winner. Only a write that could UNCOVER a lower slot (clearing
    // the winner, or writing below it) has to look, and those are rare.
    private int _effectiveFrom = LastSourcePriority + 1;

    /// <summary>Writes a slot and returns the new BASE value - the winning slot's request, still uncoerced. The caller
    /// coerces it and hands the result back through <see cref="SetEffective"/>; until then the effective value is
    /// unchanged, so a reader never sees a value that has skipped its coercion.</summary>
    public object SetValue(object value, ValuePriority priority)
    {
        var slot = (int)priority;

        // Still array-free: a seeded default stays in its field, anything else brings the slots into being.
        if (_values == null)
        {
            if (slot == LastSourcePriority)
            {
                _defaultValue = value;
                _effectiveFrom = LastSourcePriority;
                return _base = value;
            }

            Materialize();
        }

        _values[slot] = value;

        if (value != AdamantiumProperty.UnsetValue && slot <= _effectiveFrom)
        {
            _effectiveFrom = slot;
            return _base = value;
        }

        if (slot > _effectiveFrom) return _base;   // written under the winner - it changes nothing that is visible

        _effectiveFrom = LastSourcePriority + 1;
        return _base = Scan();
    }

    // Give the container its real slots, carrying the seeded default across. Runs at most once, and only for a property
    // something actually writes.
    private void Materialize()
    {
        var values = new object[SlotCount];
        Array.Fill(values, AdamantiumProperty.UnsetValue);
        values[LastSourcePriority] = _defaultValue;
        _values = values;
    }

    public object GetValue(ValuePriority priority)
    {
        if (_values != null) return _values[(int)priority];

        return (int)priority == LastSourcePriority ? _defaultValue : AdamantiumProperty.UnsetValue;
    }

    /// <summary>Nothing but the seeded DEFAULT stands here: no local value, no style, no binding - and no inherited one
    /// cached yet either. That is exactly the case an inheriting property resolves from its ancestors.</summary>
    public bool IsDefaultOnly => _effectiveFrom >= LastSourcePriority;

    /// <summary>Which inheritance EPOCH the cached inherited value was resolved in (see
    /// <see cref="AdamantiumProperty.InheritanceEpoch"/>). -1 = never resolved. A stamp per container, so one element
    /// re-resolving costs nothing to the rest.</summary>
    public long InheritedStamp { get; set; } = -1;

    /// <summary>Fills the INHERITED slot with a value resolved from an ancestor, without going through the write path:
    /// this is a cache fill, not a set. Nothing is notified, because from the outside the value did not change - it is
    /// the same value the property already read as, only now it is resolved rather than walked for.
    /// <para>This is a WRITE ON THE READ PATH, which this class otherwise refuses to do (see <c>_effective</c>). It is
    /// allowed here on one condition: the writes are ordered so that a concurrent reader can never see a HALF-resolved
    /// value. Each field below is written atomically on its own, the published <see cref="Effective"/> goes LAST and is
    /// volatile, and the stamp after it - so a racing reader sees either the value from before this fill or the one
    /// after it, and at worst resolves the same answer a second time. Two threads resolving at once compute the same
    /// value from the same ancestors, so the race costs work, never correctness.</para></summary>
    public void SetInheritedCache(object raw, object coerced, long epoch)
    {
        if (raw == AdamantiumProperty.UnsetValue)
        {
            InheritedStamp = epoch;   // no ancestor holds one, so the default stands and there is nothing to cache
            return;
        }

        if (_values == null) Materialize();

        if ((int)ValuePriority.Inherited > _effectiveFrom)
        {
            // Something above the inherited slot wins anyway: keep the slot for completeness and stamp it resolved.
            _values[(int)ValuePriority.Inherited] = raw;
            InheritedStamp = epoch;
            return;
        }

        _values[(int)ValuePriority.Inherited] = raw;
        _base = raw;
        _effectiveFrom = (int)ValuePriority.Inherited;
        _effective = coerced;   // published LAST: this is the one a lock-free reader actually reads
        InheritedStamp = epoch;
    }

    /// <summary>The effective value: the winning slot's request, coerced. A plain field read - no scan, no bookkeeping,
    /// nothing to synchronise.</summary>
    public object Effective => _effective;

    /// <summary>What the winning slot asked for, before coercion - the input a re-coercion works from.</summary>
    public object BaseValue => _base;

    /// <summary>Publishes the coerced value. ONE reference write: a reader sees the value from before this update or the
    /// one after it, never a container caught mid-change.</summary>
    public void SetEffective(object value) => _effective = value;

    private object Scan()
    {
        if (_values == null)
        {
            _effectiveFrom = LastSourcePriority;
            return _defaultValue;
        }

        for (var i = 0; i <= LastSourcePriority; i++)
        {
            if (_values[i] == AdamantiumProperty.UnsetValue) continue;

            _effectiveFrom = i;
            return _values[i];
        }

        return AdamantiumProperty.UnsetValue;
    }
}

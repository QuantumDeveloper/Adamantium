using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core;

internal class StyleValueContainer
{
    private List<StyleValuePair> _values;
    private HashSet<Style> _styleHash;

    public StyleValueContainer()
    {
        _values = new List<StyleValuePair>();
        _styleHash = new HashSet<Style>();
    }

    /// <summary>Record one style's contribution, KEPT ORDERED BY SPECIFICITY - least specific first, so the last entry
    /// is the one in force. Ordering rather than appending is what stops the order styles are DECLARED in from deciding
    /// the outcome: a plain setter writes at one priority, so before this the last style applied simply won. Equal
    /// specificity keeps insertion order, as the web does.</summary>
    public void AddValue(Style style, object value)
    {
        // A style writes the SAME property more than once when it has a BasedOn: the base's setters are applied first
        // and its own after, both under this style as the owner. The second write is the one that must stand - it is
        // what BasedOn means - so a repeat REPLACES the recorded value instead of being dropped. Dropping it gave
        // MenuScrollViewer the plain ScrollViewer template it is BasedOn, so every menu grew a scrollbar where its own
        // template draws step arrows.
        foreach (var recorded in _values)
        {
            if (recorded.Style != style) continue;
            recorded.Value = value;
            return;
        }

        if (_styleHash.Contains(style))
            return;

        var band = style?.Band ?? 0;
        var at = _values.Count;
        while (at > 0 && (_values[at - 1].Style?.Band ?? 0) > band) at--;

        _values.Insert(at, new StyleValuePair(style, value));
        _styleHash.Add(style);
    }

    /// <summary>The contribution in force: the most specific one recorded, or nothing at all.</summary>
    public object EffectiveValue =>
        _values.Count > 0 ? _values[^1].Value : AdamantiumProperty.UnsetValue;

    public Resources.Style EffectiveStyle => _values.Count > 0 ? _values[^1].Style : null;

    /// <summary>Takes one style's contribution out and answers with the value in force AFTER it is gone - the most
    /// specific of the contributions still standing (the list is kept in specificity order, see AddValue).
    /// <para>It used to answer with the entry sitting immediately BEFORE the removed one, which is the same thing only
    /// while styles are taken off in exact reverse order of application. A theme swap does not oblige: it applies the
    /// incoming set and then drops the outgoing one, so the entry removed is the one at the BOTTOM - and "the entry
    /// before it" is nothing at all. That nothing was then written into the property, wiping the incoming theme's
    /// Template and Background: the window rendered blank white.</para></summary>
    public object RemoveAndGetEffectiveValue(Style style)
    {
        var entry = _values.FirstOrDefault(x => x.Style == style);
        if (entry == null) return AdamantiumProperty.UnsetValue;

        _values.Remove(entry);
        _styleHash.Remove(style);

        return _values.Count > 0 ? _values[^1].Value : AdamantiumProperty.UnsetValue;
    }

    public object GetValue(Style style)
    {
        var entry = _values.FirstOrDefault(x => x.Style == style);
        return entry != null ? entry.Value : AdamantiumProperty.UnsetValue;
    }
}
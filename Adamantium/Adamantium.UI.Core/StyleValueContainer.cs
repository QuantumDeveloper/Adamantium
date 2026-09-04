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

    /// <summary>...and WHICH style it came from, which decides the slot the value is written to: a contribution from a
    /// bare-type selector is a type default (see <see cref="Resources.Style.IsTypeDefault"/>). It has to be the WINNING
    /// entry's style rather than the one being applied - a class style arriving after a type style must not have its
    /// value filed as a default because the type style was the last to speak.</summary>
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
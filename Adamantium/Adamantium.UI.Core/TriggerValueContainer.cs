using System.Collections.Generic;

namespace Adamantium.UI.Core;

/// <summary>
/// Per-property stack of trigger contributions (last-applied wins), so MULTIPLE triggers can target the SAME property
/// without clobbering each other: leaving the top trigger restores the one beneath it instead of dropping to default.
/// Mirrors <see cref="StyleValueContainer"/>, but keyed by an opaque token (the setter that pushed the value) rather
/// than a Style, and the effective value is simply the most-recently-applied entry - which is WPF's trigger precedence.
/// </summary>
internal class TriggerValueContainer
{
    private readonly List<(object Token, object Value)> _values = [];

    /// <summary>Records a trigger setter's contribution. Re-applying an existing token updates its value IN PLACE
    /// (keeping its stack position), so an idempotent re-apply or a {ThemeResource} refresh doesn't reshuffle who wins.</summary>
    public void Set(object token, object value)
    {
        for (var i = 0; i < _values.Count; i++)
        {
            if (ReferenceEquals(_values[i].Token, token))
            {
                _values[i] = (token, value);
                return;
            }
        }
        _values.Add((token, value));
    }

    /// <summary>Drops a contribution. The effective value is then the last remaining one (see <see cref="EffectiveValue"/>).</summary>
    public void Remove(object token)
    {
        for (var i = 0; i < _values.Count; i++)
        {
            if (ReferenceEquals(_values[i].Token, token))
            {
                _values.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>The value that wins right now: the most-recently-applied still-present contribution, or
    /// <see cref="AdamantiumProperty.UnsetValue"/> when the stack is empty (so the slot falls through below Trigger).</summary>
    public object EffectiveValue => _values.Count > 0 ? _values[^1].Value : AdamantiumProperty.UnsetValue;
}

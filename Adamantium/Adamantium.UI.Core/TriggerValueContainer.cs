using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core;

/// <summary>
/// Per-property stack of trigger contributions, so MULTIPLE triggers can target the SAME property without clobbering
/// each other: leaving the top trigger restores the one beneath it instead of dropping to default.
/// <para>Who is on top is decided by where the setters stand in the MARKUP (<see cref="ISetter.DeclarationOrder"/>) -
/// the rule a theme author writes against and the one WPF uses among the triggers of one collection. Resolving by which
/// fired last instead made the look depend on the history of events: a drop-down row that was both selected and
/// keyboard-highlighted came out accent on its first showing and grey on the next, because closing dropped the
/// highlight and reopening pushed it back on top of the selection.</para>
/// </summary>
internal class TriggerValueContainer
{
    private readonly List<(object Token, object Value, int Order)> _values = [];

    /// <summary>Records a trigger setter's contribution. Re-applying an existing token updates its value IN PLACE, so an
    /// idempotent re-apply or a {ThemeResource} refresh changes nothing about who wins.</summary>
    public void Set(object token, object value)
    {
        var order = (token as ISetter)?.DeclarationOrder ?? 0;

        for (var i = 0; i < _values.Count; i++)
        {
            if (ReferenceEquals(_values[i].Token, token))
            {
                _values[i] = (token, value, order);
                return;
            }
        }

        _values.Add((token, value, order));
    }

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

    /// <summary>The value that wins right now: the one written LOWEST in the markup among those still applied, or
    /// <see cref="AdamantiumProperty.UnsetValue"/> when the stack is empty (so the slot falls through below Trigger).
    /// Ties - contributions with no declared order, e.g. a code-made setter - keep the last-applied rule.</summary>
    public object EffectiveValue
    {
        get
        {
            if (_values.Count == 0) return AdamantiumProperty.UnsetValue;

            var winner = 0;
            for (var i = 1; i < _values.Count; i++)
            {
                if (_values[i].Order >= _values[winner].Order) winner = i;
            }

            return _values[winner].Value;
        }
    }
}

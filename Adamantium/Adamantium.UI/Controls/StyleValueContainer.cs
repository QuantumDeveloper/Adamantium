using System.Collections.Generic;
using System.Linq;
using Adamantium.UI.Resources;

namespace Adamantium.UI.Controls;

internal class StyleValueContainer
{
    private List<StyleValuePair> _values;
    private HashSet<Style> _styleHash;

    public StyleValueContainer()
    {
        _values = new List<StyleValuePair>();
        _styleHash = new HashSet<Style>();
    }

    public void AddValue(Style style, object value)
    {
        if (!_styleHash.Contains(style))
        {
            _values.Add(new StyleValuePair(style, value));
            _styleHash.Add(style);
        }
    }

    public object RemoveAndGetPreviousValue(Style style)
    {
        var entry = _values.FirstOrDefault(x => x.Style == style);
        if (entry == null) return AdamantiumProperty.UnsetValue;

        object prevValue = AdamantiumProperty.UnsetValue;
        var index = _values.IndexOf(entry);
        if (index > 0)
        {
            prevValue = _values[index - 1].Value;
        }
        _values.Remove(entry);
        _styleHash.Remove(style);

        return prevValue;
    }

    public object GetValue(Style style)
    {
        var entry = _values.FirstOrDefault(x => x.Style == style);
        return entry != null ? entry.Value : AdamantiumProperty.UnsetValue;
    }
}
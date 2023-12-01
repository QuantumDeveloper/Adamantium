using Adamantium.UI.Resources;

namespace Adamantium.UI.Controls;

internal class StyleValuePair
{
    public StyleValuePair(Style style, object value)
    {
        Style = style;
        Value = value;
    }
    public Style Style { get; }
    
    public object Value { get; }
}
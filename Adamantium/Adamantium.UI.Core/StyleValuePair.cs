using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core;

internal class StyleValuePair
{
    public StyleValuePair(Style style, object value)
    {
        Style = style;
        Value = value;
    }
    public Style Style { get; }
    
    public object Value { get; set; }
}
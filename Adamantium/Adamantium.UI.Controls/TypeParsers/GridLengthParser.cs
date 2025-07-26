using Adamantium.Core.TypeParsing;
using Adamantium.UI.Controls.Panels;

namespace Adamantium.UI.Controls.TypeParsers;

public class GridLengthParser : ITypeParser<GridLength>
{
    public GridLength Parse(string value)
    {
        switch (value)
        {
            case "Auto":
                return GridLength.Auto;
            default:
            {
                if (value.EndsWith("*"))
                {
                    if (value == "*")
                        return GridLength.Star;
                    var star = value.Substring(0, value.Length - 1);
                    return new GridLength(Double.Parse(star), GridUnitType.Star);
                }

                return new GridLength(Double.Parse(value), GridUnitType.Pixel);
            }
        }
    }
}
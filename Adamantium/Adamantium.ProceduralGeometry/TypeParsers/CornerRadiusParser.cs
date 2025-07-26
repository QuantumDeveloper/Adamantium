using System.Globalization;
using Adamantium.Core.TypeParsing;

namespace Adamantium.ProceduralGeometry.TypeParsers;

public class CornerRadiusParser : ITypeParser<CornerRadius>
{
    public CornerRadius Parse(string value)
    {
        var values = value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

        if (values.Length == 1)
        {
            return new CornerRadius(double.Parse(values[0], CultureInfo.InvariantCulture));
        }

        var list = new List<double>();
        foreach (var v in values)
        {
            list.Add(double.Parse(v, CultureInfo.InvariantCulture));
        }

        return new CornerRadius(list);
    }
}
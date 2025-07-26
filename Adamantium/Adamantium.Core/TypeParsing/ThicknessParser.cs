using System;
using System.Collections.Generic;
using System.Globalization;
using Adamantium.Mathematics;

namespace Adamantium.Core.TypeParsing;

public class ThicknessParser : ITypeParser<Thickness>
{
    public Thickness Parse(string value)
    {
        var values = value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);

        if (values.Length == 1)
        {
            return new Thickness(double.Parse(values[0], CultureInfo.InvariantCulture));
        }

        var list = new List<double>();
        foreach (var v in values)
        {
            list.Add(double.Parse(v, CultureInfo.InvariantCulture));
        }

        return new Thickness(list);
    }
}
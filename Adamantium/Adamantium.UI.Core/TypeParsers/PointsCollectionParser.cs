using System;
using System.Globalization;
using System.Linq;
using Adamantium.Core.TypeParsing;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Collections;

namespace Adamantium.UI.Core.TypeParsers;

/// <summary>
/// Parses a points string into a <see cref="PointsCollection"/> - the WPF <c>Points</c> syntax used by Polygon/Polyline
/// (<c>"x1,y1 x2,y2 …"</c>, or equally <c>"x1 y1 x2 y2 …"</c>). Coordinates may be separated by whitespace or commas;
/// they are read as a flat list of numbers and paired into <see cref="Vector2"/> points.
/// </summary>
public class PointsCollectionParser : ITypeParser<PointsCollection>
{
    private static readonly char[] Separators = [' ', ',', '\t', '\r', '\n'];

    public PointsCollection Parse(string value)
    {
        var points = new PointsCollection();
        if (string.IsNullOrWhiteSpace(value)) return points;

        var numbers = value.Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(n => double.Parse(n, CultureInfo.InvariantCulture))
            .ToArray();

        for (var i = 0; i + 1 < numbers.Length; i += 2)
            points.Add(new Vector2(numbers[i], numbers[i + 1]));

        return points;
    }
}

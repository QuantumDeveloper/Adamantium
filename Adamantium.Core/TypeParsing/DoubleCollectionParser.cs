using System;
using System.Globalization;
using Adamantium.Core.Collections;

namespace Adamantium.Core.TypeParsing;

// Parses a space/comma-separated list of numbers into a TrackingCollection<double> - e.g. a Shape's StrokeDashArray
// ("10,6" or "10 6 2 6"). Lets such collection properties be set straight from an AUML string attribute.
public class DoubleCollectionParser : ITypeParser<TrackingCollection<double>>
{
    public TrackingCollection<double> Parse(string value)
    {
        var result = new TrackingCollection<double>();
        if (string.IsNullOrWhiteSpace(value)) return result;

        var parts = value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
            result.Add(double.Parse(part, CultureInfo.InvariantCulture));

        return result;
    }
}

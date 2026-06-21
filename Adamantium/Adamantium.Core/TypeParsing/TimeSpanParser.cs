using System;
using System.Globalization;

namespace Adamantium.Core.TypeParsing;

/// <summary>
/// Parses a <see cref="TimeSpan"/> in the invariant "[d.]h:m:s[.fff]" form used by animation Duration/Delay
/// (e.g. "0:0:0.3"). Invariant so markup parses the same regardless of OS locale.
/// </summary>
public class TimeSpanParser : ITypeParser<TimeSpan>
{
    public TimeSpan Parse(string value) => TimeSpan.Parse(value, CultureInfo.InvariantCulture);
}

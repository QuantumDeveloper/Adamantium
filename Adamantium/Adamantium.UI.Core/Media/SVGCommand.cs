using System.Globalization;
using System.Text.RegularExpressions;

namespace Adamantium.UI.Core.Media;

public class SVGCommand
{
    public SVGCommand(char command)
    {
        Command = command;
        Arguments = new List<double>();
    }
   
    public char Command { get; }
   
    public List<double> Arguments { get; }

    public static SVGCommand Parse(string svgCommand)
    {
        var command = new SVGCommand(svgCommand[0]);

        foreach (var number in Numbers(svgCommand.AsSpan(1))) command.Arguments.Add(number);

        return command;
    }

    /// <summary>The numbers in a command's argument list, SVG's way: separated by commas, spaces or by nothing at all.
    /// A minus starts a new number, a second dot does too ("1.5.5" is two), and an exponent's sign belongs to the
    /// exponent - split on every minus, "1e-5" became "1e" and "-5" and the whole path was refused.</summary>
    private static List<double> Numbers(ReadOnlySpan<char> text)
    {
        var numbers = new List<double>();
        var at = 0;

        while (at < text.Length)
        {
            while (at < text.Length && (char.IsWhiteSpace(text[at]) || text[at] == ',')) at++;
            if (at >= text.Length) break;

            var start = at;
            var seenDot = false;

            if (text[at] is '-' or '+') at++;

            while (at < text.Length)
            {
                var c = text[at];

                if (char.IsDigit(c)) { at++; continue; }

                if (c == '.' && !seenDot) { seenDot = true; at++; continue; }

                if ((c == 'e' || c == 'E') && at + 1 < text.Length
                    && (char.IsDigit(text[at + 1]) || text[at + 1] is '-' or '+'))
                {
                    at += 2;
                    while (at < text.Length && char.IsDigit(text[at])) at++;
                }

                break;
            }

            if (at == start) { at++; continue; }

            if (double.TryParse(text[start..at], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                numbers.Add(value);
        }

        return numbers;
    }
   
    public override string ToString()
    {
        return $"Command: {Command}, Params: {string.Join(',', Arguments)}";
    }
}
using System.Text.RegularExpressions;
using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

public class SVGParser
{
    private string pattern = @"(?=[MZLHVCSQTAmzlhvcsqta])";

    public List<SVGCommand> Commands { get; private set; }

    public StreamGeometry Parse(string svgString)
    {
        svgString = svgString?.TrimStart() ?? string.Empty;

        // Optional leading fill-rule token (XAML/WPF path mini-language): "F0" = EvenOdd, "F1" = NonZero.
        FillRule? fillRule = null;
        if (svgString.Length >= 2 && (svgString[0] == 'F' || svgString[0] == 'f'))
        {
            if (svgString[1] == '0') fillRule = FillRule.EvenOdd;
            else if (svgString[1] == '1') fillRule = FillRule.NonZero;
            if (fillRule != null) svgString = svgString.Substring(2).TrimStart();
        }

        var tokens = Regex.Split(svgString, pattern).Where(t => !string.IsNullOrEmpty(t));
        Commands = new List<SVGCommand>();
        foreach (var token in tokens)
        {
            Commands.Add(SVGCommand.Parse(token));
        }

        if (Commands.Count == 0) return new StreamGeometry();

        var geometry = new SVGCommandInterpreter().InterpretCommands(Commands);

        if (fillRule != null) geometry.FillRule = fillRule.Value;

        return geometry;
    }

    public override string ToString()
    {
        return $"Commands count: {Commands.Count} ";
    }
}
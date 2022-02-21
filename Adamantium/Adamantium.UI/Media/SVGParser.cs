using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Adamantium.UI.Media;

public class SVGParser
{
    private string pattern = @"(?=[MZLHVCSQTAmzlhvcsqta])";
   
    public List<SVGCommand> Commands { get; private set; }
   
    public StreamGeometry Parse(string svgString)
    {
        var tokens = Regex.Split(svgString, pattern).Where(t => !string.IsNullOrEmpty(t));
        Commands = new List<SVGCommand>();
        foreach (var token in tokens)
        {
            Commands.Add(SVGCommand.Parse(token));
        }

        if (Commands.Count == 0) return new StreamGeometry();

        var geometry = new SVGCommandInterpreter().InterpretCommands(Commands);

        return geometry;
    }

    public override string ToString()
    {
        return $"Commands count: {Commands.Count} ";
    }
}
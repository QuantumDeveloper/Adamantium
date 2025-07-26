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
        string argSeparators = @"[\s,]|(?=-)";
      
        var commandChar = svgCommand[0];
      
        var splitArgs = Regex
            .Split(svgCommand.Substring(1), argSeparators)
            .Where(t => !string.IsNullOrEmpty(t));

        var command = new SVGCommand(commandChar);
      
        foreach (var arg in splitArgs)
        {
            command.Arguments.Add(double.Parse(arg, CultureInfo.InvariantCulture));
        }

        return command;
    }
   
    public override string ToString()
    {
        return $"Command: {Command}, Params: {string.Join(',', Arguments)}";
    }
}
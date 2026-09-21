using Adamantium.Core.TypeParsing;
using Adamantium.UI.Controls.Panels;

namespace Adamantium.UI.Controls.TypeParsers;

public class RowDefinitionsParser : ITypeParser<RowDefinitions>
{
    public RowDefinitions Parse(string value)
    {
        var definitionStrings = value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        
        var definitions = new RowDefinitions(); 
        foreach (var def in definitionStrings)
        {
            definitions.Add(new RowDefinition(TypeParser.Parse<GridLength>(def)));
        }
        
        return definitions;
    }
}
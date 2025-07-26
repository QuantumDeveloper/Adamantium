using Adamantium.Core.TypeParsing;
using Adamantium.UI.Controls.Panels;

namespace Adamantium.UI.Controls.TypeParsers;

public class ColumDefinitionsParser : ITypeParser<ColumnDefinitions>
{
    public ColumnDefinitions Parse(string value)
    {
        var definitionStrings = value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        
        var definitions = new ColumnDefinitions(); 
        foreach (var def in definitionStrings)
        {
            definitions.Add(new ColumnDefinition(TypeParser.Parse<GridLength>(def)));
        }
        
        return definitions;
    }
}
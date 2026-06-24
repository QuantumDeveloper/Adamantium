using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core.TypeParsers;

public class SelectorParser : ITypeParser<Selector>
{
    public Selector Parse(string value)
    {
        var splitResult = value.Split([',', ' '],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var selector = new Selector();

        foreach (var splitItem in splitResult)
        {
            if (splitItem.StartsWith('#'))
            {
                selector.Id = splitItem.Substring(1);
            }
            else if (splitItem.StartsWith('.'))
            {
                if (splitItem.Contains('.')) // several chained classes  
                {
                    var group = ClassGroup.Parse(splitItem);
                    selector.ClassGroups.Add(group);
                }
                else  // single class
                {
                    selector.Classes.Add(splitItem.Substring(1));
                }
            }
            else // Control type, optionally with chained .classes: "Button" or "Button.Accent" or "Button.Accent.Big"
            {
                var parts = splitItem.Split('.', StringSplitOptions.RemoveEmptyEntries);
                var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).FirstOrDefault(x => x.Name == parts[0]);
                if (type != null)
                {
                    selector.Types.Add(type);
                }
                for (var i = 1; i < parts.Length; i++)
                {
                    selector.Classes.Add(parts[i]);
                }
            }
        }
        return selector;
    }
}
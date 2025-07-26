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
            else // Control type
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).FirstOrDefault(x => x.Name == splitItem);
                if (type != null)
                {
                    selector.Types.Add(type);
                }
            }
        }
        return selector;
    }
}
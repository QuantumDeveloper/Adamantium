using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Resources.Triggers;

namespace Adamantium.UI.Core.TypeParsers;

public class SelectorParser : ITypeParser<StyleSelector>
{
    public StyleSelector Parse(string value)
    {
        var splitResult = value.Split([',', ' '],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var selector = new StyleSelector();

        foreach (var rawItem in splitResult)
        {
            // Peel off any "[Prop=Value]" condition fragments first (Avalonia-style), leaving the structural part to the
            // existing type/class/id parsing. Each fragment becomes a runtime property gate on the style's setters.
            var splitItem = ExtractConditions(rawItem, selector);
            if (splitItem.Length == 0) continue;

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

    // Splits "TabControl[TabStripPlacement=Left]" into the structural prefix ("TabControl") + one condition per bracket
    // group (added to the selector), tolerating several groups ("X[A=1][B=2]"). A group without '=' is ignored.
    private static string ExtractConditions(string item, StyleSelector selector)
    {
        var open = item.IndexOf('[');
        if (open < 0) return item;

        var structural = item[..open];
        var rest = item[open..];
        foreach (var fragment in rest.Split('[', StringSplitOptions.RemoveEmptyEntries))
        {
            var body = fragment.TrimEnd(']').Trim();
            var eq = body.IndexOf('=');
            if (eq <= 0) continue;
            selector.Conditions.Add(new Condition
            {
                Property = body[..eq].Trim(),
                Value = body[(eq + 1)..].Trim()
            });
        }
        return structural;
    }
}
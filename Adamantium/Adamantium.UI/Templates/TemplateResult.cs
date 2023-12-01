using System.Collections.Generic;
using Adamantium.UI.Resources;

namespace Adamantium.UI.Templates;

public class TemplateResult
{
    private readonly Dictionary<string, IUIComponent> namesMap;

    public TemplateResult()
    {
        namesMap = new Dictionary<string, IUIComponent>();
        Triggers = new TriggerCollection();
        Overrides = new TemplateOverridesCollection();
    }
    
    public IUIComponent RootComponent { get; internal set; }

    internal void RegisterName(string name, IUIComponent component)
    {
        namesMap[name] = component;
    }

    public IUIComponent GetComponentByName(string name)
    {
        namesMap.TryGetValue(name, out var component);

        return component;
    }
    
    public TriggerCollection Triggers { get; }
    
    public TemplateOverridesCollection Overrides { get; }
}
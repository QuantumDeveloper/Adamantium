using System;
using System.Collections.Generic;

namespace Adamantium.UI.Templates;

public class NameScope : INameScope
{
    private readonly Dictionary<string, object> nameScope;

    public NameScope()
    {
        nameScope = new Dictionary<string, object>();
    }
    
    public void RegisterName(string name, object component)
    {
        if (string.IsNullOrEmpty(name)) return;

        if (nameScope.TryGetValue(name, out var existingComponent))
        {
            if (component != existingComponent)
                throw new ArgumentException($"Component with name {name} already registered");
        }
        else
        {
            nameScope[name] = component;
        }
        
    }

    public void Unregister(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        
        nameScope.Remove(name);
    }

    public object Find(string name)
    {
        nameScope.TryGetValue(name, out var component);
        return component;
    }
}
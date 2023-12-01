using System;
using System.Collections.Generic;
using Adamantium.Core.Collections;

namespace Adamantium.UI;

internal class AdamantiumPropertyContainer
{
    private readonly Type owner;
    private Dictionary<string, AdamantiumProperty> propertiesMap;
    private AdamantiumCollection<AdamantiumProperty> propertiesList;

    public AdamantiumPropertyContainer(Type owner)
    {
        this.owner = owner;
        propertiesMap = new Dictionary<string, AdamantiumProperty>();
        propertiesList = new AdamantiumCollection<AdamantiumProperty>();
    }

    public IReadOnlyCollection<AdamantiumProperty> Properties => propertiesList.AsReadOnly();

    public void Add(AdamantiumProperty property)
    {
        if (propertiesMap.ContainsKey(property.Name))
        {
            throw new AdamantiumPropertyException($"Property {property.Name} already registered for type: {owner}");
        }

        propertiesMap[property.Name] = property;
        propertiesList.Add(property);
    }

    public bool Exists(string property)
    {
        return propertiesMap.ContainsKey(property);
    }
}
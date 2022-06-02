using Adamantium.Core.Collections;
using Adamantium.UI.Controls;

namespace Adamantium.UI;

public class StyleRepository : AdamantiumComponent
{
    private object syncObject = new object();

    public ResourceDictionary Resources { get; }

    [Content]
    public AdamantiumCollection<Style> Styles { get; }

    public StyleRepository()
    {
        Resources = new ResourceDictionary();
        Styles = new AdamantiumCollection<Style>();
    }

    public void AddResource(string name, object resource)
    {
        lock(syncObject)
        {
            Resources.Add(name, resource);
        }
    }

    public void RemoveResource(string name)
    {
        if (Resources.ContainsKey(name))
        {
            Resources.Remove(name);
        }
    }

    public object this[string resourceName] => !Resources.ContainsKey(resourceName) ? null : Resources[resourceName];
}
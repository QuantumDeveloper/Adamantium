using Adamantium.Core.Collections;
using Adamantium.Graphics.Core;

namespace Adamantium.UI.Core.Resources;

public interface IResourceDictionary : IResourceContainer, IName, IInitializable
{
    public object FindName(string name);

    public void Add(string key, object value);

    public void Remove(string key);

    public void Clear();

    public bool TryGetValue(string key, out object value);
    
    object this[string index] { get; set; }

    bool ContainsKey(string key);
}
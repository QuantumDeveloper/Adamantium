using Adamantium.Core.Collections;

namespace Adamantium.UI.Core.Resources;

public class ResourceDictionary : IResourceDictionary
{
    private readonly AdamantiumDictionary<string, object> resourceCache;

    private bool _isInitialized;
    private bool _isInitializing;

    public ResourceDictionary()
    {
        resourceCache = new AdamantiumDictionary<string, object>();
    }

    public object FindName(string name)
    {
        resourceCache.TryGetValue(name, out var findName);
        
        return findName;
    }

    public void Add(string key, object value)
    {
        resourceCache.Add(key, value);
    }

    public void Remove(string key)
    {
        resourceCache.Remove(key);
    }

    public void Clear()
    {
        resourceCache.Clear();
    }

    public bool TryGetValue(string key, out object value) => resourceCache.TryGetValue(key, out value);

    public object this[string index]
    {
        get => resourceCache[index];
        set => resourceCache[index] = value;
    }

    public bool ContainsKey(string key)
    {
        return resourceCache.ContainsKey(key);
    }

    public void AddOrSetChildComponent(string key, object component)
    {
        Add(key, component);
    }

    public void RemoveChildComponent(string key)
    {
        Remove(key);
    }

    public void RemoveAllChildComponents()
    {
        Clear();
    }

    public void Initialize()
    {
        if (Initialized || Initializing) return;

        _isInitializing = true;
        OnInitialize();
        _isInitialized = true;
        _isInitializing = false;
    }

    protected virtual void OnInitialize()
    {

    }

    public string Name { get; set; }

    public bool Initialized => _isInitialized;
    public bool Initializing => _isInitializing;
}

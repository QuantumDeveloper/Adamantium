namespace Adamantium.UI.Core.Resources;

public class ResourceProvider
{
    private readonly List<ResourceInfo> _orderedDictionaries = new();
    private readonly Dictionary<string, object> _cache = new();
    private readonly Dictionary<Type, List<string>> _cacheByDictionary = new();
    private readonly Dictionary<Type, ResourceInfo> _loadedDictionaries = new();

    public void AddSource(IAdamantiumComponent owner, Type sourceType)
    {
        if (!_loadedDictionaries.TryGetValue(sourceType, out var info))
        {
            info = new ResourceInfo(sourceType);
            _loadedDictionaries[sourceType] = info;
            _orderedDictionaries.Add(info);
            
            InvalidateCache();
        }
    
        info.Owners.Add(owner);
    }
   
    public void RemoveOwner(IAdamantiumComponent owner)
    {
        var abandonedInfos = new List<ResourceInfo>();

        foreach (var info in _orderedDictionaries)
        {
            if (info.Owners.Remove(owner) && info.Owners.Count == 0)
            {
                abandonedInfos.Add(info);
            }
        }

        if (abandonedInfos.Any())
        {
            foreach (var info in abandonedInfos)
            {
                _loadedDictionaries.Remove(info.Resource.GetType());
                _orderedDictionaries.Remove(info);
            }
                
            InvalidateCache();
        }
    }

    public object FindResource(string key)
    {
        if (_cache.TryGetValue(key, out var resource))
        {
            return resource;
        }

        for (int i = _orderedDictionaries.Count - 1; i >= 0; i--)
        {
            var info = _orderedDictionaries[i];
            if (info.Resource.TryGetValue(key, out resource))
            {
                _cache[key] = resource;
                return resource;
            }
        }
        return null;
    }

    private void AddCacheFor(Type type, string key)
    {
        if (!_cacheByDictionary.TryGetValue(type, out var keys))
        {
            keys = new List<string>();
            _cacheByDictionary.Add(type, keys);
        }
        keys.Add(key);
    }

    public void ClearCacheFor(Type type)
    {
        if (!_cacheByDictionary.TryGetValue(type, out var keys))
            return;
        
        foreach (var key in keys)
        {
            _cache.Remove(key);
        }
    }

    public void InvalidateCache()
    {
        _cache.Clear();
    }
    
    private class ResourceInfo
    {
        public ResourceDictionary Resource { get; }
    
        public HashSet<IAdamantiumComponent> Owners { get; } = new();

        public ResourceInfo(Type dictionaryType)
        {
            Resource = (ResourceDictionary)Activator.CreateInstance(dictionaryType);
            Resource?.Initialize();
        }
    }
}
namespace Adamantium.UI.Core.Resources;

public class ResourceProvider
{
    private readonly List<ResourceInfo> _orderedDictionaries = new();
    private readonly Dictionary<string, object> _cache = new();
    private readonly Dictionary<Type, List<string>> _cacheByDictionary = new();
    private readonly Dictionary<Type, ResourceInfo> _loadedDictionaries = new();
    // owner -> the dictionaries it owns, in add order. Lets FindResourceForOwner / RemoveOwner touch only THIS owner's
    // handful of dictionaries instead of scanning every dictionary in the scope (the tree-walk lookup asks this per
    // ancestor, so a global scan was O(ancestors x all-dictionaries) per resolve).
    private readonly Dictionary<IAdamantiumComponent, List<ResourceInfo>> _byOwner = new();

    public void AddSource(IAdamantiumComponent owner, Type sourceType)
    {
        if (!_loadedDictionaries.TryGetValue(sourceType, out var info))
        {
            info = new ResourceInfo(sourceType);
            _loadedDictionaries[sourceType] = info;
            _orderedDictionaries.Add(info);

            InvalidateCache();
        }

        if (info.Owners.Add(owner))
        {
            if (!_byOwner.TryGetValue(owner, out var owned))
            {
                owned = new List<ResourceInfo>();
                _byOwner[owner] = owned;
            }
            owned.Add(info);
        }
    }

    // An INLINE (per-element) dictionary: a unique instance authored right on the element (ResourceContext.Resources),
    // not a shared dictionary TYPE. No type-dedup (each instance is its own) - just register it under this owner, so it
    // is tree-scoped and freed with the owner exactly like a linked type is.
    public void AddSource(IAdamantiumComponent owner, ResourceDictionary instance)
    {
        var info = new ResourceInfo(instance);
        _orderedDictionaries.Add(info);
        info.Owners.Add(owner);
        if (!_byOwner.TryGetValue(owner, out var owned))
        {
            owned = new List<ResourceInfo>();
            _byOwner[owner] = owned;
        }
        owned.Add(info);
        InvalidateCache();
    }

    public void RemoveOwner(IAdamantiumComponent owner)
    {
        if (!_byOwner.TryGetValue(owner, out var owned)) return;
        _byOwner.Remove(owner);

        List<ResourceInfo> abandonedInfos = null;
        foreach (var info in owned)
        {
            if (info.Owners.Remove(owner) && info.Owners.Count == 0)
            {
                (abandonedInfos ??= new List<ResourceInfo>()).Add(info);
            }
        }

        if (abandonedInfos != null)
        {
            foreach (var info in abandonedInfos)
            {
                if (info.SourceType != null) _loadedDictionaries.Remove(info.SourceType);   // inline instances aren't in the type map
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

    // A key from dictionaries owned by EXACTLY this element. The caller (ResourceManager) walks the tree and asks each
    // ancestor in turn, so a Local resource is visible only inside the owner's subtree - never from an unrelated element.
    // Not cached: the same key can resolve to different values under different owners, so the flat _cache would leak
    // across scopes. Iterate only this owner's own dictionaries (via _byOwner), newest-first for last-declared-wins.
    public object FindResourceForOwner(IAdamantiumComponent owner, string key)
    {
        if (!_byOwner.TryGetValue(owner, out var owned)) return null;
        for (int i = owned.Count - 1; i >= 0; i--)
        {
            if (owned[i].Resource.TryGetValue(key, out var resource))
            {
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

        // The dictionary TYPE when this is a shared/deduped linked dictionary; null for an inline instance (which is
        // unique and not tracked in _loadedDictionaries).
        public Type SourceType { get; }

        public HashSet<IAdamantiumComponent> Owners { get; } = new();

        public ResourceInfo(Type dictionaryType)
        {
            SourceType = dictionaryType;
            Resource = (ResourceDictionary)Activator.CreateInstance(dictionaryType);
            Resource?.Initialize();
        }

        public ResourceInfo(ResourceDictionary instance)
        {
            Resource = instance;
            instance.Initialize();
        }
    }
}
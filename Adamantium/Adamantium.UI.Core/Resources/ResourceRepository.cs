using Adamantium.Core.Collections;

namespace Adamantium.UI.Core.Resources;

public static class ResourceRepository
{
    private static readonly object syncObject = new object();
    
    private static TrackingCollection<IResourceDictionary> _resources;
    private static TrackingCollection<IStyleSet> _styles;

    static ResourceRepository()
    {
        _resources = new TrackingCollection<IResourceDictionary>();
        _styles = new TrackingCollection<IStyleSet>();
    }

    public static void AddResourceDictionary(IResourceDictionary resourceDictionary)
    {
        lock (syncObject)
        {
            _resources.Add(resourceDictionary);
        }
    }

    public static IResourceDictionary GetResourceDictionary(string path)
    {
        foreach (var resource in _resources)
        {
            if (resource.Source.OriginalString.Replace("\\", "/") == path)
                return resource;
        }

        return null;
    }
    
    public static void AddStyleSet(IStyleSet set)
    {
        lock (syncObject)
        {
            _styles.Add(set);
        }
    }

    public static IStyleSet GetStyleSet(string path)
    {
        foreach (var resource in _styles)
        {
            if (resource.Source.OriginalString.Replace("\\", "/") == path) return resource;
        }

        return null;
    }
}
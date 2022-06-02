using Adamantium.Core.Collections;
using System.Collections.Generic;

namespace Adamantium.UI;

public class ResourceDictionary : AdamantiumDictionary<string, object>
{
    private Dictionary<string, object> resourceCache;

    public ResourceDictionary()
    {
        MergedDictionaries = new TrackingCollection<ResourceDictionary>();
        MergedDictionaries.CollectionChanged += MergedDictionaries_CollectionChanged;
        resourceCache = new Dictionary<string, object>();
    }

    private void MergedDictionaries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        resourceCache.Clear();
    }

    public TrackingCollection<ResourceDictionary> MergedDictionaries { get; }

    public object FindName(string name)
    {
        if (resourceCache.ContainsKey(name)) return resourceCache[name];

        if (ContainsKey(name)) return this[name];
        
        var stack = new Stack<ResourceDictionary>();
        stack.Push(this);

        while(stack.Count > 0)
        {
            var item = stack.Pop();

            if (item.ContainsKey(name))
            {
                var obj = item[name];
                resourceCache[name] = obj;
                return obj;
            }

            foreach (var dict in item.MergedDictionaries)
            {
                stack.Push(dict);
            }
        }

        return null;
    }
}

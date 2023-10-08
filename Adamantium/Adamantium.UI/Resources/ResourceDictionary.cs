using System;
using System.Collections.Generic;
using Adamantium.Core.Collections;

namespace Adamantium.UI.Resources;

public class ResourceDictionary : IResourceDictionary
{
    private readonly AdamantiumDictionary<string, object> resourceCache;

    private bool _isInitialized;
    private bool _isInitializing;

    public ResourceDictionary()
    {
        MergedDictionaries = new TrackingCollection<ResourceDictionary>();
        MergedDictionaries.CollectionChanged += MergedDictionaries_CollectionChanged;
        resourceCache = new AdamantiumDictionary<string, object>();
    }

    private void MergedDictionaries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        resourceCache.Clear();
    }
    
    public Uri Source { get; set; }

    public TrackingCollection<ResourceDictionary> MergedDictionaries { get; }

    public object FindName(string name)
    {
        if (resourceCache.TryGetValue(name, out var findName)) return findName;

        var stack = new Stack<ResourceDictionary>();
        stack.Push(this);

        while(stack.Count > 0)
        {
            var item = stack.Pop();

            if (item.TryGetValue(name, out var obj))
            {
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

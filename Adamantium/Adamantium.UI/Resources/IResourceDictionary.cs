using System;
using Adamantium.Core.Collections;
using Adamantium.Engine.Core;

namespace Adamantium.UI.Resources;

public interface IResourceDictionary : IResourceContainer, IName, IInitializable
{
    public Uri Source { get; set; }

    public TrackingCollection<ResourceDictionary> MergedDictionaries { get; }

    public object FindName(string name);

    public void Add(string key, object value);

    public void Remove(string key);

    public void Clear();

    public bool TryGetValue(string key, out object value);
    
    object this[string index] { get; set; }

}
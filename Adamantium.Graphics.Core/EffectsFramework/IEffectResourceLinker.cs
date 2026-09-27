using System.Collections.Generic;
using Adamantium.EffectsCompiler;

namespace Adamantium.Graphics.Core.EffectsFramework;

public interface IEffectResourceLinker
{
    /// <summary>
    /// Initializes this instance.
    /// </summary>
    void Initialize();

    T GetResource<T>(EffectData.Parameter resourceName) where T : class;
    T[] GetResources<T>(EffectData.Parameter resourceName) where T : class;
    void SetResource<T>(EffectData.ResourceParameter paramDescription, EffectResourceType type, T value);
    void SetResource<T>(EffectData.ResourceParameter resourceName, EffectResourceType type, params T[] valueArray) where T : class;

    Dictionary<EffectData.Parameter, object> GetBoundResources();
    
    void AddBoundResource(EffectData.Parameter resourceName, object value);
    int Count { get; set; }
}
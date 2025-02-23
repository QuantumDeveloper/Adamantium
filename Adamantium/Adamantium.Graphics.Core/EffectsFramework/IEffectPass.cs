using System;
using Adamantium.Core;

namespace Adamantium.Graphics.Core.EffectsFramework;

public interface IEffectPass : INamedObject, IDisposable
{
    void Initialize(Logger logger);
    
    void PrepareData();
    
    void Apply();
    
    void UnApply(bool fullUnApply = false);
}
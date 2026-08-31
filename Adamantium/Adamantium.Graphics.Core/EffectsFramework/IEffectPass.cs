using System;
using Adamantium.Core;

namespace Adamantium.Graphics.Core.EffectsFramework;

public interface IEffectPass : INamedObject, IDisposable
{
    void Initialize(Logger logger);
    
    void PrepareData();
    
    void Apply();

    /// <summary>Whether every resource this pass declares was actually BOUND on the last <see cref="Apply"/>. False
    /// means at least one was not, and the pass handed the shader the heap's FALLBACK slot for it - a red square in
    /// DEBUG, a transparent one otherwise. The draw still goes ahead: a stand-in is a better answer than a hole in the
    /// frame, and either way the parameter has already been reported by name.</summary>
    bool ResourcesBound { get; }
    
    void UnApply(bool fullUnApply = false);
}
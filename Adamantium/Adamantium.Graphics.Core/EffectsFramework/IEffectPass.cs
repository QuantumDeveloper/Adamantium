using System;
using Adamantium.Core;

namespace Adamantium.Graphics.Core.EffectsFramework;

public interface IEffectPass : INamedObject, IDisposable
{
    void Initialize(Logger logger);
    
    void PrepareData();
    
    void Apply();

    /// <summary>Whether every resource this pass declares was actually BOUND on the last <see cref="Apply"/>. False means
    /// its push data carries an out-of-range heap index, and drawing would sample whoever else owns that slot - so the
    /// device refuses the draw instead of putting someone else's texture on screen.</summary>
    bool ResourcesBound { get; }
    
    void UnApply(bool fullUnApply = false);
}
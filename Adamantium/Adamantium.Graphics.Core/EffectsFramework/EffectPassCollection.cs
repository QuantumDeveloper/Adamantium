using Adamantium.Core.Collections;

namespace Adamantium.Graphics.Core.EffectsFramework;

/// <summary>
/// A collection of <see cref="EffectPass"/>.
/// </summary>
public sealed class EffectPassCollection : NamedObjectsCollection<IEffectPass>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EffectPassCollection" /> class.
    /// </summary>
    internal EffectPassCollection()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EffectPassCollection" /> class.
    /// </summary>
    /// <param name="capacity">The capacity.</param>
    internal EffectPassCollection(int capacity)
        : base(capacity)
    {
    }
}
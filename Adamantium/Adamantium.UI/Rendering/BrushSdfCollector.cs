using Adamantium.Graphics.Core;
using Adamantium.UI.Effects.Generated;

namespace Adamantium.UI.Rendering;

/// <summary>An SDF batch drawn through <c>BrushEffect</c> - the FILLS whose colour is computed or sampled rather than
/// flat: gradients, procedural patterns and noise, textures, fractals. Everything about the batching is the shapes'
/// (see <see cref="ShapeSdfCollector{TItem}"/>); only the effect differs, and it differs because a brush pass carries
/// its own parameters and its own pixel-shader budget.</summary>
internal abstract class BrushSdfCollector<TItem> : SdfBatchCollector<TItem> where TItem : struct
{
    protected BrushEffect Effect;

    protected BrushSdfCollector(int initialCapacity) : base(initialCapacity) { }

    protected override void EnsureEffect(IGraphicsDevice device)
    {
        if (Effect != null) return;

        Effect = new BrushEffect(device);
        ProjectionParam = Effect.Projection;
        ViewportSizeParam = Effect.ViewportSize;
        InstancesAddressParam = Effect.InstancesAddress;
        TransformsAddressParam = Effect.TransformsAddress;
    }
}

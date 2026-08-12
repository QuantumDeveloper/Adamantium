using Adamantium.Graphics.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Effects.Generated;

namespace Adamantium.UI.Rendering;

// The shared services every render unit needs: the device, the effects, the resource factory and the GPU buffer
// manager. Bundled into one object so a NEW shared dependency is added HERE, not threaded through every RenderUnit
// constructor (and the factory call site). Created once per renderer by RenderUnitFactory and passed to each unit.
public sealed class RenderUnitContext
{
    public RenderUnitContext(
        IGraphicsDevice graphicsDevice,
        IResourceFactory resourceFactory,
        UIBasicEffect uiBasicEffect,
        StrokeEffect strokeEffect,
        FillFringeEffect fillFringeEffect,
        GpuBufferManager bufferManager)
    {
        GraphicsDevice = graphicsDevice;
        ResourceFactory = resourceFactory;
        UIBasicEffect = uiBasicEffect;
        StrokeEffect = strokeEffect;
        FillFringeEffect = fillFringeEffect;
        BufferManager = bufferManager;
    }

    /// <summary>Distance fields baked for halos on arbitrary geometry, keyed by the SHAPE, not by the element: meshes are
    /// shared, so a thousand identical badges bake one field between them. Lives here because the cache has to outlive
    /// any one unit and be shared by all of them - the same reason everything else in this bundle does.</summary>
    internal HaloFieldCache HaloFields { get; } = new();

    public IGraphicsDevice GraphicsDevice { get; }
    public IResourceFactory ResourceFactory { get; }
    public UIBasicEffect UIBasicEffect { get; }
    public StrokeEffect StrokeEffect { get; }
    public FillFringeEffect FillFringeEffect { get; }
    public GpuBufferManager BufferManager { get; }
}
